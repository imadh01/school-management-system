// E2a: the Users, My Account and Settings endpoints through the real API and database.
//
//   Users      - create with profile and address, teacher record follows the role, Student/Parent refused,
//                employee number unique, edit + 409 when stale, role/status/password/delete end the
//                user's sessions, a teacher who still teaches cannot leave the role or be deleted,
//                the "needs name" flag, unlock.
//   My Account - your own profile only; username/role stay; 409 when stale.
//   Settings   - read, save, 409 when stale, unknown country, IANA time-zone list, the basics endpoint.
//
// The last-admin rule and "two admins at once" are in DatabaseRules/UserAccountRulesTests.

using System.Net;
using System.Net.Http.Headers;
using System.Net.Http.Json;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.DependencyInjection;
using SchoolManagement.Application.DTOs.Addresses;
using SchoolManagement.Application.DTOs.Auth;
using SchoolManagement.Application.DTOs.Settings;
using SchoolManagement.Application.DTOs.Teachers;
using SchoolManagement.Application.DTOs.Users;
using SchoolManagement.Application.Interfaces;
using SchoolManagement.Domain.Constants;
using SchoolManagement.Domain.Entities;
using SchoolManagement.Infrastructure.Persistence;
using SchoolManagement.Tests.Support;
using Xunit;

namespace SchoolManagement.Tests.IntegrationTests;

[Collection("LegacyApi")]
public class UsersApiTests
{
    private const string AdminPassword = "Admin@12345";
    private const string TempPassword = "Temp@12345";
    private const string OwnPassword = "Mine@12345";

    private readonly CustomWebApplicationFactory _factory;
    private readonly HttpClient _client;

    public UsersApiTests(CustomWebApplicationFactory factory)
    {
        _factory = factory;
        _client = factory.CreateClient();
    }

    // ─────────────────────────────────────────────────────────────────
    // Create
    // ─────────────────────────────────────────────────────────────────

    [Fact]
    public async Task A_new_clerk_gets_a_profile_an_address_and_a_temporary_password()
    {
        var admin = await AdminTokenAsync();
        var uae = await CountryAsync("AE");
        var name = "u_" + TestData.Unique();
        var request = TestData.NewUserRequest(name, $"{name}@test.local", TempPassword, RoleNames.Clerk) with
        {
            Addresses = new() { new AddressRequest(AddressTypes.Home, true, "Villa 1", null, "Dubai", uae.Id, uae.Regions.First().Id, null) },
        };

        var created = await ReadAsync<UserResponse>(await SendAsync(admin, HttpMethod.Post, "/api/users", request), HttpStatusCode.Created);

        Assert.True(created.MustChangePassword);
        Assert.Equal(new[] { RoleNames.Clerk }, created.Roles);
        Assert.Null(created.TeacherId);
        Assert.Equal(name + " Name", created.Profile!.FullName);
        Assert.False(created.Profile.NeedsName);
        Assert.Equal(uae.Name, Assert.Single(created.Addresses).CountryName);

        var list = await ReadAsync<List<UserListItemResponse>>(await SendAsync(admin, HttpMethod.Get, "/api/users"));
        var row = Assert.Single(list, u => u.Id == created.Id);
        Assert.Equal(RoleNames.Clerk, row.Role);
        Assert.Equal("+971 50 123 4567", row.Mobile);
        Assert.True(row.MustChangePassword);
    }

    [Fact]
    public async Task A_new_teacher_also_gets_a_teacher_record_from_the_profile()
    {
        var admin = await AdminTokenAsync();
        var name = "t_" + TestData.Unique();
        var request = TestData.NewUserRequest(name, $"{name}@test.local", TempPassword, RoleNames.Teacher) with
        {
            Specialization = "Mathematics",
        };

        var created = await ReadAsync<UserResponse>(await SendAsync(admin, HttpMethod.Post, "/api/users", request), HttpStatusCode.Created);

        Assert.NotNull(created.TeacherId);
        var teacher = await ReadAsync<TeacherResponse>(await SendAsync(admin, HttpMethod.Get, $"/api/teachers/{created.TeacherId}"));
        Assert.Equal(name + " Name", teacher.Name);
        Assert.Equal("+971 50 123 4567", teacher.Phone);
        Assert.Equal("Mathematics", teacher.Specialization);
    }

    [Theory]
    [InlineData(RoleNames.Student)]
    [InlineData(RoleNames.Parent)]
    public async Task Student_and_parent_accounts_cannot_be_created_yet(string role)
    {
        var admin = await AdminTokenAsync();
        var name = "sp_" + TestData.Unique();

        var response = await SendAsync(admin, HttpMethod.Post, "/api/users",
            TestData.NewUserRequest(name, $"{name}@test.local", TempPassword, role));

        Assert.Equal(HttpStatusCode.UnprocessableEntity, response.StatusCode);
    }

    [Fact]
    public async Task An_employee_number_can_be_used_once()
    {
        var admin = await AdminTokenAsync();
        var number = "EMP-" + TestData.Unique();
        await CreateUserAsync(admin, RoleNames.Clerk, employeeNumber: number);

        var name = "dup_" + TestData.Unique();
        var request = new CreateUserRequest(name, $"{name}@test.local", TempPassword, RoleNames.Clerk,
            TestData.StaffProfile("Someone Else", number));
        var response = await SendAsync(admin, HttpMethod.Post, "/api/users", request);

        Assert.Equal(HttpStatusCode.Conflict, response.StatusCode);
    }

    [Fact]
    public async Task Missing_profile_fields_are_reported_by_name()
    {
        var admin = await AdminTokenAsync();
        var name = "v_" + TestData.Unique();
        var request = new CreateUserRequest(name, $"{name}@test.local", TempPassword, RoleNames.Clerk,
            TestData.StaffProfile() with { Gender = null, JoiningDate = null });

        var response = await SendAsync(admin, HttpMethod.Post, "/api/users", request);

        Assert.Equal(HttpStatusCode.BadRequest, response.StatusCode);
        var body = await response.Content.ReadAsStringAsync();
        Assert.Contains("Profile.Gender", body);
        Assert.Contains("Profile.JoiningDate", body);
    }

    // ─────────────────────────────────────────────────────────────────
    // Edit
    // ─────────────────────────────────────────────────────────────────

    [Fact]
    public async Task An_edit_saves_the_profile_mirrors_the_teacher_and_refuses_a_stale_version()
    {
        var admin = await AdminTokenAsync();
        var user = await CreateUserAsync(admin, RoleNames.Teacher);

        var edited = await ReadAsync<UserResponse>(await SendAsync(admin, HttpMethod.Put, $"/api/users/{user.Id}",
            UpdateRequest(user, profile: TestData.StaffProfile("Renamed Teacher") with { Mobile = "+971 55 999 0000" })));

        Assert.Equal("Renamed Teacher", edited.Profile!.FullName);
        Assert.NotEqual(user.RowVersion, edited.RowVersion);
        var teacher = await ReadAsync<TeacherResponse>(await SendAsync(admin, HttpMethod.Get, $"/api/teachers/{user.TeacherId}"));
        Assert.Equal("Renamed Teacher", teacher.Name); // the teacher page reads the profile
        Assert.Equal("+971 55 999 0000", teacher.Phone);

        // The first version again: somebody saved after it was loaded.
        var stale = await SendAsync(admin, HttpMethod.Put, $"/api/users/{user.Id}", UpdateRequest(user));
        Assert.Equal(HttpStatusCode.Conflict, stale.StatusCode);
    }

    [Fact]
    public async Task A_role_change_ends_the_session_and_removes_an_idle_teacher_record()
    {
        var admin = await AdminTokenAsync();
        var user = await CreateUserAsync(admin, RoleNames.Teacher);
        var session = await LoginAsync(user.Username, TempPassword);
        Assert.Equal(HttpStatusCode.OK, (await SendAsync(session.Token, HttpMethod.Get, "/api/auth/me")).StatusCode);

        var edited = await ReadAsync<UserResponse>(await SendAsync(admin, HttpMethod.Put, $"/api/users/{user.Id}",
            UpdateRequest(user, roleName: RoleNames.Clerk)));

        Assert.Equal(new[] { RoleNames.Clerk }, edited.Roles);
        Assert.Null(edited.TeacherId);
        Assert.Equal(HttpStatusCode.NotFound, (await SendAsync(admin, HttpMethod.Get, $"/api/teachers/{user.TeacherId}")).StatusCode);
        Assert.Equal(HttpStatusCode.Unauthorized, (await SendAsync(session.Token, HttpMethod.Get, "/api/auth/me")).StatusCode);
    }

    [Fact]
    public async Task A_teacher_who_still_teaches_cannot_leave_the_role_or_be_deleted()
    {
        var admin = await AdminTokenAsync();
        var user = await CreateUserAsync(admin, RoleNames.Teacher);
        await AssignSubjectAsync(user.TeacherId!.Value);

        var roleChange = await SendAsync(admin, HttpMethod.Put, $"/api/users/{user.Id}", UpdateRequest(user, roleName: RoleNames.Clerk));
        var delete = await SendAsync(admin, HttpMethod.Delete, $"/api/users/{user.Id}");

        Assert.Equal(HttpStatusCode.UnprocessableEntity, roleChange.StatusCode);
        Assert.Equal(HttpStatusCode.UnprocessableEntity, delete.StatusCode);
        var after = await ReadAsync<UserResponse>(await SendAsync(admin, HttpMethod.Get, $"/api/users/{user.Id}"));
        Assert.Equal(new[] { RoleNames.Teacher }, after.Roles);
        Assert.Equal(user.TeacherId, after.TeacherId);
    }

    [Fact]
    public async Task Making_a_clerk_a_teacher_creates_the_teacher_record()
    {
        var admin = await AdminTokenAsync();
        var user = await CreateUserAsync(admin, RoleNames.Clerk);

        var edited = await ReadAsync<UserResponse>(await SendAsync(admin, HttpMethod.Put, $"/api/users/{user.Id}",
            UpdateRequest(user, roleName: RoleNames.Teacher)));

        Assert.NotNull(edited.TeacherId);
        var teacher = await ReadAsync<TeacherResponse>(await SendAsync(admin, HttpMethod.Get, $"/api/teachers/{edited.TeacherId}"));
        Assert.Equal(user.Profile!.FullName, teacher.Name);
    }

    // ─────────────────────────────────────────────────────────────────
    // Status, password, unlock, delete
    // ─────────────────────────────────────────────────────────────────

    [Fact]
    public async Task Deactivating_ends_the_session_and_a_stale_version_is_refused()
    {
        var admin = await AdminTokenAsync();
        var user = await CreateUserAsync(admin, RoleNames.Clerk);
        var session = await LoginAsync(user.Username, TempPassword);

        var changed = await ReadAsync<UserResponse>(await SendAsync(admin, HttpMethod.Patch, $"/api/users/{user.Id}/status",
            new ChangeUserStatusRequest(UserStatuses.Inactive, user.RowVersion)));

        Assert.Equal(UserStatuses.Inactive, changed.Status);
        Assert.Equal(HttpStatusCode.Unauthorized, (await SendAsync(session.Token, HttpMethod.Get, "/api/auth/me")).StatusCode);

        var stale = await SendAsync(admin, HttpMethod.Patch, $"/api/users/{user.Id}/status",
            new ChangeUserStatusRequest(UserStatuses.Active, user.RowVersion));
        Assert.Equal(HttpStatusCode.Conflict, stale.StatusCode);
    }

    [Fact]
    public async Task A_reset_password_ends_the_session_and_must_be_changed()
    {
        var admin = await AdminTokenAsync();
        var user = await CreateUserAsync(admin, RoleNames.Clerk);
        var session = await LoginAsync(user.Username, TempPassword);

        var reset = await SendAsync(admin, HttpMethod.Post, $"/api/users/{user.Id}/reset-password",
            new ResetUserPasswordRequest("Reset@12345"));

        Assert.Equal(HttpStatusCode.NoContent, reset.StatusCode);
        Assert.Equal(HttpStatusCode.Unauthorized, (await SendAsync(session.Token, HttpMethod.Get, "/api/auth/me")).StatusCode);
        Assert.True((await LoginAsync(user.Username, "Reset@12345")).MustChangePassword);
    }

    [Fact]
    public async Task Unlock_ends_a_lockout()
    {
        var admin = await AdminTokenAsync();
        var user = await CreateUserAsync(admin, RoleNames.Clerk);
        await WithDbAsync(async db =>
        {
            var row = await db.Users.SingleAsync(u => u.Id == user.Id);
            row.LockoutEnd = DateTime.UtcNow.AddMinutes(15);
            await db.SaveChangesAsync();
        });
        Assert.True((await ListRowAsync(admin, user.Id)).IsLocked);

        var response = await SendAsync(admin, HttpMethod.Post, $"/api/users/{user.Id}/unlock");

        Assert.Equal(HttpStatusCode.NoContent, response.StatusCode);
        Assert.False((await ListRowAsync(admin, user.Id)).IsLocked);
    }

    [Fact]
    public async Task Deleting_removes_the_login_its_profile_and_its_teacher_record()
    {
        var admin = await AdminTokenAsync();
        var user = await CreateUserAsync(admin, RoleNames.Teacher);
        var session = await LoginAsync(user.Username, TempPassword);

        Assert.Equal(HttpStatusCode.NoContent, (await SendAsync(admin, HttpMethod.Delete, $"/api/users/{user.Id}")).StatusCode);

        Assert.Equal(HttpStatusCode.NotFound, (await SendAsync(admin, HttpMethod.Get, $"/api/users/{user.Id}")).StatusCode);
        Assert.Equal(HttpStatusCode.NotFound, (await SendAsync(admin, HttpMethod.Get, $"/api/teachers/{user.TeacherId}")).StatusCode);
        Assert.Equal(HttpStatusCode.Unauthorized, (await SendAsync(session.Token, HttpMethod.Get, "/api/auth/me")).StatusCode);
        await WithDbAsync(async db =>
            Assert.True((await db.StaffProfiles.IgnoreQueryFilters().SingleAsync(p => p.UserId == user.Id)).IsDeleted));
    }

    [Fact]
    public async Task You_cannot_delete_or_deactivate_yourself_here()
    {
        var (adminId, admin) = await AdminAsync();
        var me = await WithDbAsync(db => db.Users.AsNoTracking().SingleAsync(u => u.Id == adminId));

        var delete = await SendAsync(admin, HttpMethod.Delete, $"/api/users/{adminId}");
        var status = await SendAsync(admin, HttpMethod.Patch, $"/api/users/{adminId}/status",
            new ChangeUserStatusRequest(UserStatuses.Inactive, Convert.ToBase64String(me.RowVersion)));

        Assert.Equal(HttpStatusCode.UnprocessableEntity, delete.StatusCode);
        Assert.Equal(HttpStatusCode.UnprocessableEntity, status.StatusCode);
    }

    [Fact]
    public async Task A_profile_still_named_after_the_username_is_flagged()
    {
        var admin = await AdminTokenAsync();
        var name = "nn_" + TestData.Unique();
        var userId = await WithDbAsync(async db =>
        {
            // What the E2a migration copies for a non-teacher: the username as the name (decision 3).
            var user = new User { Username = name, Email = $"{name}@test.local", PasswordHash = "x", Status = "Active" };
            user.StaffProfile = new StaffProfile { FullName = name };
            db.Users.Add(user);
            await db.SaveChangesAsync();
            return user.Id;
        });

        Assert.True((await ListRowAsync(admin, userId)).NeedsName);
        var detail = await ReadAsync<UserResponse>(await SendAsync(admin, HttpMethod.Get, $"/api/users/{userId}"));
        Assert.True(detail.Profile!.NeedsName);
    }

    // ─────────────────────────────────────────────────────────────────
    // My Account
    // ─────────────────────────────────────────────────────────────────

    [Fact]
    public async Task My_account_edits_only_my_own_details()
    {
        var admin = await AdminTokenAsync();
        var user = await CreateUserAsync(admin, RoleNames.Clerk);
        var token = await SignInAndSetOwnPasswordAsync(user.Username);

        var mine = await ReadAsync<UserResponse>(await SendAsync(token, HttpMethod.Get, "/api/me/profile"));
        Assert.Equal(user.Id, mine.Id);

        var newEmail = $"own_{TestData.Unique()}@test.local";
        var request = new UpdateMyProfileRequest(newEmail, "My Own Name", "Male", new DateOnly(1990, 5, 1),
            "+971 52 111 2222", "B.Ed", "O+", "Next Of Kin", "+971 52 333 4444", null, mine.RowVersion);
        var updated = await ReadAsync<UserResponse>(await SendAsync(token, HttpMethod.Put, "/api/me/profile", request));

        Assert.Equal(newEmail, updated.Email);
        Assert.Equal("My Own Name", updated.Profile!.FullName);
        Assert.Equal(user.Username, updated.Username);                     // not editable here
        Assert.Equal(user.Profile!.JoiningDate, updated.Profile.JoiningDate);
        Assert.Equal(new[] { RoleNames.Clerk }, updated.Roles);

        var stale = await SendAsync(token, HttpMethod.Put, "/api/me/profile", request);
        Assert.Equal(HttpStatusCode.Conflict, stale.StatusCode);
    }

    // ─────────────────────────────────────────────────────────────────
    // Settings
    // ─────────────────────────────────────────────────────────────────

    [Fact]
    public async Task School_settings_save_and_the_basics_follow()
    {
        var admin = await AdminTokenAsync();
        var india = await CountryAsync("IN");
        var original = await ReadAsync<SchoolSettingsResponse>(await SendAsync(admin, HttpMethod.Get, "/api/settings/school"));

        try
        {
            var request = new UpdateSchoolSettingsRequest(
                "Test School " + TestData.Unique(), "TS", "office@test.school", null, "https://test.school", "Main Road",
                india.Id, "Asia/Kolkata", "INR",
                new List<string> { "Monday", "Tuesday", "Wednesday", "Thursday", "Friday", "Saturday" },
                null, original.RowVersion);
            var saved = await ReadAsync<SchoolSettingsResponse>(await SendAsync(admin, HttpMethod.Put, "/api/settings/school", request));

            Assert.Equal(request.SchoolName, saved.SchoolName);
            Assert.Equal(india.Name, saved.CountryName);
            Assert.Equal("Asia/Kolkata", saved.TimeZoneId);
            Assert.Equal(request.WorkingDays, saved.WorkingDays);

            // Every signed-in user sees the new name and country (the cache was refreshed after the save).
            var clerk = await SignInAndSetOwnPasswordAsync((await CreateUserAsync(admin, RoleNames.Clerk)).Username);
            var basics = await ReadAsync<SchoolBasicsResponse>(await SendAsync(clerk, HttpMethod.Get, "/api/settings/basics"));
            Assert.Equal(request.SchoolName, basics.SchoolName);
            Assert.Equal(india.Id, basics.CountryId);

            var stale = await SendAsync(admin, HttpMethod.Put, "/api/settings/school", request);
            Assert.Equal(HttpStatusCode.Conflict, stale.StatusCode);

            var unknownCountry = await SendAsync(admin, HttpMethod.Put, "/api/settings/school",
                request with { CountryId = 999999, RowVersion = saved.RowVersion });
            Assert.Equal(HttpStatusCode.BadRequest, unknownCountry.StatusCode);
            Assert.Contains("CountryId", await unknownCountry.Content.ReadAsStringAsync());
        }
        finally
        {
            var current = await ReadAsync<SchoolSettingsResponse>(await SendAsync(admin, HttpMethod.Get, "/api/settings/school"));
            var restore = new UpdateSchoolSettingsRequest(original.SchoolName, original.ShortName, original.Email, original.Phone,
                original.Website, original.AddressText, original.CountryId, original.TimeZoneId, original.CurrencyCode,
                original.WorkingDays, original.AboutTagline, current.RowVersion);
            await ReadAsync<SchoolSettingsResponse>(await SendAsync(admin, HttpMethod.Put, "/api/settings/school", restore));
        }
    }

    [Fact]
    public async Task The_time_zone_list_uses_IANA_ids()
    {
        var admin = await AdminTokenAsync();

        var zones = await ReadAsync<List<TimeZoneOption>>(await SendAsync(admin, HttpMethod.Get, "/api/settings/time-zones"));

        var dubai = Assert.Single(zones, z => z.Id == "Asia/Dubai");
        Assert.Equal(240, dubai.UtcOffsetMinutes);
        Assert.DoesNotContain(zones, z => z.Id.EndsWith("Standard Time"));
    }

    // ─────────────────────────────────────────────────────────────────
    // helpers
    // ─────────────────────────────────────────────────────────────────

    private async Task<UserResponse> CreateUserAsync(string adminToken, string role, string? employeeNumber = null)
    {
        var name = "e2_" + TestData.Unique();
        var request = new CreateUserRequest(name, $"{name}@test.local", TempPassword, role,
            TestData.StaffProfile("Staff " + name, employeeNumber));
        return await ReadAsync<UserResponse>(await SendAsync(adminToken, HttpMethod.Post, "/api/users", request), HttpStatusCode.Created);
    }

    private static UpdateUserRequest UpdateRequest(UserResponse user, string? roleName = null, StaffProfileRequest? profile = null) =>
        new(user.Username, user.Email, roleName ?? user.Roles.Single(),
            profile ?? TestData.StaffProfile(user.Profile!.FullName, user.Profile.EmployeeNumber),
            null, user.RowVersion);

    private async Task<UserListItemResponse> ListRowAsync(string adminToken, int userId) =>
        (await ReadAsync<List<UserListItemResponse>>(await SendAsync(adminToken, HttpMethod.Get, "/api/users")))
            .Single(u => u.Id == userId);

    /// <summary>A subject in a new class, taught by this teacher.</summary>
    private Task AssignSubjectAsync(int teacherId) => WithDbAsync(async db =>
    {
        var section = await TestData.CreateSectionAsync(db);
        db.Subjects.Add(new Subject { Name = "Maths", Code = "M" + TestData.Unique(), ClassSectionId = section.Id, TeacherId = teacherId, MaxMarks = 100, PassMarks = 35 });
        await db.SaveChangesAsync();
    });

    private Task<Country> CountryAsync(string code) =>
        WithDbAsync(db => db.Countries.AsNoTracking().Include(c => c.Regions).SingleAsync(c => c.Code == code));

    /// <summary>A fresh Admin (not the shared "admin": other tests rotate its stamp), its id and token.</summary>
    private async Task<(int Id, string Token)> AdminAsync()
    {
        var name = "e2admin_" + TestData.Unique();
        var id = await WithDbAsync(async db =>
        {
            var role = await db.Roles.SingleAsync(r => r.Name == RoleNames.Admin);
            using var scope = _factory.Services.CreateScope();
            var user = new User
            {
                Username = name, Email = $"{name}@test.local", Status = "Active", SecurityStamp = Guid.NewGuid(),
                PasswordHash = scope.ServiceProvider.GetRequiredService<IPasswordHasher>().HashPassword(AdminPassword),
            };
            user.UserRoles.Add(new UserRole { RoleId = role.Id, AssignedAt = DateTime.UtcNow });
            db.Users.Add(user);
            await db.SaveChangesAsync();
            return user.Id;
        });
        return (id, (await LoginAsync(name, AdminPassword)).Token);
    }

    private async Task<string> AdminTokenAsync() => (await AdminAsync()).Token;

    /// <summary>A new account may only call /auth/me and change-password until it chooses its own password.</summary>
    private async Task<string> SignInAndSetOwnPasswordAsync(string username)
    {
        var first = await LoginAsync(username, TempPassword);
        var response = await SendAsync(first.Token, HttpMethod.Post, "/api/auth/change-password",
            new ChangePasswordRequest(TempPassword, OwnPassword));
        return (await ReadAsync<AuthResult>(response)).Token;
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

    private static async Task<T> ReadAsync<T>(HttpResponseMessage response, HttpStatusCode expected = HttpStatusCode.OK)
    {
        Assert.True(response.StatusCode == expected,
            $"Expected {(int)expected}, got {(int)response.StatusCode}: {await response.Content.ReadAsStringAsync()}");
        return (await response.Content.ReadFromJsonAsync<T>())!;
    }

    private async Task<T> WithDbAsync<T>(Func<ApplicationDbContext, Task<T>> action)
    {
        using var scope = _factory.Services.CreateScope();
        return await action(scope.ServiceProvider.GetRequiredService<ApplicationDbContext>());
    }

    private async Task WithDbAsync(Func<ApplicationDbContext, Task> action)
    {
        using var scope = _factory.Services.CreateScope();
        await action(scope.ServiceProvider.GetRequiredService<ApplicationDbContext>());
    }
}
