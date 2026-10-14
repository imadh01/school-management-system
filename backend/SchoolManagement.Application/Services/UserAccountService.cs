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
    private readonly IPasswordHasher _passwordHasher;
    private readonly IPermissionCacheService _permissionCache;
    private readonly IRefreshTokenService _refreshTokens;
    private readonly ICurrentUser _currentUser;
    private readonly ICurrentUserPermissions _callerPermissions;

    public UserAccountService(
        IUserRepository users,
        IRoleRepository roles,
        IPasswordHasher passwordHasher,
        IPermissionCacheService permissionCache,
        IRefreshTokenService refreshTokens,
        ICurrentUser currentUser,
        ICurrentUserPermissions callerPermissions)
    {
        _users = users;
        _roles = roles;
        _passwordHasher = passwordHasher;
        _permissionCache = permissionCache;
        _refreshTokens = refreshTokens;
        _currentUser = currentUser;
        _callerPermissions = callerPermissions;
    }

    public Task ChangeStatusAsync(int userId, string status, CancellationToken cancellationToken)
    {
        if (!UserStatuses.IsValid(status))
            throw new BusinessRuleException($"Status must be one of: {string.Join(", ", UserStatuses.All)}.");

        return ApplyAsync(
            userId,
            "change the status of",
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
        var role = await _roles.GetByNameAsync(roleName, cancellationToken)
            ?? throw new BusinessRuleException($"Role '{roleName}' does not exist.");
        if (!role.IsActive)
            throw new BusinessRuleException($"Role '{role.Name}' is inactive and cannot be given to anyone.");

        // E2 decision 1: no student or parent logins until the portal is designed.
        if (role.Name is RoleNames.Student or RoleNames.Parent)
            throw new BusinessRuleException($"The {role.Name} role cannot be given yet: student and parent logins come with the portal.");

        // A teacher is a login PLUS a Teachers row with assignments. Until E2 handles that row here,
        // moving into or out of the Teacher role happens on the Teachers page only.
        if (role.Name == RoleNames.Teacher)
            throw new BusinessRuleException("Use the Teachers page to make someone a teacher.");

        // E1 rule: you cannot hand out permissions you do not hold yourself.
        await GuardCallerHoldsRoleAsync(role, cancellationToken);

        await ApplyAsync(
            userId,
            "change the role of",
            staysActiveAdmin: user => role.Name == RoleNames.Admin && user.Status == UserStatuses.Active,
            apply: user =>
            {
                if (user.UserRoles.Any(ur => ur.Role.Name == RoleNames.Teacher))
                    throw new BusinessRuleException("This user is a teacher. Change teachers on the Teachers page.");

                if (user.UserRoles.Count == 1 && user.UserRoles.First().RoleId == role.Id)
                    return Task.CompletedTask; // already this role (the stamp still rotates: harmless)

                // One role per user (service rule; the table stays many-to-many).
                user.UserRoles.Clear();
                user.UserRoles.Add(new UserRole { UserId = user.Id, RoleId = role.Id, AssignedAt = DateTime.UtcNow });
                return Task.CompletedTask;
            },
            RefreshTokenRevokeReasons.SessionInvalidated,
            cancellationToken);
    }

    public Task DeleteAsync(int userId, CancellationToken cancellationToken) =>
        ApplyAsync(
            userId,
            "delete",
            staysActiveAdmin: _ => false,
            apply: user =>
            {
                user.IsDeleted = true;
                user.DeletedAt = DateTime.UtcNow;
                return Task.CompletedTask;
            },
            RefreshTokenRevokeReasons.SessionInvalidated,
            cancellationToken);

    // ------------------------------------------------------------------ the shared steps

    /// <param name="action">For messages: "You cannot {action} your own account."</param>
    /// <param name="staysActiveAdmin">Is the user still an active Admin AFTER the change? (Only asked when they are one now.)</param>
    private async Task ApplyAsync(
        int userId,
        string action,
        Func<User, bool> staysActiveAdmin,
        Func<User, Task> apply,
        string revokeReason,
        CancellationToken cancellationToken)
    {
        if (_currentUser.UserId == userId)
            throw new BusinessRuleException(
                $"You cannot {action} your own account. Ask another administrator, or use My Account for your own password.");

        await _users.ExecuteInTransactionAsync(async () =>
        {
            // Every account change waits here for the previous one to commit. Without it, two admins
            // could each deactivate the other at the same moment: both would still see one other
            // active Admin, both would succeed, and the school would have none.
            await _users.LockAccountChangesAsync(cancellationToken);

            var user = await _users.GetByIdAsync(userId, cancellationToken)
                ?? throw new NotFoundException($"User {userId} was not found.");

            if (IsActiveAdmin(user) && !staysActiveAdmin(user)
                && await _users.CountActiveAdminsAsync(excludeUserId: user.Id, cancellationToken) == 0)
            {
                throw new BusinessRuleException(
                    $"'{user.Username}' is the last active Admin. Make another user an active Admin first, " +
                    "so the school is never left without one.");
            }

            await apply(user);
            user.SecurityStamp = Guid.NewGuid(); // the current access token stops working at once (D1)

            await _users.SaveChangesAsync(cancellationToken);
            return true;
        }, cancellationToken);

        // AFTER the commit, so no request can reload the old status/role into the cache in between.
        _permissionCache.InvalidateUser(userId);

        // The new stamp already makes every refresh token useless; revoking them as well records why.
        await _refreshTokens.RevokeAllForUserAsync(userId, revokeReason, cancellationToken);
    }

    private static bool IsActiveAdmin(User user) =>
        !user.IsDeleted
        && user.Status == UserStatuses.Active
        && user.UserRoles.Any(ur => ur.Role.Name == RoleNames.Admin);

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
