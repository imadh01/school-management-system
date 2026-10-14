using System.IdentityModel.Tokens.Jwt;
using System.Security.Claims;
using Microsoft.AspNetCore.Http;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.DependencyInjection;
using SchoolManagement.Application.DTOs.Auth;
using SchoolManagement.Application.Interfaces;
using SchoolManagement.Domain.Constants;
using SchoolManagement.Domain.Entities;
using SchoolManagement.Domain.Exceptions;
using SchoolManagement.Infrastructure.Persistence;
using SchoolManagement.Tests.Support;
using Xunit;

namespace SchoolManagement.Tests.DatabaseRules;

/// <summary>
/// E2 security fix (Problem 2): the school is never left without an active Admin, whatever account
/// change is made (deactivate, suspend, demote, delete) and even when two admins act at the same moment.
/// Also: nobody changes their own account here, and every change ends the user's sessions.
///
/// These tests call <see cref="IUserAccountService"/> directly (the Users endpoints that will use it come in
/// E2). "Who is acting" is set the way a request sets it: an HttpContext carrying the user's id.
/// The "Database" collection has no Admin users of its own, so each test first sets every existing
/// Admin to Inactive and then creates exactly the admins it needs.
/// </summary>
[Collection("Database")]
public class UserAccountRulesTests
{
    private readonly SqlServerWebApplicationFactory _factory;

    public UserAccountRulesTests(SqlServerWebApplicationFactory factory) => _factory = factory;

    // ------------------------------------------------------------------ the last active Admin

    [Theory]
    [InlineData(UserStatuses.Inactive)]
    [InlineData(UserStatuses.Suspended)]
    public async Task The_last_active_admin_cannot_be_deactivated_or_suspended(string status)
    {
        await ResetAdminsAsync();
        var lastAdmin = await NewUserAsync(RoleNames.Admin);
        var clerk = await NewUserAsync(RoleNames.Clerk);

        var error = await Assert.ThrowsAsync<BusinessRuleException>(
            () => ActAsync(clerk, a => a.ChangeStatusAsync(lastAdmin, status, default)));

        Assert.Contains("last active Admin", error.Message);
        Assert.Equal(UserStatuses.Active, await StatusOfAsync(lastAdmin));
    }

    [Fact]
    public async Task The_last_active_admin_cannot_be_demoted()
    {
        await ResetAdminsAsync();
        var lastAdmin = await NewUserAsync(RoleNames.Admin);
        var clerk = await NewUserAsync(RoleNames.Clerk); // holds every Clerk permission, so the escalation check passes

        var error = await Assert.ThrowsAsync<BusinessRuleException>(
            () => ActAsync(clerk, a => a.ChangeRoleAsync(lastAdmin, RoleNames.Clerk, default)));

        Assert.Contains("last active Admin", error.Message);
        Assert.Equal(new[] { RoleNames.Admin }, await RolesOfAsync(lastAdmin));
    }

    [Fact]
    public async Task The_last_active_admin_cannot_be_deleted()
    {
        await ResetAdminsAsync();
        var lastAdmin = await NewUserAsync(RoleNames.Admin);
        var clerk = await NewUserAsync(RoleNames.Clerk);

        var error = await Assert.ThrowsAsync<BusinessRuleException>(
            () => ActAsync(clerk, a => a.DeleteAsync(lastAdmin, default)));

        Assert.Contains("last active Admin", error.Message);
        using var scope = _factory.Services.CreateScope();
        var db = scope.ServiceProvider.GetRequiredService<ApplicationDbContext>();
        Assert.True(await db.Users.AnyAsync(u => u.Id == lastAdmin)); // the query filter hides deleted users
    }

    [Fact]
    public async Task An_inactive_or_suspended_admin_does_not_count_as_another_admin()
    {
        await ResetAdminsAsync();
        var lastActive = await NewUserAsync(RoleNames.Admin);
        await NewUserAsync(RoleNames.Admin, UserStatuses.Inactive);
        await NewUserAsync(RoleNames.Admin, UserStatuses.Suspended);
        var clerk = await NewUserAsync(RoleNames.Clerk);

        await Assert.ThrowsAsync<BusinessRuleException>(
            () => ActAsync(clerk, a => a.ChangeStatusAsync(lastActive, UserStatuses.Inactive, default)));
    }

    [Fact]
    public async Task With_another_active_admin_an_admin_can_be_deactivated_and_their_sessions_end()
    {
        await ResetAdminsAsync();
        var adminA = await NewUserAsync(RoleNames.Admin);
        var adminB = await NewUserAsync(RoleNames.Admin);
        var stampBefore = await StampOfAsync(adminB);
        await IssueRefreshTokenAsync(adminB);

        await ActAsync(adminA, a => a.ChangeStatusAsync(adminB, UserStatuses.Inactive, default));

        Assert.Equal(UserStatuses.Inactive, await StatusOfAsync(adminB));
        Assert.NotEqual(stampBefore, await StampOfAsync(adminB)); // access tokens stop at once
        using var scope = _factory.Services.CreateScope();
        var db = scope.ServiceProvider.GetRequiredService<ApplicationDbContext>();
        Assert.False(await db.RefreshTokens.AnyAsync(t => t.UserId == adminB && t.RevokedAt == null));
    }

    [Fact]
    public async Task Two_admins_removing_each_other_at_the_same_moment_leave_exactly_one_active_admin()
    {
        // Without the lock both requests would see "one other active Admin", both would succeed, and
        // the school would have none. Several rounds, because a race does not always show on the first try.
        for (var round = 0; round < 5; round++)
        {
            await ResetAdminsAsync();
            var adminA = await NewUserAsync(RoleNames.Admin);
            var adminB = await NewUserAsync(RoleNames.Admin);

            var results = await Task.WhenAll(
                Task.Run(() => TryActAsync(adminA, a => a.ChangeStatusAsync(adminB, UserStatuses.Inactive, default))),
                Task.Run(() => TryActAsync(adminB, a => a.DeleteAsync(adminA, default))));

            Assert.Equal(1, results.Count(r => r is null));                         // one succeeded
            Assert.Equal(1, results.Count(r => r is BusinessRuleException));        // the other was refused
            Assert.Equal(1, await CountActiveAdminsAsync());
        }
    }

    // ------------------------------------------------------------------ your own account

    [Fact]
    public async Task Nobody_changes_their_own_account_here()
    {
        await ResetAdminsAsync();
        var admin = await NewUserAsync(RoleNames.Admin);
        await NewUserAsync(RoleNames.Admin); // so "last admin" is not the reason

        await Assert.ThrowsAsync<BusinessRuleException>(
            () => ActAsync(admin, a => a.ChangeStatusAsync(admin, UserStatuses.Inactive, default)));
        await Assert.ThrowsAsync<BusinessRuleException>(
            () => ActAsync(admin, a => a.ChangeRoleAsync(admin, RoleNames.Clerk, default)));
        await Assert.ThrowsAsync<BusinessRuleException>(
            () => ActAsync(admin, a => a.DeleteAsync(admin, default)));
        await Assert.ThrowsAsync<BusinessRuleException>(
            () => ActAsync(admin, a => a.SetTemporaryPasswordAsync(admin, "Temp@12345", default)));
    }

    [Fact]
    public async Task A_temporary_password_forces_a_change_and_ends_the_sessions()
    {
        await ResetAdminsAsync();
        var admin = await NewUserAsync(RoleNames.Admin);
        var clerk = await NewUserAsync(RoleNames.Clerk);
        var stampBefore = await StampOfAsync(clerk);

        await ActAsync(admin, a => a.SetTemporaryPasswordAsync(clerk, "Temp@12345", default));

        using var scope = _factory.Services.CreateScope();
        var db = scope.ServiceProvider.GetRequiredService<ApplicationDbContext>();
        var user = await db.Users.SingleAsync(u => u.Id == clerk);
        Assert.True(user.MustChangePassword);
        Assert.NotEqual(stampBefore, user.SecurityStamp);
    }

    // ------------------------------------------------------------------ helpers

    /// <summary>Runs an account change as <paramref name="actorId"/>, in its own scope like a request.</summary>
    private async Task ActAsync(int actorId, Func<IUserAccountService, Task> action)
    {
        using var scope = _factory.Services.CreateScope();
        var accessor = scope.ServiceProvider.GetRequiredService<IHttpContextAccessor>();
        accessor.HttpContext = new DefaultHttpContext
        {
            User = new ClaimsPrincipal(new ClaimsIdentity(
                new[] { new Claim(JwtRegisteredClaimNames.Sub, actorId.ToString()) }, "Test")),
        };
        try
        {
            await action(scope.ServiceProvider.GetRequiredService<IUserAccountService>());
        }
        finally
        {
            accessor.HttpContext = null;
        }
    }

    private async Task<Exception?> TryActAsync(int actorId, Func<IUserAccountService, Task> action)
    {
        try
        {
            await ActAsync(actorId, action);
            return null;
        }
        catch (Exception ex)
        {
            return ex;
        }
    }

    /// <summary>Every existing Admin becomes Inactive, so the test controls who the active admins are.</summary>
    private async Task ResetAdminsAsync()
    {
        using var scope = _factory.Services.CreateScope();
        var db = scope.ServiceProvider.GetRequiredService<ApplicationDbContext>();
        await db.Database.ExecuteSqlRawAsync(
            "UPDATE u SET u.[Status] = N'Inactive' FROM [Users] u " +
            "JOIN [UserRoles] ur ON ur.[UserId] = u.[Id] JOIN [Roles] r ON r.[Id] = ur.[RoleId] " +
            "WHERE r.[Name] = N'Admin';");
        scope.ServiceProvider.GetRequiredService<IPermissionCacheService>().InvalidateAll();
    }

    private async Task<int> NewUserAsync(string roleName, string status = UserStatuses.Active)
    {
        using var scope = _factory.Services.CreateScope();
        var db = scope.ServiceProvider.GetRequiredService<ApplicationDbContext>();
        var role = await db.Roles.SingleAsync(r => r.Name == roleName);
        var name = "acct_" + TestData.Unique();
        var user = new User
        {
            Username = name,
            Email = $"{name}@test.local",
            PasswordHash = "not-a-real-hash",
            Status = status,
            SecurityStamp = Guid.NewGuid(),
        };
        user.UserRoles.Add(new UserRole { RoleId = role.Id, AssignedAt = DateTime.UtcNow });
        db.Users.Add(user);
        await db.SaveChangesAsync();
        return user.Id;
    }

    private async Task IssueRefreshTokenAsync(int userId)
    {
        using var scope = _factory.Services.CreateScope();
        var db = scope.ServiceProvider.GetRequiredService<ApplicationDbContext>();
        var user = await db.Users.Include(u => u.UserRoles).SingleAsync(u => u.Id == userId);
        await scope.ServiceProvider.GetRequiredService<IRefreshTokenService>().IssueAsync(user, ClientInfo.Unknown, default);
    }

    private async Task<string> StatusOfAsync(int userId)
    {
        using var scope = _factory.Services.CreateScope();
        var db = scope.ServiceProvider.GetRequiredService<ApplicationDbContext>();
        return await db.Users.IgnoreQueryFilters().Where(u => u.Id == userId).Select(u => u.Status).SingleAsync();
    }

    private async Task<Guid> StampOfAsync(int userId)
    {
        using var scope = _factory.Services.CreateScope();
        var db = scope.ServiceProvider.GetRequiredService<ApplicationDbContext>();
        return await db.Users.IgnoreQueryFilters().Where(u => u.Id == userId).Select(u => u.SecurityStamp).SingleAsync();
    }

    private async Task<string[]> RolesOfAsync(int userId)
    {
        using var scope = _factory.Services.CreateScope();
        var db = scope.ServiceProvider.GetRequiredService<ApplicationDbContext>();
        return await db.UserRoles.Where(ur => ur.UserId == userId).Select(ur => ur.Role.Name).ToArrayAsync();
    }

    private async Task<int> CountActiveAdminsAsync()
    {
        using var scope = _factory.Services.CreateScope();
        return await scope.ServiceProvider.GetRequiredService<IUserRepository>().CountActiveAdminsAsync(null, default);
    }
}
