// File: backend/SchoolManagement.Tests/IntegrationTests/RolesApiTests.cs
//
// Roles API (Batch E1) through HTTP: creation rules, every guard, concurrency, the change taking
// effect immediately (cache invalidation), and the audit trail.
//
// Only CUSTOM roles are changed here; system-role grants are frozen by RoleMatrixSnapshotTests.

using System.Net;
using System.Net.Http.Json;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.DependencyInjection;
using SchoolManagement.Application.DTOs.Roles;
using SchoolManagement.Domain.Constants;
using SchoolManagement.Domain.Entities;
using SchoolManagement.Infrastructure.Persistence;
using Xunit;

namespace SchoolManagement.Tests.IntegrationTests;

[Collection("LegacyApi")]
public class RolesApiTests
{
    private readonly CustomWebApplicationFactory _factory;
    private readonly HttpClient _client;

    public RolesApiTests(CustomWebApplicationFactory factory)
    {
        _factory = factory;
        _client = factory.CreateClient();
    }

    private static string NewRoleName(string prefix = "R") => prefix + Guid.NewGuid().ToString("N")[..12];

    // ─────────────────────────────────────────────────────────────────
    // Create
    // ─────────────────────────────────────────────────────────────────

    [Fact]
    public async Task Admin_creates_a_custom_role_and_reads_it_back()
    {
        var name = NewRoleName();

        var created = await CreateRoleAsAsync(RoleNames.Admin, name, Permissions.StudentsView);
        Assert.Equal(HttpStatusCode.Created, created.StatusCode);
        var role = await created.Content.ReadFromJsonAsync<RoleResponse>();
        Assert.False(role!.IsSystem);
        Assert.True(role.IsActive);
        Assert.Equal(new[] { Permissions.StudentsView }, role.Permissions);

        var list = await SendAsAsync<List<RoleSummaryResponse>>(RoleNames.Admin, HttpMethod.Get, "/api/roles");
        var row = Assert.Single(list, r => r.Id == role.Id);
        Assert.Equal(0, row.UserCount);
        Assert.Equal(1, row.PermissionCount);
    }

    [Theory]
    [InlineData("teacher")]
    [InlineData("ADMIN")]
    [InlineData("Parent")]
    public async Task A_custom_role_cannot_take_a_system_name_in_any_case(string name)
    {
        var response = await CreateRoleAsAsync(RoleNames.Admin, name);

        Assert.Equal(HttpStatusCode.UnprocessableEntity, response.StatusCode);
    }

    [Fact]
    public async Task A_duplicate_role_name_is_a_conflict()
    {
        var name = NewRoleName();
        Assert.Equal(HttpStatusCode.Created, (await CreateRoleAsAsync(RoleNames.Admin, name)).StatusCode);

        var second = await CreateRoleAsAsync(RoleNames.Admin, name.ToLowerInvariant());

        Assert.Equal(HttpStatusCode.Conflict, second.StatusCode);
    }

    [Fact]
    public async Task A_permission_without_what_it_needs_is_refused()
    {
        // Students.Edit needs Students.View and ClassSections.View (the class dropdown).
        var response = await CreateRoleAsAsync(RoleNames.Admin, NewRoleName(), Permissions.StudentsEdit);

        Assert.Equal(HttpStatusCode.UnprocessableEntity, response.StatusCode);
        var body = await response.Content.ReadAsStringAsync();
        Assert.Contains(Permissions.StudentsView, body);
        Assert.Contains(Permissions.ClassSectionsView, body);
    }

    [Fact]
    public async Task An_unknown_permission_is_refused()
    {
        var response = await CreateRoleAsAsync(RoleNames.Admin, NewRoleName(), "Students.Teleport");

        Assert.Equal(HttpStatusCode.UnprocessableEntity, response.StatusCode);
    }

    // ─────────────────────────────────────────────────────────────────
    // Guards for a non-Admin role manager
    // ─────────────────────────────────────────────────────────────────

    [Fact]
    public async Task A_role_manager_cannot_grant_permissions_they_do_not_hold()
    {
        var manager = await CreateRoleManagerAsync();

        var allowed = await CreateRoleAsAsync(manager, NewRoleName(), Permissions.StudentsView);
        Assert.Equal(HttpStatusCode.Created, allowed.StatusCode);

        var escalation = await CreateRoleAsAsync(manager, NewRoleName(),
            Permissions.StudentsView, Permissions.ClassSectionsView, Permissions.StudentsEdit);
        Assert.Equal(HttpStatusCode.Forbidden, escalation.StatusCode);
    }

    [Fact]
    public async Task A_role_manager_cannot_change_their_own_role()
    {
        var manager = await CreateRoleManagerAsync();
        var own = await GetRoleAsync(RoleNames.Admin, manager);

        var permissions = await SendAsync(manager, HttpMethod.Put, $"/api/roles/{own.Id}/permissions",
            new SetRolePermissionsRequest(own.Permissions.ToList(), own.RowVersion));
        var rename = await SendAsync(manager, HttpMethod.Put, $"/api/roles/{own.Id}",
            new UpdateRoleRequest(own.Name + "x", null, own.RowVersion));
        var status = await SendAsync(manager, HttpMethod.Patch, $"/api/roles/{own.Id}/status",
            new ChangeRoleStatusRequest(false, own.RowVersion));

        Assert.Equal(HttpStatusCode.Forbidden, permissions.StatusCode);
        Assert.Equal(HttpStatusCode.Forbidden, rename.StatusCode);
        Assert.Equal(HttpStatusCode.Forbidden, status.StatusCode);
    }

    [Fact]
    public async Task The_Admin_role_cannot_be_edited_or_deactivated()
    {
        var manager = await CreateRoleManagerAsync();
        var admin = await GetRoleAsync(manager, RoleNames.Admin);

        var permissions = await SendAsync(manager, HttpMethod.Put, $"/api/roles/{admin.Id}/permissions",
            new SetRolePermissionsRequest(new List<string>(), admin.RowVersion));
        var status = await SendAsync(manager, HttpMethod.Patch, $"/api/roles/{admin.Id}/status",
            new ChangeRoleStatusRequest(false, admin.RowVersion));

        Assert.Equal(HttpStatusCode.UnprocessableEntity, permissions.StatusCode);
        Assert.Equal(HttpStatusCode.UnprocessableEntity, status.StatusCode);
    }

    [Fact]
    public async Task A_system_role_cannot_be_renamed()
    {
        var clerk = await GetRoleAsync(RoleNames.Admin, RoleNames.Clerk);

        var response = await SendAsync(RoleNames.Admin, HttpMethod.Put, $"/api/roles/{clerk.Id}",
            new UpdateRoleRequest("Office Clerk", clerk.Description, clerk.RowVersion));

        Assert.Equal(HttpStatusCode.UnprocessableEntity, response.StatusCode);
    }

    // ─────────────────────────────────────────────────────────────────
    // Concurrency
    // ─────────────────────────────────────────────────────────────────

    [Fact]
    public async Task Two_admins_editing_the_same_role_the_second_gets_409()
    {
        var name = NewRoleName();
        await CreateRoleAsAsync(RoleNames.Admin, name, Permissions.StudentsView);
        var loaded = await GetRoleAsync(RoleNames.Admin, name);

        var first = await SendAsync(RoleNames.Admin, HttpMethod.Put, $"/api/roles/{loaded.Id}/permissions",
            new SetRolePermissionsRequest(new List<string> { Permissions.StudentsView, Permissions.ParentsView }, loaded.RowVersion));
        var second = await SendAsync(RoleNames.Admin, HttpMethod.Put, $"/api/roles/{loaded.Id}/permissions",
            new SetRolePermissionsRequest(new List<string>(), loaded.RowVersion));

        Assert.Equal(HttpStatusCode.OK, first.StatusCode);
        Assert.Equal(HttpStatusCode.Conflict, second.StatusCode);

        // Changing only the permission list still moved the role's version on.
        var afterFirst = await first.Content.ReadFromJsonAsync<RoleResponse>();
        Assert.NotEqual(loaded.RowVersion, afterFirst!.RowVersion);
    }

    // ─────────────────────────────────────────────────────────────────
    // Effect is immediate (cache invalidated after commit)
    // ─────────────────────────────────────────────────────────────────

    [Fact]
    public async Task Removing_a_permission_takes_effect_on_the_next_request()
    {
        var name = NewRoleName("Exam");
        await CreateRoleAsAsync(RoleNames.Admin, name, Permissions.StudentsView);
        Assert.Equal(HttpStatusCode.OK, (await SendAsync(name, HttpMethod.Get, "/api/students")).StatusCode);

        var role = await GetRoleAsync(RoleNames.Admin, name);
        var change = await SendAsync(RoleNames.Admin, HttpMethod.Put, $"/api/roles/{role.Id}/permissions",
            new SetRolePermissionsRequest(new List<string>(), role.RowVersion));
        Assert.Equal(HttpStatusCode.OK, change.StatusCode);

        // Same access token as before: the permission is resolved server-side on every request.
        Assert.Equal(HttpStatusCode.Forbidden, (await SendAsync(name, HttpMethod.Get, "/api/students")).StatusCode);
    }

    [Fact]
    public async Task A_deactivated_role_grants_nothing_until_reactivated()
    {
        var name = NewRoleName("Lib");
        await CreateRoleAsAsync(RoleNames.Admin, name, Permissions.StudentsView);
        Assert.Equal(HttpStatusCode.OK, (await SendAsync(name, HttpMethod.Get, "/api/students")).StatusCode);

        var role = await GetRoleAsync(RoleNames.Admin, name);
        var off = await SendAsync(RoleNames.Admin, HttpMethod.Patch, $"/api/roles/{role.Id}/status",
            new ChangeRoleStatusRequest(false, role.RowVersion));
        Assert.Equal(HttpStatusCode.OK, off.StatusCode);
        Assert.Equal(HttpStatusCode.Forbidden, (await SendAsync(name, HttpMethod.Get, "/api/students")).StatusCode);

        var deactivated = await off.Content.ReadFromJsonAsync<RoleResponse>();
        var on = await SendAsync(RoleNames.Admin, HttpMethod.Patch, $"/api/roles/{role.Id}/status",
            new ChangeRoleStatusRequest(true, deactivated!.RowVersion));
        Assert.Equal(HttpStatusCode.OK, on.StatusCode);
        Assert.Equal(HttpStatusCode.OK, (await SendAsync(name, HttpMethod.Get, "/api/students")).StatusCode);
    }

    // ─────────────────────────────────────────────────────────────────
    // Audit + catalog
    // ─────────────────────────────────────────────────────────────────

    [Fact]
    public async Task Permission_changes_are_written_to_the_audit_log()
    {
        var name = NewRoleName();
        await CreateRoleAsAsync(RoleNames.Admin, name, Permissions.StudentsView);
        var role = await GetRoleAsync(RoleNames.Admin, name);

        await SendAsync(RoleNames.Admin, HttpMethod.Put, $"/api/roles/{role.Id}/permissions",
            new SetRolePermissionsRequest(new List<string> { Permissions.ParentsView }, role.RowVersion));

        using var scope = _factory.Services.CreateScope();
        var db = scope.ServiceProvider.GetRequiredService<ApplicationDbContext>();
        var actions = await db.AuditLogs.AsNoTracking()
            .Where(a => a.TableName == "RolePermissions" && a.RecordId.StartsWith($"RoleId={role.Id},"))
            .Select(a => a.Action)
            .ToListAsync();

        // Insert (create), then Delete (Students.View) + Insert (Parents.View).
        Assert.Contains(AuditActions.Delete, actions);
        Assert.True(actions.Count(a => a == AuditActions.Insert) >= 2);
    }

    [Fact]
    public async Task The_catalog_lists_each_permission_with_what_it_requires()
    {
        var catalog = await SendAsAsync<List<PermissionModuleResponse>>(RoleNames.Admin, HttpMethod.Get, "/api/permissions");

        var students = Assert.Single(catalog, m => m.Module == "Students");
        var edit = Assert.Single(students.Permissions, p => p.Name == Permissions.StudentsEdit);
        Assert.Contains(Permissions.StudentsView, edit.Requires);
        Assert.Contains(Permissions.ClassSectionsView, edit.Requires);
        Assert.Equal(Permissions.All.Count, catalog.Sum(m => m.Permissions.Count));
    }

    // ─────────────────────────────────────────────────────────────────
    // Helpers
    // ─────────────────────────────────────────────────────────────────

    /// <summary>A custom role that may manage roles but holds only Students.View otherwise. Returns its name.</summary>
    private async Task<string> CreateRoleManagerAsync()
    {
        var name = NewRoleName("Mgr");
        var response = await CreateRoleAsAsync(RoleNames.Admin, name,
            Permissions.RolesView, Permissions.RolesCreate, Permissions.RolesEdit, Permissions.StudentsView);
        Assert.Equal(HttpStatusCode.Created, response.StatusCode);
        return name;
    }

    private Task<HttpResponseMessage> CreateRoleAsAsync(string actingRole, string name, params string[] permissions) =>
        SendAsync(actingRole, HttpMethod.Post, "/api/roles",
            new CreateRoleRequest(name, "Created by a test", permissions.ToList()));

    /// <summary>Finds a role by name through the API (as <paramref name="actingRole"/>) and loads its details.</summary>
    private async Task<RoleResponse> GetRoleAsync(string actingRole, string roleName)
    {
        var list = await SendAsAsync<List<RoleSummaryResponse>>(actingRole, HttpMethod.Get, "/api/roles");
        var summary = list.Single(r => r.Name == roleName);
        return await SendAsAsync<RoleResponse>(actingRole, HttpMethod.Get, $"/api/roles/{summary.Id}");
    }

    private async Task<HttpResponseMessage> SendAsync(string actingRole, HttpMethod method, string url, object? body = null)
    {
        using var request = await RoleSessions.RequestAsAsync(_factory, actingRole, method, url, body);
        return await _client.SendAsync(request);
    }

    private async Task<T> SendAsAsync<T>(string actingRole, HttpMethod method, string url)
    {
        var response = await SendAsync(actingRole, method, url);
        Assert.Equal(HttpStatusCode.OK, response.StatusCode);
        return (await response.Content.ReadFromJsonAsync<T>())!;
    }
}
