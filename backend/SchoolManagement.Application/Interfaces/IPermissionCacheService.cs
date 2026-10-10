namespace SchoolManagement.Application.Interfaces;

/// <summary>
/// In-memory cache of per-user security info (stamp, status, roles,
/// permissions). Loaded from DB on first request per user, invalidated
/// explicitly after writes commit.
///
/// Single-instance assumption: the cache lives in-process and is NOT
/// distributed. If the deployment ever goes multi-instance, replace
/// this with a shared store (Redis, etc.).
/// </summary>
public interface IPermissionCacheService
{
    /// <summary>
    /// Returns cached security info for the user, loading from the
    /// database on a cache miss. Returns null if the user does not exist.
    /// </summary>
    Task<CachedUserInfo?> GetUserInfoAsync(int userId, CancellationToken cancellationToken = default);

    /// <summary>Removes one user's entry so the next request reloads from DB.</summary>
    void InvalidateUser(int userId);

    /// <summary>
    /// Clears the entire cache. Used after bulk changes such as
    /// role-permission updates where many users may be affected.
    /// </summary>
    void InvalidateAll();
}

/// <summary>
/// Immutable snapshot of a user's security state, held in the
/// permission cache. Compared against JWT claims on every request
/// (in OnTokenValidated) and used by the authorization handler
/// to resolve permissions without touching the database.
/// </summary>
public record CachedUserInfo(
    string SecurityStamp,
    string Username,
    string Email,
    string Status,
    bool IsDeleted,
    bool MustChangePassword,
    DateTime? LockoutEnd,
    IReadOnlyList<string> Roles,
    IReadOnlyList<string> Permissions);
