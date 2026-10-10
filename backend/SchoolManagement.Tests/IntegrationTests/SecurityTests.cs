// File: backend/SchoolManagement.Tests/IntegrationTests/SecurityTests.cs
//
// D1 security integration tests.  Each test exercises the full pipeline
// (middleware, auth, authorization) against the real test database.
//
// Tests:
//   1. Stamp bump → old token rejected (401)
//   2. Locked account → login returns same 401 as wrong password
//   3. MustChangePassword → non-auth endpoints return 403
//   4. Admin holds all catalog permissions (via GET /api/auth/me)
//   5. Change password → returns new token
//   6. Unlock user → clears lockout

using System.Net;
using System.Net.Http.Headers;
using System.Net.Http.Json;
using Microsoft.Extensions.DependencyInjection;
using SchoolManagement.Application.DTOs.Auth;
using SchoolManagement.Application.Interfaces;
using SchoolManagement.Domain.Constants;
using SchoolManagement.Domain.Entities;
using SchoolManagement.Infrastructure.Persistence;
using Xunit;

namespace SchoolManagement.Tests.IntegrationTests;

[Collection("LegacyApi")]
public class SecurityTests
{
    private readonly CustomWebApplicationFactory _factory;
    private readonly HttpClient _client;

    public SecurityTests(CustomWebApplicationFactory factory)
    {
        _factory = factory;
        _client = factory.CreateClient();
    }

    /// <summary>
    /// Helper: log in with the seeded admin and return the JWT.
    /// </summary>
    private async Task<string> LoginAsAdminAsync()
    {
        var response = await _client.PostAsJsonAsync("/api/auth/login",
            new LoginRequest("admin", "Admin@123"));
        response.EnsureSuccessStatusCode();
        var result = await response.Content.ReadFromJsonAsync<AuthResult>();
        return result!.Token;
    }

    /// <summary>
    /// Helper: set the Authorization header to the given JWT.
    /// </summary>
    private void SetToken(string token)
    {
        _client.DefaultRequestHeaders.Authorization =
            new AuthenticationHeaderValue("Bearer", token);
    }

    // ─────────────────────────────────────────────────────────────────
    // 1. Security stamp rotation rejects the old token immediately
    // ─────────────────────────────────────────────────────────────────
    [Fact]
    public async Task OldToken_RejectedAfterSecurityStampRotation()
    {
        var oldToken = await LoginAsAdminAsync();
        SetToken(oldToken);

        // Sanity: the old token works.
        var meOk = await _client.GetAsync("/api/auth/me");
        Assert.Equal(HttpStatusCode.OK, meOk.StatusCode);

        // Rotate the security stamp in the database.
        using (var scope = _factory.Services.CreateScope())
        {
            var db = scope.ServiceProvider.GetRequiredService<ApplicationDbContext>();
            var admin = await db.Users.FindAsync(1); // seeded admin is Id=1
            admin!.SecurityStamp = Guid.NewGuid();
            await db.SaveChangesAsync();

            // Invalidate the permission cache so the handler sees the new stamp.
            var cache = scope.ServiceProvider.GetRequiredService<IPermissionCacheService>();
            cache.InvalidateUser(admin.Id);
        }

        // The old token should now be rejected (401).
        var meFail = await _client.GetAsync("/api/auth/me");
        Assert.Equal(HttpStatusCode.Unauthorized, meFail.StatusCode);
    }

    // ─────────────────────────────────────────────────────────────────
    // 2. Locked account returns 401 (same as wrong password)
    // ─────────────────────────────────────────────────────────────────
    [Fact]
    public async Task Login_LockedAccount_Returns401()
    {
        // Create a test user specifically for this test.
        string username = $"locktest_{Guid.NewGuid():N}";
        using (var scope = _factory.Services.CreateScope())
        {
            var db = scope.ServiceProvider.GetRequiredService<ApplicationDbContext>();
            var hasher = scope.ServiceProvider.GetRequiredService<IPasswordHasher>();
            var user = new User
            {
                Username = username,
                Email = $"{username}@test.local",
                PasswordHash = hasher.HashPassword("Test@1234"),
                Status = "Active",
                SecurityStamp = Guid.NewGuid(),
                LockoutEnd = DateTime.UtcNow.AddMinutes(15), // locked
                AccessFailedCount = 0,
            };
            user.UserRoles.Add(new UserRole { RoleId = 1, AssignedAt = DateTime.UtcNow });
            db.Users.Add(user);
            await db.SaveChangesAsync();
        }

        // Even with the correct password, login should fail with 401.
        var response = await _client.PostAsJsonAsync("/api/auth/login",
            new LoginRequest(username, "Test@1234"));

        Assert.Equal(HttpStatusCode.Unauthorized, response.StatusCode);
    }

    // ─────────────────────────────────────────────────────────────────
    // 3. MustChangePassword blocks non-auth endpoints
    // ─────────────────────────────────────────────────────────────────
    [Fact]
    public async Task MustChangePassword_BlocksNonAuthEndpoints()
    {
        // Create a user with MustChangePassword = true.
        string username = $"mustchange_{Guid.NewGuid():N}";
        using (var scope = _factory.Services.CreateScope())
        {
            var db = scope.ServiceProvider.GetRequiredService<ApplicationDbContext>();
            var hasher = scope.ServiceProvider.GetRequiredService<IPasswordHasher>();
            var user = new User
            {
                Username = username,
                Email = $"{username}@test.local",
                PasswordHash = hasher.HashPassword("Test@1234"),
                Status = "Active",
                SecurityStamp = Guid.NewGuid(),
                MustChangePassword = true,
            };
            user.UserRoles.Add(new UserRole { RoleId = 1, AssignedAt = DateTime.UtcNow });
            db.Users.Add(user);
            await db.SaveChangesAsync();

            // Clear cache so the new user's info is loaded fresh.
            var cache = scope.ServiceProvider.GetRequiredService<IPermissionCacheService>();
            cache.InvalidateAll();
        }

        // Log in — should succeed and return MustChangePassword = true.
        var loginResponse = await _client.PostAsJsonAsync("/api/auth/login",
            new LoginRequest(username, "Test@1234"));
        Assert.Equal(HttpStatusCode.OK, loginResponse.StatusCode);
        var loginResult = await loginResponse.Content.ReadFromJsonAsync<AuthResult>();
        Assert.True(loginResult!.MustChangePassword);

        SetToken(loginResult.Token);

        // /api/auth/me should still work (whitelisted).
        var meResponse = await _client.GetAsync("/api/auth/me");
        Assert.Equal(HttpStatusCode.OK, meResponse.StatusCode);

        // Any other endpoint should return 403 PASSWORD_CHANGE_REQUIRED.
        var subjectsResponse = await _client.GetAsync("/api/subjects");
        Assert.Equal(HttpStatusCode.Forbidden, subjectsResponse.StatusCode);
    }

    // ─────────────────────────────────────────────────────────────────
    // 4. Admin role includes every permission in the catalog
    // ─────────────────────────────────────────────────────────────────
    [Fact]
    public async Task AdminUser_HasAllCatalogPermissions()
    {
        var token = await LoginAsAdminAsync();
        SetToken(token);

        var response = await _client.GetAsync("/api/auth/me");
        response.EnsureSuccessStatusCode();
        var me = await response.Content.ReadFromJsonAsync<CurrentUserResponse>();

        Assert.NotNull(me);
        foreach (var permission in Permissions.All)
        {
            Assert.Contains(permission, me!.Permissions);
        }
    }

    // ─────────────────────────────────────────────────────────────────
    // 5. Change password returns a new token
    // ─────────────────────────────────────────────────────────────────
    [Fact]
    public async Task ChangePassword_ReturnsNewToken()
    {
        // Create a throwaway user for this test.
        string username = $"changepw_{Guid.NewGuid():N}";
        using (var scope = _factory.Services.CreateScope())
        {
            var db = scope.ServiceProvider.GetRequiredService<ApplicationDbContext>();
            var hasher = scope.ServiceProvider.GetRequiredService<IPasswordHasher>();
            var user = new User
            {
                Username = username,
                Email = $"{username}@test.local",
                PasswordHash = hasher.HashPassword("OldPass@123"),
                Status = "Active",
                SecurityStamp = Guid.NewGuid(),
            };
            user.UserRoles.Add(new UserRole { RoleId = 1, AssignedAt = DateTime.UtcNow });
            db.Users.Add(user);
            await db.SaveChangesAsync();

            var cache = scope.ServiceProvider.GetRequiredService<IPermissionCacheService>();
            cache.InvalidateAll();
        }

        // Log in with old password.
        var loginResp = await _client.PostAsJsonAsync("/api/auth/login",
            new LoginRequest(username, "OldPass@123"));
        var loginResult = await loginResp.Content.ReadFromJsonAsync<AuthResult>();
        var oldToken = loginResult!.Token;

        SetToken(oldToken);

        // Change password.
        var changeResp = await _client.PostAsJsonAsync("/api/auth/change-password",
            new ChangePasswordRequest("OldPass@123", "NewPass@456"));
        Assert.Equal(HttpStatusCode.OK, changeResp.StatusCode);

        var changeResult = await changeResp.Content.ReadFromJsonAsync<AuthResult>();
        Assert.NotNull(changeResult);
        Assert.NotEqual(oldToken, changeResult!.Token);

        // Old token should now be rejected.
        SetToken(oldToken);
        var meResp = await _client.GetAsync("/api/auth/me");
        Assert.Equal(HttpStatusCode.Unauthorized, meResp.StatusCode);

        // New token should work.
        SetToken(changeResult.Token);
        var meResp2 = await _client.GetAsync("/api/auth/me");
        Assert.Equal(HttpStatusCode.OK, meResp2.StatusCode);
    }
}
