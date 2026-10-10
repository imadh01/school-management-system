using SchoolManagement.Application.DTOs.Auth;
using SchoolManagement.Domain.Entities;

namespace SchoolManagement.Application.Interfaces;

/// <summary>
/// The mechanics of refresh tokens: generating, hashing, rotating, revoking and spotting reuse.
/// Deciding WHO may get a token (user active, stamp still valid) is AuthService's job.
/// </summary>
public interface IRefreshTokenService
{
    /// <summary>Starts a new family (a new login on a new device) and returns its first token.</summary>
    Task<IssuedRefreshToken> IssueAsync(User user, ClientInfo client, CancellationToken cancellationToken);

    /// <summary>
    /// Finds the token behind a raw cookie value and classifies it. When the token was already
    /// rotated (state <see cref="RefreshTokenState.Reused"/>) this also revokes its whole family
    /// and logs a security warning before returning.
    /// </summary>
    Task<RefreshTokenLookup> LookupAsync(string? rawToken, CancellationToken cancellationToken);

    /// <summary>
    /// Exchanges an active token for a new one in the same family. Returns null when another
    /// request won the race for the same token. That counts as reuse: the family is revoked and
    /// a warning is logged.
    /// </summary>
    Task<IssuedRefreshToken?> RotateAsync(RefreshToken current, User user, ClientInfo client, CancellationToken cancellationToken);

    Task RevokeFamilyAsync(Guid familyId, string reason, CancellationToken cancellationToken);

    Task RevokeAllForUserAsync(int userId, string reason, CancellationToken cancellationToken);

    /// <summary>Deletes families that expired more than RetentionDays ago. Returns rows deleted.</summary>
    Task<int> PurgeExpiredAsync(CancellationToken cancellationToken);
}

public enum RefreshTokenState
{
    /// <summary>No such token: never issued, purged, or a forged/garbled cookie.</summary>
    NotFound,
    /// <summary>Usable.</summary>
    Active,
    /// <summary>Idle or absolute lifetime has passed.</summary>
    Expired,
    /// <summary>Revoked by logout, password change, etc.</summary>
    Revoked,
    /// <summary>Already rotated and presented again: the family has just been revoked.</summary>
    Reused,
}

public sealed record RefreshTokenLookup(RefreshTokenState State, RefreshToken? Token)
{
    public static readonly RefreshTokenLookup NotFound = new(RefreshTokenState.NotFound, null);
}
