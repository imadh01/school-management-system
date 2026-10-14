using SchoolManagement.Application.DTOs.Addresses;
using SchoolManagement.Application.DTOs.Users;
using SchoolManagement.Application.Interfaces;
using SchoolManagement.Domain.Entities;
using SchoolManagement.Domain.Exceptions;

namespace SchoolManagement.Application.Services;

/// <summary>
/// E2: the Users page and My Account. What this service saves itself: the account's username/e-mail, the
/// staff profile and its addresses. Everything that changes what a user may DO (status, password, role,
/// delete) goes through <see cref="IUserAccountService"/>, which locks, guards and ends sessions.
/// </summary>
public class UserService : IUserService
{
    private readonly IUserRepository _users;
    private readonly ITeacherRepository _teachers;
    private readonly IUserAccountService _accounts;
    private readonly IPasswordHasher _passwordHasher;
    private readonly IAddressService _addresses;
    private readonly IConcurrencyGuard _concurrency;
    private readonly IPermissionCacheService _permissionCache;
    private readonly ICurrentUser _currentUser;

    public UserService(
        IUserRepository users,
        ITeacherRepository teachers,
        IUserAccountService accounts,
        IPasswordHasher passwordHasher,
        IAddressService addresses,
        IConcurrencyGuard concurrency,
        IPermissionCacheService permissionCache,
        ICurrentUser currentUser)
    {
        _users = users;
        _teachers = teachers;
        _accounts = accounts;
        _passwordHasher = passwordHasher;
        _addresses = addresses;
        _concurrency = concurrency;
        _permissionCache = permissionCache;
        _currentUser = currentUser;
    }

    // ------------------------------------------------------------------ reads

    public Task<List<UserListItemResponse>> GetAllAsync(CancellationToken cancellationToken) =>
        _users.GetListAsync(DateTime.UtcNow, cancellationToken);

    public async Task<UserResponse> GetByIdAsync(int id, CancellationToken cancellationToken) =>
        await ToResponseAsync(await GetOrThrowAsync(id, cancellationToken), cancellationToken);

    // ------------------------------------------------------------------ create

    public async Task<UserResponse> CreateUserAsync(CreateUserRequest request, CancellationToken cancellationToken)
    {
        var username = request.Username.Trim();
        var email = request.Email.Trim();
        await GuardUniqueAsync(username, email, Clean(request.Profile.EmployeeNumber), null, cancellationToken);

        // Checked before the transaction: exists, active, not Student/Parent, and you hold its permissions.
        var role = await _accounts.PrepareRoleAsync(request.RoleName, cancellationToken);
        var addresses = await _addresses.BuildAsync(request.Addresses ?? new(), cancellationToken);

        var profile = new StaffProfile();
        Apply(profile, request.Profile);
        profile.Addresses = addresses;

        var user = new User
        {
            Username = username,
            Email = email,
            PasswordHash = _passwordHasher.HashPassword(request.Password),
            Status = UserStatuses.Active,
            // The password was typed by an administrator: the user chooses their own at the first sign-in.
            MustChangePassword = true,
            StaffProfile = profile,
        };

        await _users.ExecuteInTransactionAsync(async () =>
        {
            // Gives the role (one per user) and, for Teacher, adds the teacher record from the profile.
            await _accounts.ApplyRoleChangeAsync(user, role, Clean(request.Specialization), cancellationToken);
            await _users.AddAsync(user, cancellationToken); // one save: user, role, profile, addresses, teacher
            return true;
        }, cancellationToken);

        return await ToResponseAsync(user, cancellationToken);
    }

    // ------------------------------------------------------------------ update

    public async Task<UserResponse> UpdateAsync(int id, UpdateUserRequest request, CancellationToken cancellationToken)
    {
        var user = await GetOrThrowAsync(id, cancellationToken);
        _concurrency.Expect(user, request.RowVersion); // 409 if someone saved this user after it was loaded

        var username = request.Username.Trim();
        var email = request.Email.Trim();
        await GuardUniqueAsync(username, email, Clean(request.Profile.EmployeeNumber), id, cancellationToken);

        // Only a DIFFERENT role goes through the role checks (an unchanged role needs no permission check).
        var currentRole = user.UserRoles.Select(ur => ur.Role.Name).FirstOrDefault();
        var newRole = string.Equals(currentRole, request.RoleName, StringComparison.OrdinalIgnoreCase)
            ? null
            : await _accounts.PrepareRoleAsync(request.RoleName, cancellationToken);
        var addresses = request.Addresses is null ? null : await _addresses.BuildAsync(request.Addresses, cancellationToken);

        user.Username = username;
        user.Email = email;
        var profile = user.StaffProfile ??= new StaffProfile();
        Apply(profile, request.Profile);
        user.NewVersion(); // also for a profile-only edit: the version covers the account and its profile

        var roleChanged = await _users.ExecuteInTransactionAsync(async () =>
        {
            var changed = newRole is not null
                && await _accounts.ApplyRoleChangeAsync(user, newRole, newTeacherSpecialization: null, cancellationToken);
            if (addresses is not null)
                await _addresses.ReplaceAsync(profile.Addresses, addresses, cancellationToken);
            await _users.SaveChangesAsync(cancellationToken);
            return changed;
        }, cancellationToken);

        if (roleChanged)
            await _accounts.EndSessionsAsync(id, RefreshTokenRevokeReasons.SessionInvalidated, cancellationToken);
        else
            _permissionCache.InvalidateUser(id); // the cached username/e-mail (shown by /auth/me) changed

        return await ToResponseAsync(user, cancellationToken);
    }

    // ------------------------------------------------------------------ account security (account service)

    public async Task<UserResponse> ChangeStatusAsync(int id, ChangeUserStatusRequest request, CancellationToken cancellationToken)
    {
        await _accounts.ChangeStatusAsync(id, request.Status, request.RowVersion, cancellationToken);
        return await GetByIdAsync(id, cancellationToken);
    }

    public Task ResetPasswordAsync(int id, ResetUserPasswordRequest request, CancellationToken cancellationToken) =>
        _accounts.SetTemporaryPasswordAsync(id, request.NewPassword, cancellationToken);

    public Task DeleteAsync(int id, CancellationToken cancellationToken) =>
        _accounts.DeleteAsync(id, cancellationToken);

    // ------------------------------------------------------------------ My Account

    public async Task<UserResponse> GetMyProfileAsync(CancellationToken cancellationToken) =>
        await ToResponseAsync(await GetMeAsync(cancellationToken), cancellationToken);

    public async Task<UserResponse> UpdateMyProfileAsync(UpdateMyProfileRequest request, CancellationToken cancellationToken)
    {
        var user = await GetMeAsync(cancellationToken);
        _concurrency.Expect(user, request.RowVersion);

        var email = request.Email.Trim();
        if (await _users.ExistsByEmailAsync(email, user.Id, cancellationToken))
            throw new ConflictException($"Email '{email}' is already registered.");
        var addresses = request.Addresses is null ? null : await _addresses.BuildAsync(request.Addresses, cancellationToken);

        // Username, role, status, joining date and employee number stay as an administrator set them.
        var profile = user.StaffProfile!;
        user.Email = email;
        profile.FullName = request.FullName.Trim();
        profile.Gender = Clean(request.Gender);
        profile.DateOfBirth = request.DateOfBirth;
        profile.Mobile = Clean(request.Mobile);
        profile.Qualification = Clean(request.Qualification);
        profile.BloodGroup = Clean(request.BloodGroup);
        profile.EmergencyContactName = Clean(request.EmergencyContactName);
        profile.EmergencyContactPhone = Clean(request.EmergencyContactPhone);
        user.NewVersion();

        await _users.ExecuteInTransactionAsync(async () =>
        {
            if (addresses is not null)
                await _addresses.ReplaceAsync(profile.Addresses, addresses, cancellationToken);
            await _users.SaveChangesAsync(cancellationToken);
            return true;
        }, cancellationToken);

        _permissionCache.InvalidateUser(user.Id); // /auth/me shows the cached e-mail
        return await ToResponseAsync(user, cancellationToken);
    }

    // ------------------------------------------------------------------ helpers

    private async Task<User> GetOrThrowAsync(int id, CancellationToken cancellationToken) =>
        await _users.GetWithProfileAsync(id, cancellationToken)
            ?? throw new NotFoundException($"User {id} was not found.");

    private async Task<User> GetMeAsync(CancellationToken cancellationToken)
    {
        var userId = _currentUser.UserId
            ?? throw new ForbiddenException("My Account needs a signed-in user.");
        var user = await GetOrThrowAsync(userId, cancellationToken);
        if (user.StaffProfile is null)
            throw new NotFoundException("Your account has no staff profile.");
        return user;
    }

    private async Task GuardUniqueAsync(
        string username, string email, string? employeeNumber, int? excludeUserId, CancellationToken cancellationToken)
    {
        if (await _users.ExistsByUsernameAsync(username, excludeUserId, cancellationToken))
            throw new ConflictException($"Username '{username}' is already taken.");
        if (await _users.ExistsByEmailAsync(email, excludeUserId, cancellationToken))
            throw new ConflictException($"Email '{email}' is already registered.");
        if (employeeNumber is not null
            && await _users.ExistsByEmployeeNumberAsync(employeeNumber, excludeUserId, cancellationToken))
            throw new ConflictException($"Employee number '{employeeNumber}' is already used by another staff member.");
    }

    private static void Apply(StaffProfile profile, StaffProfileRequest request)
    {
        profile.FullName = request.FullName.Trim();
        profile.Gender = Clean(request.Gender);
        profile.DateOfBirth = request.DateOfBirth;
        profile.Mobile = Clean(request.Mobile);
        profile.JoiningDate = request.JoiningDate;
        profile.Qualification = Clean(request.Qualification);
        profile.EmployeeNumber = Clean(request.EmployeeNumber);
        profile.BloodGroup = Clean(request.BloodGroup);
        profile.EmergencyContactName = Clean(request.EmergencyContactName);
        profile.EmergencyContactPhone = Clean(request.EmergencyContactPhone);
    }

    private async Task<UserResponse> ToResponseAsync(User user, CancellationToken cancellationToken)
    {
        var profile = user.StaffProfile;
        return new UserResponse(
            user.Id,
            user.Username,
            user.Email,
            user.Status,
            user.UserRoles.Select(ur => ur.Role.Name).ToList(),
            user.MustChangePassword,
            user.LockoutEnd > DateTime.UtcNow,
            user.LastLoginAt,
            user.CreatedAt,
            await _users.GetTeacherIdAsync(user.Id, cancellationToken),
            profile is null
                ? null
                : new StaffProfileResponse(
                    profile.FullName,
                    profile.FullName == user.Username,
                    profile.Gender,
                    profile.DateOfBirth,
                    profile.Mobile,
                    profile.JoiningDate,
                    profile.Qualification,
                    profile.EmployeeNumber,
                    profile.BloodGroup,
                    profile.EmergencyContactName,
                    profile.EmergencyContactPhone),
            profile is null ? new List<AddressResponse>() : AddressMapping.ToResponses(profile.Addresses),
            Convert.ToBase64String(user.RowVersion));
    }

    private static string? Clean(string? value) =>
        string.IsNullOrWhiteSpace(value) ? null : value.Trim();
}