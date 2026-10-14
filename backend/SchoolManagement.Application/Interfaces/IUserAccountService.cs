namespace SchoolManagement.Application.Interfaces;

/// <summary>
/// The ONE place where a login account's security changes: status, password set by someone else, role,
/// delete. Every caller (the Teachers endpoints today, the Users endpoints in E2) goes through it, so each
/// change always:
///   1. runs under one lock ("account changes"), so two admins cannot remove each other at the same moment;
///   2. refuses to touch your own account (use My Account / change password for yourself);
///   3. refuses to leave the school without an active Admin;
///   4. rotates the security stamp, so the user's current access token stops working at once;
///   5. after the commit, clears the permission cache entry and revokes the refresh tokens.
/// Problem 1 of the E2 design: before this service, teacher status / delete / password changes skipped 4 and 5.
/// </summary>
public interface IUserAccountService
{
    /// <summary>Active / Inactive / Suspended. Anything but Active ends every session.</summary>
    Task ChangeStatusAsync(int userId, string status, CancellationToken cancellationToken);

    /// <summary>
    /// A password chosen by an administrator. The user must change it at the next sign-in
    /// (MustChangePassword) and every session ends.
    /// </summary>
    Task SetTemporaryPasswordAsync(int userId, string newPassword, CancellationToken cancellationToken);

    /// <summary>Replaces the user's (single) role. The caller must hold every permission of the new role.</summary>
    Task ChangeRoleAsync(int userId, string roleName, CancellationToken cancellationToken);

    /// <summary>
    /// Soft-deletes the login and ends every session. The caller deletes the person's profile rows
    /// (teacher, later staff profile) in the same unit of work before calling this.
    /// </summary>
    Task DeleteAsync(int userId, CancellationToken cancellationToken);
}
