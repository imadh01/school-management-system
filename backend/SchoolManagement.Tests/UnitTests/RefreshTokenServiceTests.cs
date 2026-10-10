// D2: unit tests for RefreshTokenService: token generation, hashing, expiry rules,
// rotation and reuse detection. The repository is mocked and time is controlled, so
// expiry is tested by moving the clock instead of waiting.

using Microsoft.Extensions.Logging.Abstractions;
using Moq;
using SchoolManagement.Application.DTOs.Auth;
using SchoolManagement.Application.Interfaces;
using SchoolManagement.Application.Services;
using SchoolManagement.Application.Settings;
using SchoolManagement.Domain.Entities;
using Xunit;

namespace SchoolManagement.Tests.UnitTests;

public class RefreshTokenServiceTests
{
    private static readonly DateTime Now = new(2026, 10, 10, 9, 0, 0, DateTimeKind.Utc);

    private readonly Mock<IRefreshTokenRepository> _repository = new();
    private readonly ManualTimeProvider _time = new(Now);
    private readonly RefreshTokenSettings _settings = new()
    {
        IdleLifetimeDays = 7,
        AbsoluteLifetimeDays = 30,
        RetentionDays = 7,
    };
    private readonly RefreshTokenService _sut;

    public RefreshTokenServiceTests()
    {
        _sut = new RefreshTokenService(
            _repository.Object, _settings, _time, NullLogger<RefreshTokenService>.Instance);
    }

    private static User TestUser() => new() { Id = 42, SecurityStamp = Guid.NewGuid() };

    // ─────────────────────────────────────────────────────────────────
    // Issue
    // ─────────────────────────────────────────────────────────────────

    [Fact]
    public async Task IssueAsync_StoresTheHashNotTheRawToken_WithIdleAndAbsoluteExpiry()
    {
        var user = TestUser();
        RefreshToken? saved = null;
        _repository
            .Setup(r => r.AddAsync(It.IsAny<RefreshToken>(), default))
            .Callback<RefreshToken, CancellationToken>((t, _) => saved = t);

        var issued = await _sut.IssueAsync(user, new ClientInfo("10.0.0.1", "Test browser"), default);

        Assert.NotNull(saved);
        Assert.Equal(43, issued.Token.Length); // 32 bytes, base64url without padding
        Assert.Equal(RefreshTokenService.HashToken(issued.Token), saved!.TokenHash);
        Assert.NotEqual(System.Text.Encoding.UTF8.GetBytes(issued.Token), saved.TokenHash);
        Assert.Equal(user.Id, saved.UserId);
        Assert.Equal(user.SecurityStamp, saved.SecurityStamp);
        Assert.Equal(Now, saved.IssuedAt);
        Assert.Equal(Now.AddDays(7), saved.ExpiresAt);
        Assert.Equal(Now.AddDays(30), saved.FamilyExpiresAt);
        Assert.Equal(saved.ExpiresAt, issued.ExpiresAtUtc);
        Assert.Equal("10.0.0.1", saved.CreatedByIp);
        Assert.NotEqual(Guid.Empty, saved.FamilyId);
    }

    [Fact]
    public async Task IssueAsync_EveryTokenIsDifferent()
    {
        var a = await _sut.IssueAsync(TestUser(), ClientInfo.Unknown, default);
        var b = await _sut.IssueAsync(TestUser(), ClientInfo.Unknown, default);

        Assert.NotEqual(a.Token, b.Token);
    }

    [Fact]
    public async Task IssueAsync_TruncatesAnOverlongUserAgent()
    {
        RefreshToken? saved = null;
        _repository
            .Setup(r => r.AddAsync(It.IsAny<RefreshToken>(), default))
            .Callback<RefreshToken, CancellationToken>((t, _) => saved = t);

        await _sut.IssueAsync(TestUser(), new ClientInfo(null, new string('x', 1000)), default);

        Assert.Equal(RefreshToken.MaxUserAgentLength, saved!.UserAgent!.Length);
    }

    // ─────────────────────────────────────────────────────────────────
    // Lookup
    // ─────────────────────────────────────────────────────────────────

    [Theory]
    [InlineData(null)]
    [InlineData("")]
    [InlineData("   ")]
    public async Task LookupAsync_MissingToken_IsNotFound_WithoutTouchingTheDatabase(string? raw)
    {
        var lookup = await _sut.LookupAsync(raw, default);

        Assert.Equal(RefreshTokenState.NotFound, lookup.State);
        _repository.Verify(r => r.GetByHashAsync(It.IsAny<byte[]>(), It.IsAny<CancellationToken>()), Times.Never);
    }

    [Fact]
    public async Task LookupAsync_UnknownToken_IsNotFound()
    {
        var lookup = await _sut.LookupAsync("unknown", default);

        Assert.Equal(RefreshTokenState.NotFound, lookup.State);
    }

    [Fact]
    public async Task LookupAsync_UsableToken_IsActive()
    {
        var (raw, _) = StoredToken(expiresAt: Now.AddDays(1));

        var lookup = await _sut.LookupAsync(raw, default);

        Assert.Equal(RefreshTokenState.Active, lookup.State);
    }

    [Fact]
    public async Task LookupAsync_AfterExpiry_IsExpired()
    {
        var (raw, _) = StoredToken(expiresAt: Now.AddDays(7));
        _time.Advance(TimeSpan.FromDays(7)); // exactly at expiry counts as expired

        var lookup = await _sut.LookupAsync(raw, default);

        Assert.Equal(RefreshTokenState.Expired, lookup.State);
    }

    [Fact]
    public async Task LookupAsync_RotatedToken_IsReuse_AndRevokesTheFamily()
    {
        var (raw, token) = StoredToken(expiresAt: Now.AddDays(7), revokedReason: RefreshTokenRevokeReasons.Rotated);

        var lookup = await _sut.LookupAsync(raw, default);

        Assert.Equal(RefreshTokenState.Reused, lookup.State);
        _repository.Verify(r => r.RevokeFamilyAsync(
            token.FamilyId, RefreshTokenRevokeReasons.ReuseDetected, Now, default), Times.Once);
    }

    [Fact]
    public async Task LookupAsync_LoggedOutToken_IsRevoked_WithoutRevokingAgain()
    {
        var (raw, _) = StoredToken(expiresAt: Now.AddDays(7), revokedReason: RefreshTokenRevokeReasons.Logout);

        var lookup = await _sut.LookupAsync(raw, default);

        Assert.Equal(RefreshTokenState.Revoked, lookup.State);
        _repository.Verify(r => r.RevokeFamilyAsync(
            It.IsAny<Guid>(), It.IsAny<string>(), It.IsAny<DateTime>(), It.IsAny<CancellationToken>()), Times.Never);
    }

    // ─────────────────────────────────────────────────────────────────
    // Rotate
    // ─────────────────────────────────────────────────────────────────

    [Fact]
    public async Task RotateAsync_KeepsTheFamily_AndSlidesTheIdleWindow()
    {
        var user = TestUser();
        var current = new RefreshToken
        {
            Id = 5, FamilyId = Guid.NewGuid(), UserId = user.Id,
            IssuedAt = Now.AddDays(-3), ExpiresAt = Now.AddDays(4), FamilyExpiresAt = Now.AddDays(27),
        };
        RefreshToken? replacement = null;
        _repository
            .Setup(r => r.TryRotateAsync(5, It.IsAny<RefreshToken>(), Now, default))
            .Callback<long, RefreshToken, DateTime, CancellationToken>((_, t, _, _) => replacement = t)
            .ReturnsAsync(true);

        var issued = await _sut.RotateAsync(current, user, ClientInfo.Unknown, default);

        Assert.NotNull(issued);
        Assert.Equal(current.FamilyId, replacement!.FamilyId);
        Assert.Equal(current.FamilyExpiresAt, replacement.FamilyExpiresAt);
        Assert.Equal(Now.AddDays(7), replacement.ExpiresAt); // fresh 7-day idle window
        Assert.Equal(RefreshTokenService.HashToken(issued!.Token), replacement.TokenHash);
    }

    [Fact]
    public async Task RotateAsync_NearTheAbsoluteLimit_NeverOutlivesTheFamily()
    {
        var user = TestUser();
        var current = new RefreshToken
        {
            Id = 5, FamilyId = Guid.NewGuid(), UserId = user.Id,
            IssuedAt = Now.AddDays(-1), ExpiresAt = Now.AddDays(2), FamilyExpiresAt = Now.AddDays(2),
        };
        RefreshToken? replacement = null;
        _repository
            .Setup(r => r.TryRotateAsync(5, It.IsAny<RefreshToken>(), Now, default))
            .Callback<long, RefreshToken, DateTime, CancellationToken>((_, t, _, _) => replacement = t)
            .ReturnsAsync(true);

        await _sut.RotateAsync(current, user, ClientInfo.Unknown, default);

        Assert.Equal(current.FamilyExpiresAt, replacement!.ExpiresAt); // capped at 2 days, not 7
    }

    [Fact]
    public async Task RotateAsync_LosingTheRace_IsReuse_AndRevokesTheFamily()
    {
        var current = new RefreshToken
        {
            Id = 5, FamilyId = Guid.NewGuid(), UserId = 42,
            IssuedAt = Now, ExpiresAt = Now.AddDays(7), FamilyExpiresAt = Now.AddDays(30),
        };
        _repository
            .Setup(r => r.TryRotateAsync(5, It.IsAny<RefreshToken>(), Now, default))
            .ReturnsAsync(false);

        var issued = await _sut.RotateAsync(current, TestUser(), ClientInfo.Unknown, default);

        Assert.Null(issued);
        _repository.Verify(r => r.RevokeFamilyAsync(
            current.FamilyId, RefreshTokenRevokeReasons.ReuseDetected, Now, default), Times.Once);
    }

    // ─────────────────────────────────────────────────────────────────
    // Cleanup
    // ─────────────────────────────────────────────────────────────────

    [Fact]
    public async Task PurgeExpiredAsync_KeepsDeadFamiliesForTheRetentionPeriod()
    {
        await _sut.PurgeExpiredAsync(default);

        _repository.Verify(r => r.DeleteFamiliesExpiredBeforeAsync(Now.AddDays(-7), default), Times.Once);
    }

    // ─────────────────────────────────────────────────────────────────
    // Helpers
    // ─────────────────────────────────────────────────────────────────

    /// <summary>Makes the mocked repository "contain" a token and returns its raw value.</summary>
    private (string Raw, RefreshToken Token) StoredToken(DateTime expiresAt, string? revokedReason = null)
    {
        var raw = Guid.NewGuid().ToString("N");
        var token = new RefreshToken
        {
            Id = 1,
            UserId = 42,
            FamilyId = Guid.NewGuid(),
            TokenHash = RefreshTokenService.HashToken(raw),
            IssuedAt = Now.AddDays(-1),
            ExpiresAt = expiresAt,
            FamilyExpiresAt = expiresAt.AddDays(20),
            RevokedAt = revokedReason is null ? null : Now.AddHours(-1),
            RevokedReason = revokedReason,
        };

        _repository
            .Setup(r => r.GetByHashAsync(
                It.Is<byte[]>(h => h.SequenceEqual(token.TokenHash)), It.IsAny<CancellationToken>()))
            .ReturnsAsync(token);

        return (raw, token);
    }

    /// <summary>A clock the test moves by hand.</summary>
    private sealed class ManualTimeProvider : TimeProvider
    {
        private DateTimeOffset _now;

        public ManualTimeProvider(DateTime utcNow) => _now = new DateTimeOffset(utcNow);

        public override DateTimeOffset GetUtcNow() => _now;

        public void Advance(TimeSpan by) => _now = _now.Add(by);
    }
}
