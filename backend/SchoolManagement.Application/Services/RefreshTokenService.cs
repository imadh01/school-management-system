using System.Security.Cryptography;
using System.Text;
using Microsoft.Extensions.Logging;
using SchoolManagement.Application.DTOs.Auth;
using SchoolManagement.Application.Interfaces;
using SchoolManagement.Application.Settings;
using SchoolManagement.Domain.Entities;

namespace SchoolManagement.Application.Services;

/// <summary>
/// Issues, rotates and revokes refresh tokens.
///
/// Three rules make this safe:
///   1. HASHED STORAGE: the raw token (32 random bytes) is only ever in the cookie; the database
///      keeps SHA-256 of it. A fast hash is fine here because, unlike a password, 256 random bits
///      cannot be guessed, and an unsalted hash lets us find the row with one index seek.
///   2. ROTATION: every refresh revokes the presented token and issues a new one in the same
///      family. The new one gets a fresh idle window but never outlives the family's absolute expiry.
///   3. REUSE DETECTION: a token that was already rotated must never come back. If it does, either
///      it was stolen or something replayed it, and we cannot tell which side is the attacker,
///      so the whole family is revoked. Other devices (other families) are not affected.
/// </summary>
public class RefreshTokenService : IRefreshTokenService
{
    private const int TokenSizeBytes = 32; // 256 bits of randomness

    private readonly IRefreshTokenRepository _repository;
    private readonly RefreshTokenSettings _settings;
    private readonly TimeProvider _timeProvider;
    private readonly ILogger<RefreshTokenService> _logger;

    public RefreshTokenService(
        IRefreshTokenRepository repository,
        RefreshTokenSettings settings,
        TimeProvider timeProvider,
        ILogger<RefreshTokenService> logger)
    {
        _repository = repository;
        _settings = settings;
        _timeProvider = timeProvider;
        _logger = logger;
    }

    public async Task<IssuedRefreshToken> IssueAsync(User user, ClientInfo client, CancellationToken cancellationToken)
    {
        var now = UtcNow();
        var (rawToken, token) = CreateToken(
            user, Guid.NewGuid(), familyExpiresAt: now.AddDays(_settings.AbsoluteLifetimeDays), now, client);

        await _repository.AddAsync(token, cancellationToken);

        _logger.LogInformation(
            "Refresh token family {FamilyId} started for user {UserId}", token.FamilyId, user.Id);

        return new IssuedRefreshToken(rawToken, token.ExpiresAt);
    }

    public async Task<RefreshTokenLookup> LookupAsync(string? rawToken, CancellationToken cancellationToken)
    {
        if (string.IsNullOrWhiteSpace(rawToken))
            return RefreshTokenLookup.NotFound;

        var token = await _repository.GetByHashAsync(HashToken(rawToken), cancellationToken);
        if (token is null)
            return RefreshTokenLookup.NotFound;

        if (token.RevokedAt is not null)
        {
            if (token.RevokedReason == RefreshTokenRevokeReasons.Rotated)
            {
                await RevokeFamilyForReuseAsync(token, cancellationToken);
                return new RefreshTokenLookup(RefreshTokenState.Reused, token);
            }

            return new RefreshTokenLookup(RefreshTokenState.Revoked, token);
        }

        // ExpiresAt is never later than FamilyExpiresAt (a CHECK constraint guarantees it),
        // so this one comparison covers both the idle and the absolute lifetime.
        if (token.ExpiresAt <= UtcNow())
            return new RefreshTokenLookup(RefreshTokenState.Expired, token);

        return new RefreshTokenLookup(RefreshTokenState.Active, token);
    }

    public async Task<IssuedRefreshToken?> RotateAsync(
        RefreshToken current, User user, ClientInfo client, CancellationToken cancellationToken)
    {
        var now = UtcNow();
        var (rawToken, replacement) = CreateToken(user, current.FamilyId, current.FamilyExpiresAt, now, client);

        if (await _repository.TryRotateAsync(current.Id, replacement, now, cancellationToken))
            return new IssuedRefreshToken(rawToken, replacement.ExpiresAt);

        // Another request rotated this same token a moment ago: it was used twice.
        await RevokeFamilyForReuseAsync(current, cancellationToken);
        return null;
    }

    public Task RevokeFamilyAsync(Guid familyId, string reason, CancellationToken cancellationToken) =>
        _repository.RevokeFamilyAsync(familyId, reason, UtcNow(), cancellationToken);

    public async Task RevokeAllForUserAsync(int userId, string reason, CancellationToken cancellationToken)
    {
        var revoked = await _repository.RevokeAllForUserAsync(userId, reason, UtcNow(), cancellationToken);

        _logger.LogInformation(
            "Revoked {RevokedCount} refresh token(s) for user {UserId}: {Reason}", revoked, userId, reason);
    }

    public Task<int> PurgeExpiredAsync(CancellationToken cancellationToken) =>
        _repository.DeleteFamiliesExpiredBeforeAsync(UtcNow().AddDays(-_settings.RetentionDays), cancellationToken);

    /// <summary>SHA-256 of the raw token, the value stored in RefreshTokens.TokenHash.</summary>
    public static byte[] HashToken(string rawToken) => SHA256.HashData(Encoding.UTF8.GetBytes(rawToken));

    // ------------------------------------------------------------------ helpers

    private async Task RevokeFamilyForReuseAsync(RefreshToken token, CancellationToken cancellationToken)
    {
        var revoked = await _repository.RevokeFamilyAsync(
            token.FamilyId, RefreshTokenRevokeReasons.ReuseDetected, UtcNow(), cancellationToken);

        // Warning, not Information: this is the signal of a possibly stolen token.
        // The token value and its hash are never logged.
        _logger.LogWarning(
            "Refresh token reuse detected for user {UserId} (family {FamilyId}, token {TokenId}). " +
            "Revoked {RevokedCount} active token(s) in the family",
            token.UserId, token.FamilyId, token.Id, revoked);
    }

    private (string RawToken, RefreshToken Token) CreateToken(
        User user, Guid familyId, DateTime familyExpiresAt, DateTime now, ClientInfo client)
    {
        var rawToken = ToBase64Url(RandomNumberGenerator.GetBytes(TokenSizeBytes));

        // Sliding idle window, capped by the family's absolute expiry.
        var idleExpiry = now.AddDays(_settings.IdleLifetimeDays);

        var token = new RefreshToken
        {
            UserId = user.Id,
            TokenHash = HashToken(rawToken),
            FamilyId = familyId,
            SecurityStamp = user.SecurityStamp,
            IssuedAt = now,
            ExpiresAt = idleExpiry < familyExpiresAt ? idleExpiry : familyExpiresAt,
            FamilyExpiresAt = familyExpiresAt,
            CreatedByIp = Truncate(client.IpAddress, RefreshToken.MaxIpAddressLength),
            UserAgent = Truncate(client.UserAgent, RefreshToken.MaxUserAgentLength),
        };

        return (rawToken, token);
    }

    private DateTime UtcNow() => _timeProvider.GetUtcNow().UtcDateTime;

    /// <summary>URL- and cookie-safe base64 without padding: 32 bytes become 43 characters.</summary>
    private static string ToBase64Url(byte[] bytes) =>
        Convert.ToBase64String(bytes).TrimEnd('=').Replace('+', '-').Replace('/', '_');

    private static string? Truncate(string? value, int maxLength) =>
        string.IsNullOrEmpty(value) ? null : value.Length <= maxLength ? value : value[..maxLength];
}
