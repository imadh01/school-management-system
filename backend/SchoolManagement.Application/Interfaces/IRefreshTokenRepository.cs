using SchoolManagement.Domain.Entities;

namespace SchoolManagement.Application.Interfaces;

public interface IRefreshTokenRepository
{
    /// <summary>Untracked lookup by SHA-256 hash. Null when no such token was ever issued (or it was purged).</summary>
    Task<RefreshToken?> GetByHashAsync(byte[] tokenHash, CancellationToken cancellationToken);

    /// <summary>Inserts a token (the first one of a new family) and saves.</summary>
    Task AddAsync(RefreshToken token, CancellationToken cancellationToken);

    /// <summary>
    /// Atomically swaps <paramref name="currentTokenId"/> for <paramref name="replacement"/>:
    /// inserts the replacement and marks the current token "Rotated" in one transaction, but only
    /// if the current token is still unrevoked. Returns false (and saves nothing) when another
    /// request already rotated or revoked it. That is how two requests racing with the same token
    /// are told apart without locks: exactly one of them can win.
    /// </summary>
    Task<bool> TryRotateAsync(long currentTokenId, RefreshToken replacement, DateTime revokedAt, CancellationToken cancellationToken);

    /// <summary>Revokes every still-usable token in the family. Returns how many were revoked.</summary>
    Task<int> RevokeFamilyAsync(Guid familyId, string reason, DateTime revokedAt, CancellationToken cancellationToken);

    /// <summary>Revokes every still-usable token of the user, on every device. Returns how many were revoked.</summary>
    Task<int> RevokeAllForUserAsync(int userId, string reason, DateTime revokedAt, CancellationToken cancellationToken);

    /// <summary>
    /// Deletes every token whose family ended before <paramref name="cutoff"/>. A family shares one
    /// FamilyExpiresAt, so whole families are deleted together. Returns the number of rows deleted.
    /// </summary>
    Task<int> DeleteFamiliesExpiredBeforeAsync(DateTime cutoff, CancellationToken cancellationToken);
}
