// File: backend/SchoolManagement.Tests/IntegrationTests/RefreshTokenTests.cs
//
// D2 refresh-token integration tests: the real pipeline (cookie, CSRF filter, controller,
// service, repository) against the real test database.
//
// Cookies are handled by hand (HandleCookies = false) so every test controls exactly which
// refresh token it sends. That is the only way to replay an old one on purpose.
// Every test creates its own user, so no test's sessions interfere with another's.

using System.Net;
using System.Net.Http.Headers;
using System.Net.Http.Json;
using System.Text.Json;
using Microsoft.AspNetCore.Mvc.Testing;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Options;
using SchoolManagement.API.Configuration;
using SchoolManagement.Application.DTOs.Auth;
using SchoolManagement.Application.Interfaces;
using SchoolManagement.Application.Services;
using SchoolManagement.Domain.Entities;
using SchoolManagement.Infrastructure.Persistence;
using Xunit;

namespace SchoolManagement.Tests.IntegrationTests;

[Collection("LegacyApi")]
public class RefreshTokenTests
{
    private const string Password = "Test@1234";
    private const string CsrfHeader = "X-Requested-With";
    private const string CsrfValue = "eschool";
    private const int ClerkRoleId = 3;

    private readonly CustomWebApplicationFactory _factory;
    private readonly HttpClient _client;
    private readonly string _cookieName;

    public RefreshTokenTests(CustomWebApplicationFactory factory)
    {
        _factory = factory;

        // https: the cookie is Secure. HandleCookies = false: we choose the cookie per request.
        _client = factory.CreateClient(new WebApplicationFactoryClientOptions
        {
            BaseAddress = new Uri("https://localhost"),
            HandleCookies = false,
        });

        _cookieName = factory.Services.GetRequiredService<IOptions<RefreshTokenCookieOptions>>().Value.Name;
    }

    // ─────────────────────────────────────────────────────────────────
    // Login
    // ─────────────────────────────────────────────────────────────────

    [Fact]
    public async Task Login_SetsSecureHttpOnlyStrictCookie_AndKeepsRefreshTokenOutOfBody()
    {
        var (username, _) = await CreateUserAsync();

        var response = await _client.PostAsJsonAsync("/api/auth/login", new LoginRequest(username, Password));

        Assert.Equal(HttpStatusCode.OK, response.StatusCode);
        var setCookie = FindRefreshSetCookie(response);
        Assert.NotNull(setCookie);

        var attributes = setCookie!.ToLowerInvariant();
        Assert.Contains("httponly", attributes);
        Assert.Contains("secure", attributes);
        Assert.Contains("samesite=strict", attributes);
        Assert.Contains("path=/api/auth", attributes);
        Assert.Contains("expires=", attributes); // persistent cookie

        var refreshToken = ReadRefreshCookie(response)!;
        var body = await response.Content.ReadAsStringAsync();
        Assert.DoesNotContain(refreshToken, body);
    }

    [Fact]
    public async Task Login_StoresOnlyTheHash_WithSevenAndThirtyDayExpiries()
    {
        var (username, userId) = await CreateUserAsync();

        var session = await LoginAsync(username);

        var row = await QueryAsync(db => db.RefreshTokens.SingleAsync(t => t.UserId == userId));
        Assert.Equal(RefreshToken.TokenHashLength, row.TokenHash.Length);
        Assert.Equal(RefreshTokenService.HashToken(session.RefreshToken), row.TokenHash);
        Assert.Null(row.RevokedAt);
        Assert.InRange((row.ExpiresAt - row.IssuedAt).TotalDays, 6.99, 7.01);
        Assert.InRange((row.FamilyExpiresAt - row.IssuedAt).TotalDays, 29.99, 30.01);
    }

    // ─────────────────────────────────────────────────────────────────
    // Refresh: happy path and rotation
    // ─────────────────────────────────────────────────────────────────

    [Fact]
    public async Task Refresh_ReturnsNewAccessToken_AndRotatesTheCookie()
    {
        var (username, userId) = await CreateUserAsync();
        var session = await LoginAsync(username);

        var response = await RefreshAsync(session.RefreshToken);

        Assert.Equal(HttpStatusCode.OK, response.StatusCode);
        var result = await response.Content.ReadFromJsonAsync<AuthResult>();
        var newRefreshToken = ReadRefreshCookie(response);
        Assert.False(string.IsNullOrEmpty(newRefreshToken));
        Assert.NotEqual(session.RefreshToken, newRefreshToken);

        // The new access token works.
        var me = await SendAsync(HttpMethod.Get, "/api/auth/me", accessToken: result!.Token);
        Assert.Equal(HttpStatusCode.OK, me.StatusCode);

        // Same family; the old row points at its replacement.
        var rows = await QueryAsync(db => db.RefreshTokens.Where(t => t.UserId == userId).OrderBy(t => t.Id).ToListAsync());
        Assert.Equal(2, rows.Count);
        Assert.Equal(rows[0].FamilyId, rows[1].FamilyId);
        Assert.Equal(RefreshTokenRevokeReasons.Rotated, rows[0].RevokedReason);
        Assert.Equal(rows[1].Id, rows[0].ReplacedByTokenId);
        Assert.Null(rows[1].RevokedAt);
        Assert.Equal(rows[0].FamilyExpiresAt, rows[1].FamilyExpiresAt); // absolute expiry carried forward
    }

    [Fact]
    public async Task Refresh_ReplayingARotatedToken_Returns401_AndRevokesTheWholeFamily()
    {
        var (username, userId) = await CreateUserAsync();
        var session = await LoginAsync(username);

        var first = await RefreshAsync(session.RefreshToken);
        Assert.Equal(HttpStatusCode.OK, first.StatusCode);
        var current = ReadRefreshCookie(first)!;

        // The attacker replays the old token...
        var replay = await RefreshAsync(session.RefreshToken);
        Assert.Equal(HttpStatusCode.Unauthorized, replay.StatusCode);
        Assert.Equal("REFRESH_TOKEN_INVALID", await ReadErrorCodeAsync(replay));

        // ...and the legitimate user's current token dies with it.
        var legit = await RefreshAsync(current);
        Assert.Equal(HttpStatusCode.Unauthorized, legit.StatusCode);

        var rows = await QueryAsync(db => db.RefreshTokens.Where(t => t.UserId == userId).ToListAsync());
        Assert.All(rows, r => Assert.NotNull(r.RevokedAt));
        Assert.Contains(rows, r => r.RevokedReason == RefreshTokenRevokeReasons.ReuseDetected);
    }

    [Fact]
    public async Task Reuse_InOneSession_DoesNotEndTheUsersOtherSessions()
    {
        var (username, _) = await CreateUserAsync();
        var laptop = await LoginAsync(username);
        var phone = await LoginAsync(username);

        var rotated = await RefreshAsync(laptop.RefreshToken);
        Assert.Equal(HttpStatusCode.OK, rotated.StatusCode);
        Assert.Equal(HttpStatusCode.Unauthorized, (await RefreshAsync(laptop.RefreshToken)).StatusCode); // reuse

        var phoneRefresh = await RefreshAsync(phone.RefreshToken);
        Assert.Equal(HttpStatusCode.OK, phoneRefresh.StatusCode);
    }

    [Fact]
    public async Task Refresh_TwoConcurrentRequestsWithTheSameToken_ExactlyOneWins_ThenFamilyIsRevoked()
    {
        var (username, _) = await CreateUserAsync();
        var session = await LoginAsync(username);

        var responses = await Task.WhenAll(
            RefreshAsync(session.RefreshToken),
            RefreshAsync(session.RefreshToken));

        Assert.Single(responses, r => r.StatusCode == HttpStatusCode.OK);
        Assert.Single(responses, r => r.StatusCode == HttpStatusCode.Unauthorized);

        // Strict reuse detection: the loser's attempt revoked the family, including the winner's new token.
        var winnerToken = ReadRefreshCookie(responses.Single(r => r.StatusCode == HttpStatusCode.OK))!;
        Assert.Equal(HttpStatusCode.Unauthorized, (await RefreshAsync(winnerToken)).StatusCode);
    }

    // ─────────────────────────────────────────────────────────────────
    // Refresh: rejected requests
    // ─────────────────────────────────────────────────────────────────

    [Fact]
    public async Task Refresh_WithoutCsrfHeader_Returns403_AndDoesNotConsumeTheToken()
    {
        var (username, _) = await CreateUserAsync();
        var session = await LoginAsync(username);

        var blocked = await RefreshAsync(session.RefreshToken, withCsrfHeader: false);
        Assert.Equal(HttpStatusCode.Forbidden, blocked.StatusCode);
        Assert.Equal("CSRF_CHECK_FAILED", await ReadErrorCodeAsync(blocked));

        // The filter ran before the controller, so the token is still good.
        Assert.Equal(HttpStatusCode.OK, (await RefreshAsync(session.RefreshToken)).StatusCode);
    }

    [Fact]
    public async Task Refresh_WithoutCookie_Returns401_WithErrorCode()
    {
        var response = await RefreshAsync(refreshToken: null);

        Assert.Equal(HttpStatusCode.Unauthorized, response.StatusCode);
        Assert.Equal("REFRESH_TOKEN_INVALID", await ReadErrorCodeAsync(response));
    }

    [Fact]
    public async Task Refresh_WithUnknownToken_Returns401_AndClearsTheCookie()
    {
        var response = await RefreshAsync("not-a-real-token");

        Assert.Equal(HttpStatusCode.Unauthorized, response.StatusCode);
        Assert.Equal(string.Empty, ReadRefreshCookie(response)); // deletion: empty value, expired date
    }

    [Fact]
    public async Task Refresh_AfterIdleExpiry_Returns401()
    {
        var (username, userId) = await CreateUserAsync();
        var session = await LoginAsync(username);

        // Pretend the token was issued 8 days ago and its 7-day idle window has passed.
        var now = DateTime.UtcNow;
        await ExecuteAsync(db => db.RefreshTokens
            .Where(t => t.UserId == userId)
            .ExecuteUpdateAsync(s => s
                .SetProperty(t => t.IssuedAt, now.AddDays(-8))
                .SetProperty(t => t.ExpiresAt, now.AddDays(-1))));

        Assert.Equal(HttpStatusCode.Unauthorized, (await RefreshAsync(session.RefreshToken)).StatusCode);
    }

    [Fact]
    public async Task Refresh_ForADeactivatedUser_Returns401_AndInvalidatesTheSession()
    {
        var (username, userId) = await CreateUserAsync();
        var session = await LoginAsync(username);

        await ExecuteAsync(db => db.Users
            .Where(u => u.Id == userId)
            .ExecuteUpdateAsync(s => s.SetProperty(u => u.Status, "Inactive")));
        InvalidateCache(userId);

        Assert.Equal(HttpStatusCode.Unauthorized, (await RefreshAsync(session.RefreshToken)).StatusCode);

        var row = await QueryAsync(db => db.RefreshTokens.SingleAsync(t => t.UserId == userId));
        Assert.Equal(RefreshTokenRevokeReasons.SessionInvalidated, row.RevokedReason);
    }

    // ─────────────────────────────────────────────────────────────────
    // Password change, logout, logout-all
    // ─────────────────────────────────────────────────────────────────

    [Fact]
    public async Task ChangePassword_EndsEveryOtherSession_AndGivesThisDeviceANewOne()
    {
        var (username, _) = await CreateUserAsync();
        var thisDevice = await LoginAsync(username);
        var otherDevice = await LoginAsync(username);

        var change = await SendAsync(HttpMethod.Post, "/api/auth/change-password",
            accessToken: thisDevice.AccessToken,
            body: new ChangePasswordRequest(Password, "NewPass@456"));
        Assert.Equal(HttpStatusCode.OK, change.StatusCode);
        var newRefreshToken = ReadRefreshCookie(change);
        Assert.False(string.IsNullOrEmpty(newRefreshToken));

        Assert.Equal(HttpStatusCode.Unauthorized, (await RefreshAsync(otherDevice.RefreshToken)).StatusCode);
        Assert.Equal(HttpStatusCode.Unauthorized, (await RefreshAsync(thisDevice.RefreshToken)).StatusCode);
        Assert.Equal(HttpStatusCode.OK, (await RefreshAsync(newRefreshToken)).StatusCode);
    }

    [Fact]
    public async Task Logout_EndsTheSession_ClearsTheCookie_AndIsIdempotent()
    {
        var (username, userId) = await CreateUserAsync();
        var session = await LoginAsync(username);

        var logout = await PostWithCookieAsync("/api/auth/logout", session.RefreshToken);
        Assert.Equal(HttpStatusCode.NoContent, logout.StatusCode);
        Assert.Equal(string.Empty, ReadRefreshCookie(logout));

        Assert.Equal(HttpStatusCode.Unauthorized, (await RefreshAsync(session.RefreshToken)).StatusCode);

        var row = await QueryAsync(db => db.RefreshTokens.SingleAsync(t => t.UserId == userId));
        Assert.Equal(RefreshTokenRevokeReasons.Logout, row.RevokedReason);

        // Again with the dead cookie, and with no cookie at all: still 204.
        Assert.Equal(HttpStatusCode.NoContent, (await PostWithCookieAsync("/api/auth/logout", session.RefreshToken)).StatusCode);
        Assert.Equal(HttpStatusCode.NoContent, (await PostWithCookieAsync("/api/auth/logout", null)).StatusCode);
    }

    [Fact]
    public async Task Logout_WithoutCsrfHeader_Returns403()
    {
        var (username, _) = await CreateUserAsync();
        var session = await LoginAsync(username);

        var response = await PostWithCookieAsync("/api/auth/logout", session.RefreshToken, withCsrfHeader: false);

        Assert.Equal(HttpStatusCode.Forbidden, response.StatusCode);
    }

    [Fact]
    public async Task LogoutAll_EndsEverySession_AndAccessTokensStopWorkingImmediately()
    {
        var (username, _) = await CreateUserAsync();
        var laptop = await LoginAsync(username);
        var phone = await LoginAsync(username);

        var response = await SendAsync(HttpMethod.Post, "/api/auth/logout-all", accessToken: laptop.AccessToken);
        Assert.Equal(HttpStatusCode.NoContent, response.StatusCode);

        Assert.Equal(HttpStatusCode.Unauthorized, (await RefreshAsync(laptop.RefreshToken)).StatusCode);
        Assert.Equal(HttpStatusCode.Unauthorized, (await RefreshAsync(phone.RefreshToken)).StatusCode);

        // The security stamp was rotated: access tokens die now, not in 15 minutes.
        Assert.Equal(HttpStatusCode.Unauthorized,
            (await SendAsync(HttpMethod.Get, "/api/auth/me", accessToken: phone.AccessToken)).StatusCode);
    }

    [Fact]
    public async Task MustChangePasswordUser_CanStillRefresh_ButOtherEndpointsStayBlocked()
    {
        var (username, _) = await CreateUserAsync(mustChangePassword: true);
        var session = await LoginAsync(username);

        // Sent WITH the (still valid) access token, so the MustChangePassword middleware runs
        // and must let /refresh through.
        var request = new HttpRequestMessage(HttpMethod.Post, "/api/auth/refresh");
        request.Headers.Authorization = new AuthenticationHeaderValue("Bearer", session.AccessToken);
        request.Headers.TryAddWithoutValidation("Cookie", $"{_cookieName}={session.RefreshToken}");
        request.Headers.Add(CsrfHeader, CsrfValue);
        var refresh = await _client.SendAsync(request);

        Assert.Equal(HttpStatusCode.OK, refresh.StatusCode);
        var result = await refresh.Content.ReadFromJsonAsync<AuthResult>();
        Assert.True(result!.MustChangePassword);

        var subjects = await SendAsync(HttpMethod.Get, "/api/subjects", accessToken: result.Token);
        Assert.Equal(HttpStatusCode.Forbidden, subjects.StatusCode);
    }

    // ─────────────────────────────────────────────────────────────────
    // Helpers
    // ─────────────────────────────────────────────────────────────────

    private sealed record Session(string AccessToken, string RefreshToken);

    private async Task<(string Username, int UserId)> CreateUserAsync(bool mustChangePassword = false)
    {
        var username = $"rt_{Guid.NewGuid():N}";
        using var scope = _factory.Services.CreateScope();
        var db = scope.ServiceProvider.GetRequiredService<ApplicationDbContext>();
        var hasher = scope.ServiceProvider.GetRequiredService<IPasswordHasher>();

        var user = new User
        {
            Username = username,
            Email = $"{username}@test.local",
            PasswordHash = hasher.HashPassword(Password),
            Status = "Active",
            SecurityStamp = Guid.NewGuid(),
            MustChangePassword = mustChangePassword,
        };
        user.UserRoles.Add(new UserRole { RoleId = ClerkRoleId, AssignedAt = DateTime.UtcNow });
        db.Users.Add(user);
        await db.SaveChangesAsync();

        return (username, user.Id);
    }

    private async Task<Session> LoginAsync(string username)
    {
        var response = await _client.PostAsJsonAsync("/api/auth/login", new LoginRequest(username, Password));
        Assert.Equal(HttpStatusCode.OK, response.StatusCode);

        var result = await response.Content.ReadFromJsonAsync<AuthResult>();
        return new Session(result!.Token, ReadRefreshCookie(response)!);
    }

    private Task<HttpResponseMessage> RefreshAsync(string? refreshToken, bool withCsrfHeader = true) =>
        PostWithCookieAsync("/api/auth/refresh", refreshToken, withCsrfHeader);

    private Task<HttpResponseMessage> PostWithCookieAsync(string url, string? refreshToken, bool withCsrfHeader = true)
    {
        var request = new HttpRequestMessage(HttpMethod.Post, url);
        if (refreshToken is not null)
            request.Headers.TryAddWithoutValidation("Cookie", $"{_cookieName}={refreshToken}");
        if (withCsrfHeader)
            request.Headers.Add(CsrfHeader, CsrfValue);
        return _client.SendAsync(request);
    }

    private Task<HttpResponseMessage> SendAsync(HttpMethod method, string url, string accessToken, object? body = null)
    {
        var request = new HttpRequestMessage(method, url);
        request.Headers.Authorization = new AuthenticationHeaderValue("Bearer", accessToken);
        if (body is not null)
            request.Content = JsonContent.Create(body);
        return _client.SendAsync(request);
    }

    private string? FindRefreshSetCookie(HttpResponseMessage response) =>
        response.Headers.TryGetValues("Set-Cookie", out var values)
            ? values.FirstOrDefault(v => v.StartsWith(_cookieName + "=", StringComparison.Ordinal))
            : null;

    /// <summary>The refresh cookie's value from Set-Cookie; "" when the response deletes it; null when absent.</summary>
    private string? ReadRefreshCookie(HttpResponseMessage response)
    {
        var header = FindRefreshSetCookie(response);
        if (header is null) return null;

        var nameValue = header.Split(';')[0];
        return nameValue[(_cookieName.Length + 1)..];
    }

    private static async Task<string?> ReadErrorCodeAsync(HttpResponseMessage response)
    {
        using var json = JsonDocument.Parse(await response.Content.ReadAsStringAsync());
        return json.RootElement.TryGetProperty("errorCode", out var code) ? code.GetString() : null;
    }

    private async Task<T> QueryAsync<T>(Func<ApplicationDbContext, Task<T>> query)
    {
        using var scope = _factory.Services.CreateScope();
        return await query(scope.ServiceProvider.GetRequiredService<ApplicationDbContext>());
    }

    private async Task ExecuteAsync(Func<ApplicationDbContext, Task<int>> command)
    {
        using var scope = _factory.Services.CreateScope();
        await command(scope.ServiceProvider.GetRequiredService<ApplicationDbContext>());
    }

    private void InvalidateCache(int userId) =>
        _factory.Services.GetRequiredService<IPermissionCacheService>().InvalidateUser(userId);
}
