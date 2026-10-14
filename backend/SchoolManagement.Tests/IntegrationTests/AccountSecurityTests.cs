// E2 security fixes, end-to-end through the real API:
//
//   Problem 1 - a teacher who is deactivated, suspended or deleted on the Teachers page, or whose password
//               an admin sets, is refused on their very next request (before the fix their current access
//               token kept working, because the permission cache still said "Active").
//   Problem 3 - password hashes and security stamps never reach the audit log, on any path (create, edit,
//               change password, delete), and a sign-in writes no audit row at all.
//
// The "last active Admin" rule (Problem 2) is tested in DatabaseRules/UserAccountRulesTests.

using System.Net;
using System.Net.Http.Headers;
using System.Net.Http.Json;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.DependencyInjection;
using SchoolManagement.Application.DTOs.Auth;
using SchoolManagement.Application.DTOs.Teachers;
using SchoolManagement.Application.Interfaces;
using SchoolManagement.Domain.Constants;
using SchoolManagement.Domain.Entities;
using SchoolManagement.Infrastructure.Persistence;
using SchoolManagement.Tests.Support;
using Xunit;

namespace SchoolManagement.Tests.IntegrationTests;

[Collection("LegacyApi")]
public class AccountSecurityTests
{
    private const string AdminPassword = "Admin@12345";
    private const string TeacherPassword = "Teach@12345";

    private readonly CustomWebApplicationFactory _factory;
    private readonly HttpClient _client;

    public AccountSecurityTests(CustomWebApplicationFactory factory)
    {
        _factory = factory;
        _client = factory.CreateClient();
    }

    // ─────────────────────────────────────────────────────────────────
    // Problem 1: teacher account changes end the session at once
    // ─────────────────────────────────────────────────────────────────

    [Fact]
    public async Task A_new_teacher_must_change_the_password_chosen_for_them()
    {
        var admin = await AdminTokenAsync();
        var teacher = await CreateTeacherAsync(admin);

        var login = await LoginAsync(teacher.Username, TeacherPassword);

        Assert.True(login.MustChangePassword);
    }

    [Theory]
    [InlineData("Inactive")]
    [InlineData("Suspended")]
    public async Task A_teacher_deactivated_on_the_status_endpoint_is_refused_on_the_next_request(string status)
    {
        var admin = await AdminTokenAsync();
        var teacher = await CreateTeacherAsync(admin);
        var token = (await LoginAsync(teacher.Username, TeacherPassword)).Token;
        Assert.Equal(HttpStatusCode.OK, (await SendAsync(token, HttpMethod.Get, "/api/auth/me")).StatusCode);

        var change = await SendAsync(admin, HttpMethod.Patch, $"/api/teachers/{teacher.Id}/status",
            new ChangeTeacherStatusRequest(status));
        Assert.Equal(HttpStatusCode.OK, change.StatusCode);

        Assert.Equal(HttpStatusCode.Unauthorized, (await SendAsync(token, HttpMethod.Get, "/api/auth/me")).StatusCode);
    }

    [Fact]
    public async Task A_teacher_deactivated_through_the_edit_form_is_refused_on_the_next_request()
    {
        var admin = await AdminTokenAsync();
        var teacher = await CreateTeacherAsync(admin);
        var token = (await LoginAsync(teacher.Username, TeacherPassword)).Token;

        var update = await SendAsync(admin, HttpMethod.Put, $"/api/teachers/{teacher.Id}",
            new UpdateTeacherRequest(teacher.Name, teacher.Username, teacher.Email, null, null, "Inactive", null));
        Assert.Equal(HttpStatusCode.OK, update.StatusCode);

        Assert.Equal(HttpStatusCode.Unauthorized, (await SendAsync(token, HttpMethod.Get, "/api/auth/me")).StatusCode);
    }

    [Fact]
    public async Task A_deleted_teacher_is_refused_on_the_next_request()
    {
        var admin = await AdminTokenAsync();
        var teacher = await CreateTeacherAsync(admin);
        var token = (await LoginAsync(teacher.Username, TeacherPassword)).Token;

        var delete = await SendAsync(admin, HttpMethod.Delete, $"/api/teachers/{teacher.Id}");
        Assert.Equal(HttpStatusCode.NoContent, delete.StatusCode);

        Assert.Equal(HttpStatusCode.Unauthorized, (await SendAsync(token, HttpMethod.Get, "/api/auth/me")).StatusCode);
    }

    [Fact]
    public async Task A_password_set_by_an_admin_ends_the_session_and_must_be_changed()
    {
        var admin = await AdminTokenAsync();
        var teacher = await CreateTeacherAsync(admin);
        var token = (await LoginAsync(teacher.Username, TeacherPassword)).Token;

        var update = await SendAsync(admin, HttpMethod.Put, $"/api/teachers/{teacher.Id}",
            new UpdateTeacherRequest(teacher.Name, teacher.Username, teacher.Email, null, null, "Active", "Reset@12345"));
        Assert.Equal(HttpStatusCode.OK, update.StatusCode);

        Assert.Equal(HttpStatusCode.Unauthorized, (await SendAsync(token, HttpMethod.Get, "/api/auth/me")).StatusCode);
        Assert.True((await LoginAsync(teacher.Username, "Reset@12345")).MustChangePassword);
    }

    // ─────────────────────────────────────────────────────────────────
    // Problem 3: no hash or stamp in the audit log, on any path
    // ─────────────────────────────────────────────────────────────────

    [Fact]
    public async Task Hashes_and_stamps_never_reach_the_audit_log()
    {
        var admin = await AdminTokenAsync();

        // Path 1: create (Insert row with every column).
        var teacher = await CreateTeacherAsync(admin);
        var secrets = new List<string>();
        secrets.AddRange(await SecretsOfAsync(teacher.UserId));

        // Path 2: change your own password (Update: hash, stamp, MustChangePassword).
        var login = await LoginAsync(teacher.Username, TeacherPassword);
        var changed = await SendAsync(login.Token, HttpMethod.Post, "/api/auth/change-password",
            new ChangePasswordRequest(TeacherPassword, "Changed@12345"));
        Assert.Equal(HttpStatusCode.OK, changed.StatusCode);
        secrets.AddRange(await SecretsOfAsync(teacher.UserId));

        // Path 3: a password set by an admin (account service).
        var update = await SendAsync(admin, HttpMethod.Put, $"/api/teachers/{teacher.Id}",
            new UpdateTeacherRequest(teacher.Name, teacher.Username, teacher.Email, null, null, "Active", "Reset@12345"));
        Assert.Equal(HttpStatusCode.OK, update.StatusCode);
        secrets.AddRange(await SecretsOfAsync(teacher.UserId));

        // Path 4: delete (soft delete; the stamp rotates).
        var delete = await SendAsync(admin, HttpMethod.Delete, $"/api/teachers/{teacher.Id}");
        Assert.Equal(HttpStatusCode.NoContent, delete.StatusCode);
        secrets.AddRange(await SecretsOfAsync(teacher.UserId));

        var logs = await UserAuditRowsAsync(teacher.UserId);
        Assert.Contains(logs, l => l.Action == AuditActions.Insert);
        Assert.Contains(logs, l => l.Action == AuditActions.Update);
        Assert.Contains(logs, l => l.Action == AuditActions.Delete);
        foreach (var log in logs)
        {
            foreach (var secret in secrets.Distinct())
                Assert.DoesNotContain(secret, log.Changes ?? string.Empty, StringComparison.OrdinalIgnoreCase);
        }
        // ...but the log still says that the password changed.
        Assert.Contains(logs, l =>
            (l.Changes ?? string.Empty).Contains("PasswordHash", StringComparison.OrdinalIgnoreCase)
            && (l.Changes ?? string.Empty).Contains("\"***\""));
    }

    [Fact]
    public async Task Hard_deleting_a_user_logs_no_hash_either()
    {
        int userId;
        string hash;
        using (var scope = _factory.Services.CreateScope())
        {
            var db = scope.ServiceProvider.GetRequiredService<ApplicationDbContext>();
            var name = "hard_" + TestData.Unique();
            var user = new User
            {
                Username = name, Email = $"{name}@test.local", Status = "Active", SecurityStamp = Guid.NewGuid(),
                PasswordHash = scope.ServiceProvider.GetRequiredService<IPasswordHasher>().HashPassword("Hard@12345"),
            };
            db.Users.Add(user);
            await db.SaveChangesAsync();
            (userId, hash) = (user.Id, user.PasswordHash);

            db.Users.Remove(user); // a real DELETE: the log row lists every old value
            await db.SaveChangesAsync();
        }

        var logs = await UserAuditRowsAsync(userId);
        Assert.Contains(logs, l => l.Action == AuditActions.Delete);
        Assert.All(logs, l => Assert.DoesNotContain(hash, l.Changes ?? string.Empty));
    }

    [Fact]
    public async Task Signing_in_writes_no_audit_row()
    {
        var admin = await AdminTokenAsync();
        var teacher = await CreateTeacherAsync(admin);
        var before = (await UserAuditRowsAsync(teacher.UserId)).Count;

        await LoginAsync(teacher.Username, TeacherPassword);                     // LastLoginAt changes
        var wrong = await _client.PostAsJsonAsync("/api/auth/login", new LoginRequest(teacher.Username, "Wrong@12345"));
        Assert.Equal(HttpStatusCode.Unauthorized, wrong.StatusCode);             // AccessFailedCount changes

        Assert.Equal(before, (await UserAuditRowsAsync(teacher.UserId)).Count);
    }

    // ─────────────────────────────────────────────────────────────────
    // helpers
    // ─────────────────────────────────────────────────────────────────

    /// <summary>A fresh Admin user (not the shared "admin": other tests rotate its stamp) and its token.</summary>
    private async Task<string> AdminTokenAsync()
    {
        var name = "secadmin_" + TestData.Unique();
        using (var scope = _factory.Services.CreateScope())
        {
            var db = scope.ServiceProvider.GetRequiredService<ApplicationDbContext>();
            var role = await db.Roles.SingleAsync(r => r.Name == RoleNames.Admin);
            var user = new User
            {
                Username = name, Email = $"{name}@test.local", Status = "Active", SecurityStamp = Guid.NewGuid(),
                PasswordHash = scope.ServiceProvider.GetRequiredService<IPasswordHasher>().HashPassword(AdminPassword),
            };
            user.UserRoles.Add(new UserRole { RoleId = role.Id, AssignedAt = DateTime.UtcNow });
            db.Users.Add(user);
            await db.SaveChangesAsync();
        }
        return (await LoginAsync(name, AdminPassword)).Token;
    }

    private async Task<TeacherResponse> CreateTeacherAsync(string adminToken)
    {
        var name = "sect_" + TestData.Unique();
        var response = await SendAsync(adminToken, HttpMethod.Post, "/api/teachers",
            new CreateTeacherRequest("Teacher " + name, name, $"{name}@test.local", TeacherPassword, null, null));
        Assert.Equal(HttpStatusCode.Created, response.StatusCode);
        return (await response.Content.ReadFromJsonAsync<TeacherResponse>())!;
    }

    private async Task<AuthResult> LoginAsync(string username, string password)
    {
        var response = await _client.PostAsJsonAsync("/api/auth/login", new LoginRequest(username, password));
        response.EnsureSuccessStatusCode();
        return (await response.Content.ReadFromJsonAsync<AuthResult>())!;
    }

    private async Task<HttpResponseMessage> SendAsync(string token, HttpMethod method, string url, object? body = null)
    {
        using var request = new HttpRequestMessage(method, url);
        request.Headers.Authorization = new AuthenticationHeaderValue("Bearer", token);
        if (body is not null) request.Content = JsonContent.Create(body);
        return await _client.SendAsync(request);
    }

    /// <summary>The user's current hash and stamp: values that must never appear in the log.</summary>
    private async Task<string[]> SecretsOfAsync(int userId)
    {
        using var scope = _factory.Services.CreateScope();
        var db = scope.ServiceProvider.GetRequiredService<ApplicationDbContext>();
        var user = await db.Users.IgnoreQueryFilters().SingleAsync(u => u.Id == userId);
        return new[] { user.PasswordHash, user.SecurityStamp.ToString() };
    }

    private async Task<List<AuditLog>> UserAuditRowsAsync(int userId)
    {
        using var scope = _factory.Services.CreateScope();
        var db = scope.ServiceProvider.GetRequiredService<ApplicationDbContext>();
        return await db.AuditLogs
            .Where(l => l.TableName == "Users" && l.RecordId == userId.ToString())
            .ToListAsync();
    }
}
