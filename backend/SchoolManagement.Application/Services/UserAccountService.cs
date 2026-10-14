using SchoolManagement.Application.Interfaces;
using SchoolManagement.Domain.Constants;
using SchoolManagement.Domain.Entities;
using SchoolManagement.Domain.Exceptions;

namespace SchoolManagement.Application.Services;

/// <summary>See <see cref="IUserAccountService"/>.</summary>
public class UserAccountService : IUserAccountService
{
    private readonly IUserRepository _users;
    private readonly IRoleRepository _roles;
    private readonly ITeacherRepository _teachers;
    private readonly IPasswordHasher _passwordHasher;
    private readonly IPermissionCacheService _permissionCache;
    private readonly IRefreshTokenService _refreshTokens;
    private readonly ICurrentUser _currentUser;
    private readonly ICurrentUserPermissions _callerPermissions;
    private readonly IConcurrencyGuard _concurrency;

    public UserAccountService(
        IUserRepository users,
        IRoleRepository roles,
        ITeacherRepository teachers,
        IPasswordHasher passwordHasher,
        IPermissionCacheService permissionCache,
        IRefreshTokenService refreshTokens,
        ICurrentUser currentUser,
        ICurrentUserPermissions callerPermissions,
        IConcurrencyGuard concurrency)
    {
        _users = users;
        _roles = roles;
        _teachers = teachers;
        _passwordHasher = passwordHasher;
        _permissionCache = permissionCache;
        _refreshTokens = refreshTokens;
        _currentUser = currentUser;
        _callerPermissions = callerPermissions;
        _concurrency = concurrency;
    }

    public Task ChangeStatusAsync(int userId, string status, string? expectedRowVersion, CancellationToken cancellationToken)
    {
        if (!UserStatuses.IsValid(status))
            throw new BusinessRuleException($"Status must be one of: {string.Join(", ", UserStatuses.All)}.");

        return ApplyAsync(
            userId,
            "change the status of",
            expectedRowVersion,
            staysActiveAdmin: _ => status == UserStatuses.Active,
            apply: user =>
            {
                user.Status = status;
                return Task.CompletedTask;
            },
            RefreshTokenRevokeReasons.SessionInvalidated,
            cancellationToken);
    }

    public Task SetTemporaryPasswordAsync(int userId, string newPassword, CancellationToken cancellationToken) =>
        ApplyAsync(
            userId,
            "reset the password of",
            expectedRowVersion: null,
            staysActiveAdmin: _ => true, // a password reset does not change who is an Admin
            apply: user =>
            {
                user.PasswordHash = _passwordHasher.HashPassword(newPassword);
                user.MustChangePassword = true;
                return Task.CompletedTask;
            },
            RefreshTokenRevokeReasons.PasswordChanged,
            cancellationToken);

    public async Task ChangeRoleAsync(int userId, string roleName, CancellationToken cancellationToken)
    {
        var role = await PrepareRoleAsync(roleName, cancellationToken);

        await ApplyAsync(
            userId,
            "change the role of",
            expectedRowVersion: null,
            staysActiveAdmin: user => role.Name == RoleNames.Admin && user.Status == UserStatuses.Active,
            apply: user => SetRoleAsync(user, role, newTeacherSpecialization: null, cancellationToken),
            RefreshTokenRevokeReasons.SessionInvalidated,
            cancellationToken);
    }

    public Task DeleteAsync(int userId, CancellationToken cancellationToken) =>
        ApplyAsync(
            userId,
            "delete",
            expectedRowVersion: null,
            staysActiveAdmin: _ => false,
            apply: async user =>
            {
                var now = DateTime.UtcNow;

                // The teacher record goes with the login, but never while it still teaches something.
                var teacher = await _teachers.GetByUserIdAsync(user.Id, cancellationToken);
                if (teacher is not null)
                {
                    await RefuseWhileTeachingAsync(teacher, "delete this account", cancellationToken);
                    teacher.IsDeleted = true;
                    teacher.DeletedAt = now;
                }

                if (user.StaffProfile is { } profile)
                {
                    profile.IsDeleted = true;
                    profile.DeletedAt = now;
                }

                user.IsDeleted = true;
                user.DeletedAt = now;
            },
            RefreshTokenRevokeReasons.SessionInvalidated,
            cancellationToken);

    // ------------------------------------------------------------------ role change inside a larger save

    public async Task<Role> PrepareRoleAsync(string roleName, CancellationToken cancellationToken)
    {
        var role = await _roles.GetByNameAsync(roleName, cancellationToken)
            ?? throw new BusinessRuleException($"Role '{roleName}' does not exist.");
        if (!role.IsActive)
            throw new BusinessRuleException($"Role '{role.Name}' is inactive and cannot be given to anyone.");

        // Decision 1: no student or parent logins until the portal is designed.
        if (role.Name is RoleNames.Student or RoleNames.Parent)
            throw new BusinessRuleException(
                $"The {role.Name} role cannot be given yet: student and parent logins come with the portal.");

        // E1 rule: you cannot hand out permissions you do not hold yourself.
        await GuardCallerHoldsRoleAsync(role, cancellationToken);
        return role;
    }

    public async Task<bool> ApplyRoleChangeAsync(
        User user, Role role, string? newTeacherSpecialization, CancellationToken cancellationToken)
    {
        if (HasOnly(user, role))
            return false;

        GuardNotSelf(user.Id, "change the role of");
        await _users.LockAccountChangesAsync(cancellationToken);
        await GuardLastAdminAsync(user, staysActiveAdmin: role.Name == RoleNames.Admin && user.Status == UserStatuses.Active,
            cancellationToken);

        await SetRoleAsync(user, role, newTeacherSpecialization, cancellationToken);
        user.SecurityStamp = Guid.NewGuid();
        user.NewVersion();
        return true;
    }

    public async Task EndSessionsAsync(int userId, string revokeReason, CancellationToken cancellationToken)
    {
        // AFTER the commit, so no request can reload the old status/role into the cache in between.
        _permissionCache.InvalidateUser(userId);

        // The new stamp already makes every refresh token useless; revoking them as well records why.
        await _refreshTokens.RevokeAllForUserAsync(userId, revokeReason, cancellationToken);
    }

    // ------------------------------------------------------------------ the shared steps

    /// <param name="action">For messages: "You cannot {action} your own account."</param>
    /// <param name="staysActiveAdmin">Is the user still an active Admin AFTER the change? (Only asked when they are one now.)</param>
    private async Task ApplyAsync(
        int userId,
        string action,
        string? expectedRowVersion,
        Func<User, bool> staysActiveAdmin,
        Func<User, Task> apply,
        string revokeReason,
        CancellationToken cancellationToken)
    {
        GuardNotSelf(userId, action);

        await _users.ExecuteInTransactionAsync(async () =>
        {
            // Every account change waits here for the previous one to commit. Without it, two admins
            // could each deactivate the other at the same moment: both would still see one other
            // active Admin, both would succeed, and the school would have none.
            await _users.LockAccountChangesAsync(cancellationToken);

            var user = await _users.GetWithProfileAsync(userId, cancellationToken)
                ?? throw new NotFoundException($"User {userId} was not found.");
            if (expectedRowVersion is not null)
                _concurrency.Expect(user, expectedRowVersion); // 409 if someone saved this user after it was loaded

            await GuardLastAdminAsync(user, staysActiveAdmin(user), cancellationToken);

            await apply(user);
            user.SecurityStamp = Guid.NewGuid(); // the current access token stops working at once (D1)
            user.NewVersion();                   // an edit form opened before this change gets a 409

            await _users.SaveChangesAsync(cancellationToken);
            return true;
        }, cancellationToken);

        await EndSessionsAsync(userId, revokeReason, cancellationToken);
    }

    private void GuardNotSelf(int userId, string action)
    {
        if (_currentUser.UserId == userId)
            throw new BusinessRuleException(
                $"You cannot {action} your own account. Ask another administrator, or use My Account for your own details and password.");
    }

    private async Task GuardLastAdminAsync(User user, bool staysActiveAdmin, CancellationToken cancellationToken)
    {
        if (IsActiveAdmin(user) && !staysActiveAdmin
            && await _users.CountActiveAdminsAsync(excludeUserId: user.Id, cancellationToken) == 0)
        {
            throw new BusinessRuleException(
                $"'{user.Username}' is the last active Admin. Make another user an active Admin first, " +
                "so the school is never left without one.");
        }
    }

    /// <summary>One role per user (service rule; the table stays many-to-many), and the teacher record follows.</summary>
    private async Task SetRoleAsync(User user, Role role, string? newTeacherSpecialization, CancellationToken cancellationToken)
    {
        if (HasOnly(user, role))
            return; // already this role

        var wasTeacher = user.UserRoles.Any(ur => ur.Role?.Name == RoleNames.Teacher);
        var becomesTeacher = role.Name == RoleNames.Teacher;
        // A user being created has no id yet, so no teacher record either.
        var teacher = user.Id == 0 ? null : await _teachers.GetByUserIdAsync(user.Id, cancellationToken);

        if (wasTeacher && !becomesTeacher && teacher is not null)
        {
            await RefuseWhileTeachingAsync(teacher, "move this teacher to another role", cancellationToken);
            teacher.IsDeleted = true;
            teacher.DeletedAt = DateTime.UtcNow;
        }

        user.UserRoles.Clear();
        user.UserRoles.Add(new UserRole { User = user, Role = role, AssignedAt = DateTime.UtcNow });

        if (becomesTeacher && teacher is null)
        {
            // The teacher's name and phone are the profile's (E2c: no copy on the Teachers row).
            _teachers.Add(new Teacher
            {
                User = user, // also works for a user created in the same save
                Specialization = newTeacherSpecialization,
            });
        }
    }

    private async Task RefuseWhileTeachingAsync(Teacher teacher, string action, CancellationToken cancellationToken)
    {
        var subjects = (await _teachers.GetAssignedSubjectsAsync(teacher.Id, cancellationToken)).Count;
        var classes = (await _teachers.GetClassTeacherSectionIdsAsync(teacher.Id, cancellationToken)).Count;
        if (subjects > 0 || classes > 0)
            throw new BusinessRuleException(
                $"Cannot {action}: the teacher still teaches {subjects} subject(s) and is class teacher of " +
                $"{classes} class(es). Remove the assignments on the Teachers page first.");
    }

    private static bool HasOnly(User user, Role role) =>
        user.UserRoles.Count == 1
        && (user.UserRoles.First().Role?.Id ?? user.UserRoles.First().RoleId) == role.Id;

    private static bool IsActiveAdmin(User user) =>
        !user.IsDeleted
        && user.Status == UserStatuses.Active
        && user.UserRoles.Any(ur => ur.Role?.Name == RoleNames.Admin);

    private async Task GuardCallerHoldsRoleAsync(Role role, CancellationToken cancellationToken)
    {
        IReadOnlyCollection<string> required = role.Name == RoleNames.Admin
            ? Permissions.All
            : (await _roles.GetWithPermissionsAsync(role.Id, cancellationToken))!
                .RolePermissions.Select(rp => rp.Permission.Name).ToList();

        var held = await _callerPermissions.GetPermissionsAsync(cancellationToken);
        var notHeld = required.Where(p => !held.Contains(p)).OrderBy(p => p, StringComparer.Ordinal).ToList();
        if (notHeld.Count > 0)
            throw new ForbiddenException(
                $"You cannot give the '{role.Name}' role: it has permissions you do not hold yourself ({string.Join(", ", notHeld)}).");
    }
}
