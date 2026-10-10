// D1: Updated for the new AuthService constructor (+ IPermissionCacheService)
// and the new IJwtTokenGenerator.GenerateToken(User) signature (no roles/permissions).
// Added tests for lockout and MustChangePassword.
//
// D2: AuthService now also takes IRefreshTokenService; login/change-password return an
// AuthSession (access result + refresh token). Added tests for refresh, logout-all and
// the password-change session rules.

using Moq;
using SchoolManagement.Application.DTOs.Auth;
using SchoolManagement.Application.Interfaces;
using SchoolManagement.Application.Services;
using SchoolManagement.Domain.Entities;
using SchoolManagement.Domain.Exceptions;
using Xunit;

namespace SchoolManagement.Tests.UnitTests;

public class AuthServiceTests
{
    private readonly Mock<IUserRepository> _userRepository = new();
    private readonly Mock<IPasswordHasher> _passwordHasher = new();
    private readonly Mock<IJwtTokenGenerator> _tokenGenerator = new();
    private readonly Mock<IPermissionCacheService> _permissionCache = new();
    private readonly Mock<IRefreshTokenService> _refreshTokens = new();
    private readonly AuthService _sut;

    private static readonly IssuedRefreshToken IssuedToken = new("raw-refresh-token", DateTime.UtcNow.AddDays(7));

    public AuthServiceTests()
    {
        _refreshTokens
            .Setup(r => r.IssueAsync(It.IsAny<User>(), It.IsAny<ClientInfo>(), It.IsAny<CancellationToken>()))
            .ReturnsAsync(IssuedToken);

        _sut = new AuthService(
            _userRepository.Object,
            _passwordHasher.Object,
            _tokenGenerator.Object,
            _permissionCache.Object,
            _refreshTokens.Object);
    }

    private Task<AuthSession?> Login(string usernameOrEmail, string password) =>
        _sut.LoginAsync(new LoginRequest(usernameOrEmail, password), ClientInfo.Unknown, default);

    private static User ActiveUser() => new()
    {
        Id = 1,
        Username = "admin",
        Email = "admin@test.com",
        PasswordHash = "hashed-value",
        Status = "Active",
        SecurityStamp = Guid.NewGuid(),
        UserRoles = new List<UserRole>
        {
            new() { Role = new Role { Name = "Admin" } }
        }
    };

    [Fact]
    public async Task LoginAsync_ValidCredentials_ReturnsAuthResult()
    {
        var user = ActiveUser();
        _userRepository
            .Setup(r => r.GetByUsernameOrEmailAsync("admin", default))
            .ReturnsAsync(user);
        _passwordHasher
            .Setup(h => h.VerifyPassword("hashed-value", "Admin@123"))
            .Returns(true);
        _tokenGenerator
            .Setup(t => t.GenerateToken(user))
            .Returns(("fake-jwt", DateTime.UtcNow.AddHours(1)));

        var session = await Login("admin", "Admin@123");

        Assert.NotNull(session);
        Assert.Equal("fake-jwt", session!.Result.Token);
        Assert.Contains("Admin", session.Result.Roles);
        Assert.False(session.Result.MustChangePassword);
        Assert.Same(IssuedToken, session.RefreshToken); // D2: a new refresh-token family per login
        _refreshTokens.Verify(r => r.IssueAsync(user, ClientInfo.Unknown, default), Times.Once);
    }

    [Fact]
    public async Task LoginAsync_WrongPassword_ReturnsNull()
    {
        var user = ActiveUser();
        _userRepository
            .Setup(r => r.GetByUsernameOrEmailAsync("admin", default))
            .ReturnsAsync(user);
        _passwordHasher
            .Setup(h => h.VerifyPassword(It.IsAny<string>(), It.IsAny<string>()))
            .Returns(false);

        var result = await Login("admin", "WrongPassword1!");

        Assert.Null(result);
        _refreshTokens.Verify(
            r => r.IssueAsync(It.IsAny<User>(), It.IsAny<ClientInfo>(), It.IsAny<CancellationToken>()), Times.Never);
    }

    [Fact]
    public async Task LoginAsync_UserDoesNotExist_ReturnsNull()
    {
        _userRepository
            .Setup(r => r.GetByUsernameOrEmailAsync("nobody", default))
            .ReturnsAsync((User?)null);

        var result = await Login("nobody", "whatever");

        Assert.Null(result);
    }

    [Fact]
    public async Task LoginAsync_InactiveUser_ReturnsNull()
    {
        var user = ActiveUser();
        user.Status = "Suspended";
        _userRepository
            .Setup(r => r.GetByUsernameOrEmailAsync("admin", default))
            .ReturnsAsync(user);

        var result = await Login("admin", "Admin@123");

        Assert.Null(result);
        _passwordHasher.Verify(
            h => h.VerifyPassword(It.IsAny<string>(), It.IsAny<string>()), Times.Never);
    }

    [Fact]
    public async Task LoginAsync_LockedOutUser_ReturnsNull()
    {
        var user = ActiveUser();
        user.LockoutEnd = DateTime.UtcNow.AddMinutes(10); // still locked
        _userRepository
            .Setup(r => r.GetByUsernameOrEmailAsync("admin", default))
            .ReturnsAsync(user);

        var result = await Login("admin", "Admin@123");

        Assert.Null(result);
        // Password is never checked when locked out.
        _passwordHasher.Verify(
            h => h.VerifyPassword(It.IsAny<string>(), It.IsAny<string>()), Times.Never);
    }

    [Fact]
    public async Task LoginAsync_ExpiredLockout_AllowsLogin()
    {
        var user = ActiveUser();
        user.LockoutEnd = DateTime.UtcNow.AddMinutes(-1); // expired
        user.AccessFailedCount = 5;
        _userRepository
            .Setup(r => r.GetByUsernameOrEmailAsync("admin", default))
            .ReturnsAsync(user);
        _passwordHasher
            .Setup(h => h.VerifyPassword("hashed-value", "Admin@123"))
            .Returns(true);
        _tokenGenerator
            .Setup(t => t.GenerateToken(user))
            .Returns(("fresh-jwt", DateTime.UtcNow.AddHours(1)));

        var result = await Login("admin", "Admin@123");

        Assert.NotNull(result);
        Assert.Equal("fresh-jwt", result!.Result.Token);
    }

    [Fact]
    public async Task LoginAsync_WrongPassword_IncrementsAccessFailedCount()
    {
        var user = ActiveUser();
        user.AccessFailedCount = 0;
        _userRepository
            .Setup(r => r.GetByUsernameOrEmailAsync("admin", default))
            .ReturnsAsync(user);
        _passwordHasher
            .Setup(h => h.VerifyPassword(It.IsAny<string>(), It.IsAny<string>()))
            .Returns(false);

        await Login("admin", "WrongPassword1!");

        Assert.Equal(1, user.AccessFailedCount);
        _userRepository.Verify(r => r.SaveChangesAsync(default), Times.Once);
    }

    [Fact]
    public async Task LoginAsync_FifthFailedAttempt_LocksAccount()
    {
        var user = ActiveUser();
        user.AccessFailedCount = 4; // next failure will be the 5th
        _userRepository
            .Setup(r => r.GetByUsernameOrEmailAsync("admin", default))
            .ReturnsAsync(user);
        _passwordHasher
            .Setup(h => h.VerifyPassword(It.IsAny<string>(), It.IsAny<string>()))
            .Returns(false);

        await Login("admin", "WrongPassword1!");

        Assert.NotNull(user.LockoutEnd);
        Assert.True(user.LockoutEnd > DateTime.UtcNow);
        Assert.Equal(0, user.AccessFailedCount); // reset after lockout
    }

    [Fact]
    public async Task LoginAsync_SuccessfulLogin_ResetsFailedCount()
    {
        var user = ActiveUser();
        user.AccessFailedCount = 3;
        _userRepository
            .Setup(r => r.GetByUsernameOrEmailAsync("admin", default))
            .ReturnsAsync(user);
        _passwordHasher
            .Setup(h => h.VerifyPassword("hashed-value", "Admin@123"))
            .Returns(true);
        _tokenGenerator
            .Setup(t => t.GenerateToken(user))
            .Returns(("jwt", DateTime.UtcNow.AddHours(1)));

        await Login("admin", "Admin@123");

        Assert.Equal(0, user.AccessFailedCount);
        Assert.Null(user.LockoutEnd);
    }

    [Fact]
    public async Task LoginAsync_MustChangePasswordUser_ReturnsTrueInResult()
    {
        var user = ActiveUser();
        user.MustChangePassword = true;
        _userRepository
            .Setup(r => r.GetByUsernameOrEmailAsync("admin", default))
            .ReturnsAsync(user);
        _passwordHasher
            .Setup(h => h.VerifyPassword("hashed-value", "Admin@123"))
            .Returns(true);
        _tokenGenerator
            .Setup(t => t.GenerateToken(user))
            .Returns(("jwt", DateTime.UtcNow.AddHours(1)));

        var result = await Login("admin", "Admin@123");

        Assert.NotNull(result);
        Assert.True(result!.Result.MustChangePassword);
    }
    // ─────────────────────────────────────────────────────────────────
    // D2: Refresh
    // ─────────────────────────────────────────────────────────────────

    private static RefreshToken ActiveTokenFor(User user) => new()
    {
        Id = 7,
        UserId = user.Id,
        FamilyId = Guid.NewGuid(),
        SecurityStamp = user.SecurityStamp,
        ExpiresAt = DateTime.UtcNow.AddDays(7),
        FamilyExpiresAt = DateTime.UtcNow.AddDays(30),
    };

    private void SetupLookup(RefreshTokenState state, RefreshToken? token) =>
        _refreshTokens
            .Setup(r => r.LookupAsync("cookie", default))
            .ReturnsAsync(new RefreshTokenLookup(state, token));

    [Theory]
    [InlineData(RefreshTokenState.NotFound)]
    [InlineData(RefreshTokenState.Expired)]
    [InlineData(RefreshTokenState.Revoked)]
    [InlineData(RefreshTokenState.Reused)]
    public async Task RefreshAsync_TokenNotActive_ReturnsNull_WithoutRotating(RefreshTokenState state)
    {
        SetupLookup(state, state == RefreshTokenState.NotFound ? null : ActiveTokenFor(ActiveUser()));

        var session = await _sut.RefreshAsync("cookie", ClientInfo.Unknown, default);

        Assert.Null(session);
        _refreshTokens.Verify(r => r.RotateAsync(
            It.IsAny<RefreshToken>(), It.IsAny<User>(), It.IsAny<ClientInfo>(), It.IsAny<CancellationToken>()), Times.Never);
    }

    [Fact]
    public async Task RefreshAsync_ValidToken_RotatesAndReturnsNewAccessToken()
    {
        var user = ActiveUser();
        var token = ActiveTokenFor(user);
        var rotated = new IssuedRefreshToken("rotated", DateTime.UtcNow.AddDays(7));
        SetupLookup(RefreshTokenState.Active, token);
        _userRepository.Setup(r => r.GetByIdAsync(user.Id, default)).ReturnsAsync(user);
        _refreshTokens.Setup(r => r.RotateAsync(token, user, ClientInfo.Unknown, default)).ReturnsAsync(rotated);
        _tokenGenerator.Setup(t => t.GenerateToken(user)).Returns(("new-jwt", DateTime.UtcNow.AddMinutes(15)));

        var session = await _sut.RefreshAsync("cookie", ClientInfo.Unknown, default);

        Assert.NotNull(session);
        Assert.Equal("new-jwt", session!.Result.Token);
        Assert.Same(rotated, session.RefreshToken);
    }

    [Fact]
    public async Task RefreshAsync_StampChangedSinceIssue_RevokesFamilyAndReturnsNull()
    {
        var user = ActiveUser();
        var token = ActiveTokenFor(user);
        user.SecurityStamp = Guid.NewGuid(); // e.g. password changed elsewhere
        SetupLookup(RefreshTokenState.Active, token);
        _userRepository.Setup(r => r.GetByIdAsync(user.Id, default)).ReturnsAsync(user);

        var session = await _sut.RefreshAsync("cookie", ClientInfo.Unknown, default);

        Assert.Null(session);
        _refreshTokens.Verify(r => r.RevokeFamilyAsync(
            token.FamilyId, RefreshTokenRevokeReasons.SessionInvalidated, default), Times.Once);
    }

    [Fact]
    public async Task RefreshAsync_InactiveUser_RevokesFamilyAndReturnsNull()
    {
        var user = ActiveUser();
        user.Status = "Inactive";
        var token = ActiveTokenFor(user);
        SetupLookup(RefreshTokenState.Active, token);
        _userRepository.Setup(r => r.GetByIdAsync(user.Id, default)).ReturnsAsync(user);

        var session = await _sut.RefreshAsync("cookie", ClientInfo.Unknown, default);

        Assert.Null(session);
        _refreshTokens.Verify(r => r.RevokeFamilyAsync(
            token.FamilyId, RefreshTokenRevokeReasons.SessionInvalidated, default), Times.Once);
    }

    [Fact]
    public async Task RefreshAsync_LockedOutUser_IsStillAllowed()
    {
        // Lockout stops password guessing; it does not end sessions that already proved the password.
        var user = ActiveUser();
        user.LockoutEnd = DateTime.UtcNow.AddMinutes(10);
        var token = ActiveTokenFor(user);
        SetupLookup(RefreshTokenState.Active, token);
        _userRepository.Setup(r => r.GetByIdAsync(user.Id, default)).ReturnsAsync(user);
        _refreshTokens
            .Setup(r => r.RotateAsync(token, user, ClientInfo.Unknown, default))
            .ReturnsAsync(new IssuedRefreshToken("rotated", DateTime.UtcNow.AddDays(7)));
        _tokenGenerator.Setup(t => t.GenerateToken(user)).Returns(("jwt", DateTime.UtcNow.AddMinutes(15)));

        var session = await _sut.RefreshAsync("cookie", ClientInfo.Unknown, default);

        Assert.NotNull(session);
    }

    [Fact]
    public async Task RefreshAsync_LostRotationRace_ReturnsNull()
    {
        var user = ActiveUser();
        var token = ActiveTokenFor(user);
        SetupLookup(RefreshTokenState.Active, token);
        _userRepository.Setup(r => r.GetByIdAsync(user.Id, default)).ReturnsAsync(user);
        _refreshTokens
            .Setup(r => r.RotateAsync(token, user, ClientInfo.Unknown, default))
            .ReturnsAsync((IssuedRefreshToken?)null);

        var session = await _sut.RefreshAsync("cookie", ClientInfo.Unknown, default);

        Assert.Null(session);
        _tokenGenerator.Verify(t => t.GenerateToken(It.IsAny<User>()), Times.Never);
    }

    // ─────────────────────────────────────────────────────────────────
    // D2: Logout, logout-all, change password
    // ─────────────────────────────────────────────────────────────────

    [Fact]
    public async Task LogoutAsync_ActiveToken_RevokesItsFamily()
    {
        var token = ActiveTokenFor(ActiveUser());
        SetupLookup(RefreshTokenState.Active, token);

        await _sut.LogoutAsync("cookie", default);

        _refreshTokens.Verify(r => r.RevokeFamilyAsync(
            token.FamilyId, RefreshTokenRevokeReasons.Logout, default), Times.Once);
    }

    [Fact]
    public async Task LogoutAsync_NoCookie_DoesNothing()
    {
        _refreshTokens
            .Setup(r => r.LookupAsync(null, default))
            .ReturnsAsync(RefreshTokenLookup.NotFound);

        await _sut.LogoutAsync(null, default);

        _refreshTokens.Verify(r => r.RevokeFamilyAsync(
            It.IsAny<Guid>(), It.IsAny<string>(), It.IsAny<CancellationToken>()), Times.Never);
    }

    [Fact]
    public async Task LogoutAllAsync_RotatesStamp_InvalidatesCache_AndRevokesEverySession()
    {
        var user = ActiveUser();
        var oldStamp = user.SecurityStamp;
        _userRepository.Setup(r => r.GetByIdAsync(user.Id, default)).ReturnsAsync(user);

        await _sut.LogoutAllAsync(user.Id, default);

        Assert.NotEqual(oldStamp, user.SecurityStamp);
        _userRepository.Verify(r => r.SaveChangesAsync(default), Times.Once);
        _permissionCache.Verify(c => c.InvalidateUser(user.Id), Times.Once);
        _refreshTokens.Verify(r => r.RevokeAllForUserAsync(
            user.Id, RefreshTokenRevokeReasons.LogoutAll, default), Times.Once);
    }

    [Fact]
    public async Task ChangePasswordAsync_RevokesAllSessions_ThenIssuesANewOneWithTheNewStamp()
    {
        var user = ActiveUser();
        var oldStamp = user.SecurityStamp;
        _userRepository.Setup(r => r.GetByIdAsync(user.Id, default)).ReturnsAsync(user);
        _passwordHasher.Setup(h => h.VerifyPassword("hashed-value", "Old@1234")).Returns(true);
        _passwordHasher.Setup(h => h.HashPassword("New@5678")).Returns("new-hash");
        _tokenGenerator.Setup(t => t.GenerateToken(user)).Returns(("jwt", DateTime.UtcNow.AddMinutes(15)));

        Guid? stampWhenIssued = null;
        _refreshTokens
            .Setup(r => r.IssueAsync(user, ClientInfo.Unknown, default))
            .Callback<User, ClientInfo, CancellationToken>((u, _, _) => stampWhenIssued = u.SecurityStamp)
            .ReturnsAsync(IssuedToken);

        var session = await _sut.ChangePasswordAsync(
            user.Id, new ChangePasswordRequest("Old@1234", "New@5678"), ClientInfo.Unknown, default);

        Assert.NotEqual(oldStamp, user.SecurityStamp);
        Assert.Equal(user.SecurityStamp, stampWhenIssued);
        Assert.Same(IssuedToken, session.RefreshToken);
        _refreshTokens.Verify(r => r.RevokeAllForUserAsync(
            user.Id, RefreshTokenRevokeReasons.PasswordChanged, default), Times.Once);
    }

    [Fact]
    public async Task ChangePasswordAsync_WrongCurrentPassword_DoesNotTouchSessions()
    {
        var user = ActiveUser();
        _userRepository.Setup(r => r.GetByIdAsync(user.Id, default)).ReturnsAsync(user);
        _passwordHasher.Setup(h => h.VerifyPassword(It.IsAny<string>(), It.IsAny<string>())).Returns(false);

        await Assert.ThrowsAsync<BusinessRuleException>(() => _sut.ChangePasswordAsync(
            user.Id, new ChangePasswordRequest("Wrong@1234", "New@5678"), ClientInfo.Unknown, default));

        _refreshTokens.Verify(r => r.RevokeAllForUserAsync(
            It.IsAny<int>(), It.IsAny<string>(), It.IsAny<CancellationToken>()), Times.Never);
    }
}
