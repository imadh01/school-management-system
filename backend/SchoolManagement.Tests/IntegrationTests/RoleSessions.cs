// File: backend/SchoolManagement.Tests/IntegrationTests/RoleSessions.cs
//
// One test user per role, created once per test run, with its access token cached.
// The authorization tests run hundreds of requests; logging in for each would be slow
// (password hashing is deliberately expensive).
//
// These users are separate from the seeded "admin", because other tests rotate that
// account's security stamp, which would invalidate a cached token.

using System.Collections.Concurrent;
using System.Net.Http.Headers;
using System.Net.Http.Json;
using System.Runtime.CompilerServices;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.DependencyInjection;
using SchoolManagement.Application.DTOs.Auth;
using SchoolManagement.Application.Interfaces;
using SchoolManagement.Domain.Entities;
using SchoolManagement.Infrastructure.Persistence;

namespace SchoolManagement.Tests.IntegrationTests;

internal static class RoleSessions
{
    private const string Password = "Role@12345";

    private static readonly ConcurrentDictionary<(int Factory, string Role), Lazy<Task<string>>> Tokens = new();

    /// <summary>A valid access token for a user holding exactly <paramref name="roleName"/>.</summary>
    public static Task<string> GetTokenAsync(CustomWebApplicationFactory factory, string roleName) =>
        Tokens.GetOrAdd(
            (RuntimeHelpers.GetHashCode(factory), roleName),
            key => new Lazy<Task<string>>(() => CreateUserAndLoginAsync(factory, key.Role))).Value;

    /// <summary>A request carrying the role's access token.</summary>
    public static async Task<HttpRequestMessage> RequestAsAsync(
        CustomWebApplicationFactory factory, string roleName, HttpMethod method, string url, object? body = null)
    {
        var request = new HttpRequestMessage(method, url);
        request.Headers.Authorization = new AuthenticationHeaderValue("Bearer", await GetTokenAsync(factory, roleName));
        if (body is not null)
            request.Content = JsonContent.Create(body);
        return request;
    }

    private static async Task<string> CreateUserAndLoginAsync(CustomWebApplicationFactory factory, string roleName)
    {
        var username = $"role_{roleName.ToLowerInvariant()}_{Guid.NewGuid():N}"[..40];

        using (var scope = factory.Services.CreateScope())
        {
            var db = scope.ServiceProvider.GetRequiredService<ApplicationDbContext>();
            var hasher = scope.ServiceProvider.GetRequiredService<IPasswordHasher>();
            var role = await db.Roles.SingleAsync(r => r.Name == roleName);

            var user = new User
            {
                Username = username,
                Email = $"{username}@test.local",
                PasswordHash = hasher.HashPassword(Password),
                Status = "Active",
                SecurityStamp = Guid.NewGuid(),
            };
            user.UserRoles.Add(new UserRole { RoleId = role.Id, AssignedAt = DateTime.UtcNow });
            db.Users.Add(user);
            await db.SaveChangesAsync();
        }

        using var client = factory.CreateClient();
        var response = await client.PostAsJsonAsync("/api/auth/login", new LoginRequest(username, Password));
        response.EnsureSuccessStatusCode();
        var result = await response.Content.ReadFromJsonAsync<AuthResult>();
        return result!.Token;
    }
}
