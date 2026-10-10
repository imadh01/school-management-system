// Decision #7: Users.Username and Users.Email are unique among NON-deleted users only
// (filtered unique indexes, [IsDeleted] = 0).
//
//   1. A soft-deleted user's username and email can be used again (was a 500 before the fix).
//   2. A duplicate that slips past UserService's "already taken?" check (two admins at the
//      same moment) is rejected by the index and reported as a ConflictException (409),
//      not a raw database error (500).

using System.Net;
using System.Net.Http.Headers;
using System.Net.Http.Json;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.DependencyInjection;
using SchoolManagement.Application.DTOs.Auth;
using SchoolManagement.Application.DTOs.Users;
using SchoolManagement.Application.Interfaces;
using SchoolManagement.Domain.Entities;
using SchoolManagement.Domain.Exceptions;
using SchoolManagement.Infrastructure.Persistence;
using Xunit;

namespace SchoolManagement.Tests.IntegrationTests;

[Collection("LegacyApi")]
public class UserUniquenessTests
{
    private const string Password = "Passw0rd!";

    private readonly CustomWebApplicationFactory _factory;
    private readonly HttpClient _client;

    public UserUniquenessTests(CustomWebApplicationFactory factory)
    {
        _factory = factory;
        _client = factory.CreateClient();
    }

    [Fact]
    public async Task SoftDeletedUser_UsernameAndEmailCanBeReused()
    {
        var username = $"reuse_{Guid.NewGuid():N}"[..20];
        var email = $"{username}@test.local";
        await AuthenticateAsAdminAsync();

        var first = await _client.PostAsJsonAsync("/api/users", new CreateUserRequest(username, email, Password, "Teacher"));
        Assert.Equal(HttpStatusCode.Created, first.StatusCode);
        var firstUser = await first.Content.ReadFromJsonAsync<UserResponse>();

        await SoftDeleteAsync(firstUser!.Id);

        var second = await _client.PostAsJsonAsync("/api/users", new CreateUserRequest(username, email, Password, "Teacher"));
        Assert.Equal(HttpStatusCode.Created, second.StatusCode);
        var secondUser = await second.Content.ReadFromJsonAsync<UserResponse>();
        Assert.NotEqual(firstUser.Id, secondUser!.Id);
    }

    [Fact]
    public async Task ActiveUser_DuplicateUsername_IsStillRejectedByTheDatabase_AsConflict()
    {
        var username = $"race_{Guid.NewGuid():N}"[..20];
        await InsertUserAsync(username, $"{username}@test.local");

        // Simulates the second admin of a race: the service check already passed,
        // so only the unique index stands between us and a duplicate.
        using var scope = _factory.Services.CreateScope();
        var repository = scope.ServiceProvider.GetRequiredService<IUserRepository>();

        await Assert.ThrowsAsync<ConflictException>(() => repository.AddAsync(
            NewUser(username, $"other_{username}@test.local"), CancellationToken.None));
    }

    [Fact]
    public async Task ActiveUser_DuplicateEmail_IsStillRejectedByTheDatabase_AsConflict()
    {
        var username = $"mail_{Guid.NewGuid():N}"[..20];
        var email = $"{username}@test.local";
        await InsertUserAsync(username, email);

        using var scope = _factory.Services.CreateScope();
        var repository = scope.ServiceProvider.GetRequiredService<IUserRepository>();

        await Assert.ThrowsAsync<ConflictException>(() => repository.AddAsync(
            NewUser($"x{username}", email), CancellationToken.None));
    }

    // ─────────────────────────────────────────────────────────────────
    // Helpers
    // ─────────────────────────────────────────────────────────────────

    private async Task AuthenticateAsAdminAsync()
    {
        var response = await _client.PostAsJsonAsync("/api/auth/login", new LoginRequest("admin", "Admin@123"));
        response.EnsureSuccessStatusCode();
        var result = await response.Content.ReadFromJsonAsync<AuthResult>();
        _client.DefaultRequestHeaders.Authorization = new AuthenticationHeaderValue("Bearer", result!.Token);
    }

    private static User NewUser(string username, string email) => new()
    {
        Username = username,
        Email = email,
        PasswordHash = "not-a-real-hash",
        Status = "Active",
    };

    private async Task InsertUserAsync(string username, string email)
    {
        using var scope = _factory.Services.CreateScope();
        var db = scope.ServiceProvider.GetRequiredService<ApplicationDbContext>();
        db.Users.Add(NewUser(username, email));
        await db.SaveChangesAsync();
    }

    private async Task SoftDeleteAsync(int userId)
    {
        using var scope = _factory.Services.CreateScope();
        var db = scope.ServiceProvider.GetRequiredService<ApplicationDbContext>();
        var user = await db.Users.SingleAsync(u => u.Id == userId);
        user.IsDeleted = true;
        user.DeletedAt = DateTime.UtcNow;
        await db.SaveChangesAsync();
    }
}
