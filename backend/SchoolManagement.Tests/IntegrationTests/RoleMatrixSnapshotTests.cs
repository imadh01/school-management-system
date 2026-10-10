// File: backend/SchoolManagement.Tests/IntegrationTests/RoleMatrixSnapshotTests.cs
//
// The database built by the migrations must match the code exactly:
//   1. Permissions table == the code catalog (keyed by name, module included).
//   2. System-role grants == ExpectedRoleMatrix (Admin: no rows at all).
//   3. Every seeded role satisfies PermissionDependencies (no broken forms).
//   4. Admin still gets the whole catalog, from code, with zero grant rows.

using System.Net;
using System.Net.Http.Json;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.DependencyInjection;
using SchoolManagement.Application.DTOs.Auth;
using SchoolManagement.Domain.Constants;
using SchoolManagement.Infrastructure.Persistence;
using Xunit;

namespace SchoolManagement.Tests.IntegrationTests;

[Collection("LegacyApi")]
public class RoleMatrixSnapshotTests
{
    private readonly CustomWebApplicationFactory _factory;

    public RoleMatrixSnapshotTests(CustomWebApplicationFactory factory) => _factory = factory;

    [Fact]
    public async Task Permissions_table_equals_the_code_catalog()
    {
        using var scope = _factory.Services.CreateScope();
        var db = scope.ServiceProvider.GetRequiredService<ApplicationDbContext>();

        var inDatabase = await db.Permissions.AsNoTracking()
            .Select(p => p.Name + " | " + p.Module)
            .ToListAsync();
        var inCode = Permissions.Definitions.Select(d => d.Name + " | " + d.Module).ToList();

        Assert.Equal(inCode.OrderBy(x => x, StringComparer.Ordinal), inDatabase.OrderBy(x => x, StringComparer.Ordinal));
    }

    [Fact]
    public async Task System_role_grants_equal_the_agreed_matrix()
    {
        var actual = await ReadSystemRoleGrantsAsync();

        foreach (var (role, expected) in ExpectedRoleMatrix.SeededGrants)
        {
            var got = actual.TryGetValue(role, out var grants) ? grants : new SortedSet<string>();
            Assert.True(
                expected.SetEquals(got),
                $"{role}: expected [{string.Join(", ", expected.OrderBy(p => p))}] " +
                $"but the database has [{string.Join(", ", got)}]");
        }
    }

    [Fact]
    public async Task System_roles_are_flagged_and_active()
    {
        using var scope = _factory.Services.CreateScope();
        var db = scope.ServiceProvider.GetRequiredService<ApplicationDbContext>();

        var roles = await db.Roles.AsNoTracking().Where(r => RoleNames.All.Contains(r.Name)).ToListAsync();

        Assert.Equal(RoleNames.All.Count, roles.Count);
        Assert.All(roles, r => Assert.True(r.IsSystem, $"{r.Name} should be a system role"));
        Assert.All(roles, r => Assert.True(r.IsActive, $"{r.Name} should be active"));
    }

    [Fact]
    public async Task Every_seeded_role_satisfies_the_permission_dependencies()
    {
        var actual = await ReadSystemRoleGrantsAsync();

        foreach (var (role, grants) in actual)
        {
            var missing = PermissionDependencies.FindMissing(grants);
            Assert.True(missing.Count == 0,
                $"{role} is missing: {string.Join("; ", missing.Select(m => $"{m.Permission} needs {m.Missing}"))}");
        }
    }

    [Fact]
    public async Task Admin_gets_the_whole_catalog_from_code_with_no_grant_rows()
    {
        using (var scope = _factory.Services.CreateScope())
        {
            var db = scope.ServiceProvider.GetRequiredService<ApplicationDbContext>();
            var adminRows = await db.RolePermissions.CountAsync(rp => rp.Role.Name == RoleNames.Admin);
            Assert.Equal(0, adminRows);
        }

        using var request = await RoleSessions.RequestAsAsync(_factory, RoleNames.Admin, HttpMethod.Get, "/api/auth/me");
        using var client = _factory.CreateClient();
        var response = await client.SendAsync(request);
        Assert.Equal(HttpStatusCode.OK, response.StatusCode);

        var me = await response.Content.ReadFromJsonAsync<CurrentUserResponse>();
        Assert.True(Permissions.All.SetEquals(me!.Permissions));
    }

    private async Task<Dictionary<string, SortedSet<string>>> ReadSystemRoleGrantsAsync()
    {
        using var scope = _factory.Services.CreateScope();
        var db = scope.ServiceProvider.GetRequiredService<ApplicationDbContext>();

        var rows = await db.RolePermissions.AsNoTracking()
            .Where(rp => RoleNames.All.Contains(rp.Role.Name))
            .Select(rp => new { Role = rp.Role.Name, Permission = rp.Permission.Name })
            .ToListAsync();

        return rows
            .GroupBy(r => r.Role)
            .ToDictionary(g => g.Key, g => new SortedSet<string>(g.Select(r => r.Permission), StringComparer.Ordinal));
    }
}
