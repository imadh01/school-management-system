using Microsoft.EntityFrameworkCore;
using SchoolManagement.Application.Interfaces;
using SchoolManagement.Domain.Entities;
using SchoolManagement.Infrastructure.Persistence;

namespace SchoolManagement.Infrastructure.Repositories;

/// <summary>
/// Revocations and the rotation claim use ExecuteUpdate/ExecuteDelete: one SQL statement each,
/// no entities loaded, and the audit interceptor is not involved (RefreshToken is [AuditIgnore] anyway).
/// </summary>
public class RefreshTokenRepository : IRefreshTokenRepository
{
    private readonly ApplicationDbContext _context;

    public RefreshTokenRepository(ApplicationDbContext context)
    {
        _context = context;
    }

    public Task<RefreshToken?> GetByHashAsync(byte[] tokenHash, CancellationToken cancellationToken) =>
        _context.RefreshTokens
            .AsNoTracking()
            .FirstOrDefaultAsync(t => t.TokenHash == tokenHash, cancellationToken);

    public async Task AddAsync(RefreshToken token, CancellationToken cancellationToken)
    {
        _context.RefreshTokens.Add(token);
        await _context.SaveChangesAsync(cancellationToken);
    }

    public async Task<bool> TryRotateAsync(
        long currentTokenId, RefreshToken replacement, DateTime revokedAt, CancellationToken cancellationToken)
    {
        var strategy = _context.Database.CreateExecutionStrategy();
        return await strategy.ExecuteAsync(async () =>
        {
            await using var transaction = await _context.Database.BeginTransactionAsync(cancellationToken);

            // 1. Insert the replacement first, so its Id exists for ReplacedByTokenId.
            _context.RefreshTokens.Add(replacement);
            await _context.SaveChangesAsync(cancellationToken);

            // 2. Claim the current token, but only if nobody has revoked it yet. SQL Server locks
            //    the row for this UPDATE: a second request racing with the same token waits here,
            //    then sees RevokedAt already set and updates 0 rows. Exactly one request can win.
            var claimed = await _context.RefreshTokens
                .Where(t => t.Id == currentTokenId && t.RevokedAt == null)
                .ExecuteUpdateAsync(setters => setters
                    .SetProperty(t => t.RevokedAt, (DateTime?)revokedAt)
                    .SetProperty(t => t.RevokedReason, (string?)RefreshTokenRevokeReasons.Rotated)
                    .SetProperty(t => t.ReplacedByTokenId, (long?)replacement.Id),
                    cancellationToken);

            if (claimed == 1)
            {
                await transaction.CommitAsync(cancellationToken);
                return true;
            }

            // Lost the race: undo the insert so the losing request leaves nothing behind.
            await transaction.RollbackAsync(cancellationToken);
            _context.Entry(replacement).State = EntityState.Detached;
            return false;
        });
    }

    public Task<int> RevokeFamilyAsync(
        Guid familyId, string reason, DateTime revokedAt, CancellationToken cancellationToken) =>
        _context.RefreshTokens
            .Where(t => t.FamilyId == familyId && t.RevokedAt == null)
            .ExecuteUpdateAsync(setters => setters
                .SetProperty(t => t.RevokedAt, (DateTime?)revokedAt)
                .SetProperty(t => t.RevokedReason, (string?)reason),
                cancellationToken);

    public Task<int> RevokeAllForUserAsync(
        int userId, string reason, DateTime revokedAt, CancellationToken cancellationToken) =>
        _context.RefreshTokens
            .Where(t => t.UserId == userId && t.RevokedAt == null)
            .ExecuteUpdateAsync(setters => setters
                .SetProperty(t => t.RevokedAt, (DateTime?)revokedAt)
                .SetProperty(t => t.RevokedReason, (string?)reason),
                cancellationToken);

    public Task<int> DeleteFamiliesExpiredBeforeAsync(DateTime cutoff, CancellationToken cancellationToken) =>
        _context.RefreshTokens
            .Where(t => t.FamilyExpiresAt < cutoff)
            .ExecuteDeleteAsync(cancellationToken);
}
