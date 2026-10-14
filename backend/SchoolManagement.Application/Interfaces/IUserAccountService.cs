using SchoolManagement.Domain.Entities;

namespace SchoolManagement.Application.Interfaces;

/// <summary>
/// The ONE place where a login account's security changes: status, password set by someone else, role,
/// delete. Every caller (the Users and Teachers endpoints) goes through it, so each change always:
///   1. runs under one lock ("account changes"), so two admins cannot remove each other at the same moment;
///   2. refuses to touch your own account (use My Account / change password for yourself);
///   3. refuses to leave the school without an active Admin;
///   4. rotates the security stamp, so the user's current access token stops working at once;
///   5. after the commit, clears the permission cache entry and revokes the refresh tokens.
/// It also keeps the teacher record in step with the role: becoming a Teacher creates the Teachers row,
/// leaving the role (or being deleted) is refused while the teacher still has assignments.
/// </summary>
public interface IUserAccountService
{
    /// <summary>Active / Inactive / Suspended. Anything but Active ends every session.</summary>
    /// <param name="expectedRowVersion">The version the caller saw (409 when stale), or null to skip the check.</param>
    Task ChangeStatusAsync(int userId, string status, string? expectedRowVersion, CancellationToken cancellationToken);

    /// <summary>
    /// A password chosen by an administrator. The user must change it at the next sign-in
    /// (MustChangePassword) and every session ends.
    /// </summary>
    Task SetTemporaryPasswordAsync(int userId, string newPassword, CancellationToken cancellationToken);

    /// <summary>Replaces the user's (single) role. The caller must hold every permission of the new role.</summary>
    Task ChangeRoleAsync(int userId, string roleName, CancellationToken cancellationToken);

    /// <summary>Soft-deletes the login, its staff profile and its teacher record, and ends every session.</summary>
    Task DeleteAsync(int userId, CancellationToken cancellationToken);

    // ── For a larger save that includes a role change (the Users edit form) ──────────────────────

    /// <summary>
    /// The role, checked: exists, active, not Student/Parent (decision 1), and the caller holds every
    /// permission it grants (E1 rule). Call BEFORE opening the transaction.
    /// </summary>
    Task<Role> PrepareRoleAsync(string roleName, CancellationToken cancellationToken);

    /// <summary>
    /// Applies a role change to a user loaded inside the caller's transaction: takes the account lock,
    /// runs every guard, creates/removes the teacher record, rotates the stamp. Does not save.
    /// Also used for a user being created (not saved yet): the teacher record is then added with it.
    /// Returns false when the user already had this role (nothing changed).
    /// After the commit, call <see cref="EndSessionsAsync"/>.
    /// </summary>
    /// <param name="newTeacherSpecialization">Written to the teacher record when this change creates one.</param>
    Task<bool> ApplyRoleChangeAsync(User user, Role role, string? newTeacherSpecialization, CancellationToken cancellationToken);

    /// <summary>After the commit: clear the cached permissions and revoke the refresh tokens.</summary>
    Task EndSessionsAsync(int userId, string revokeReason, CancellationToken cancellationToken);
}
