// Batch E1: RoleService guards in isolation (repository, caller and cache are mocked).
// The HTTP + database behaviour is covered by RolesApiTests.

using Moq;
using SchoolManagement.Application.DTOs.Roles;
using SchoolManagement.Application.Interfaces;
using SchoolManagement.Application.Services;
using SchoolManagement.Domain.Constants;
using SchoolManagement.Domain.Entities;
using SchoolManagement.Domain.Exceptions;
using Xunit;

namespace SchoolManagement.Tests.UnitTests;

public class RoleServiceTests
{
    private const int CallerId = 100;
    private const string Version = "AAAAAAAAB9E="; // any valid 8-byte rowversion

    private readonly Mock<IRoleRepository> _roles = new();
    private readonly Mock<ICurrentUser> _currentUser = new();
    private readonly Mock<ICurrentUserPermissions> _callerPermissions = new();
    private readonly Mock<IConcurrencyGuard> _concurrency = new();
    private readonly Mock<IPermissionCacheService> _cache = new();
    private readonly RoleService _sut;

    private readonly Dictionary<string, Permission> _permissionRows;

    public RoleServiceTests()
    {
        _currentUser.Setup(u => u.UserId).Returns(CallerId);
        CallerHolds(Permissions.All.ToArray()); // Admin-like by default

        var id = 1;
        _permissionRows = Permissions.All.ToDictionary(n => n, n => new Permission { Id = id++, Name = n });
        _roles.Setup(r => r.GetPermissionsByNamesAsync(It.IsAny<IReadOnlyCollection<string>>(), It.IsAny<CancellationToken>()))
            .ReturnsAsync((IReadOnlyCollection<string> names, CancellationToken _) => names.Select(n => _permissionRows[n]).ToList());
        _roles.Setup(r => r.GetUserIdsInRoleAsync(It.IsAny<int>(), It.IsAny<CancellationToken>())).ReturnsAsync(new List<int>());

        _sut = new RoleService(_roles.Object, _currentUser.Object, _callerPermissions.Object, _concurrency.Object, _cache.Object);
    }

    // ------------------------------------------------------------------ helpers

    private void CallerHolds(params string[] permissions) =>
        _callerPermissions.Setup(p => p.GetPermissionsAsync(It.IsAny<CancellationToken>()))
            .ReturnsAsync(permissions.ToHashSet());

    private Role GivenRole(int id, string name, bool isSystem = false, params string[] permissions)
    {
        var role = new Role { Id = id, Name = name, IsSystem = isSystem, IsActive = true, RowVersion = new byte[8] };
        foreach (var p in permissions)
            role.RolePermissions.Add(new RolePermission { RoleId = id, Permission = _permissionRows[p] });
        _roles.Setup(r => r.GetWithPermissionsAsync(id, It.IsAny<CancellationToken>())).ReturnsAsync(role);
        return role;
    }

    private void CallerHoldsRole(int roleId) =>
        _roles.Setup(r => r.UserHoldsRoleAsync(CallerId, roleId, It.IsAny<CancellationToken>())).ReturnsAsync(true);

    // ------------------------------------------------------------------ create

    [Theory]
    [InlineData("Teacher")]
    [InlineData("teacher")]
    [InlineData("  ADMIN  ")]
    public async Task Create_refuses_a_system_role_name_in_any_case(string name)
    {
        await Assert.ThrowsAsync<BusinessRuleException>(() =>
            _sut.CreateAsync(new CreateRoleRequest(name, null, new()), default));
    }

    [Fact]
    public async Task Create_refuses_a_duplicate_name()
    {
        _roles.Setup(r => r.NameExistsAsync("Exam Cell", null, It.IsAny<CancellationToken>())).ReturnsAsync(true);

        await Assert.ThrowsAsync<ConflictException>(() =>
            _sut.CreateAsync(new CreateRoleRequest("  Exam   Cell ", null, new()), default));
    }

    [Fact]
    public async Task Create_refuses_unknown_permissions()
    {
        await Assert.ThrowsAsync<BusinessRuleException>(() =>
            _sut.CreateAsync(new CreateRoleRequest("Exam Cell", null, new() { "Students.Fly" }), default));
    }

    [Fact]
    public async Task Create_refuses_a_permission_without_its_dependencies()
    {
        var ex = await Assert.ThrowsAsync<BusinessRuleException>(() =>
            _sut.CreateAsync(new CreateRoleRequest("Exam Cell", null, new() { Permissions.StudentsCreate }), default));

        Assert.Contains(Permissions.ClassSectionsView, ex.Message);
    }

    [Fact]
    public async Task Create_refuses_granting_what_the_caller_does_not_hold()
    {
        CallerHolds(Permissions.RolesCreate, Permissions.RolesView, Permissions.StudentsView);

        var ex = await Assert.ThrowsAsync<ForbiddenException>(() =>
            _sut.CreateAsync(new CreateRoleRequest("Exam Cell", null,
                new() { Permissions.StudentsView, Permissions.ParentsView }), default));

        Assert.Contains(Permissions.ParentsView, ex.Message);
        _roles.Verify(r => r.AddAsync(It.IsAny<Role>(), It.IsAny<CancellationToken>()), Times.Never);
    }

    [Fact]
    public async Task Admin_can_grant_any_permission_because_it_holds_the_whole_catalog()
    {
        // Same source of truth as authorization: Admin's set is Permissions.All, not database rows.
        var result = await _sut.CreateAsync(new CreateRoleRequest("Exam Cell", "Exams", new()
        {
            Permissions.StudentsView, Permissions.StudentsEdit, Permissions.ClassSectionsView,
        }), default);

        Assert.False(result.IsSystem);
        Assert.Equal(3, result.Permissions.Count);
        _roles.Verify(r => r.AddAsync(It.Is<Role>(x => x.Name == "Exam Cell" && !x.IsSystem), It.IsAny<CancellationToken>()), Times.Once);
    }

    // ------------------------------------------------------------------ own role

    [Fact]
    public async Task Nobody_can_change_a_role_they_hold()
    {
        GivenRole(7, "Exam Cell", false, Permissions.StudentsView);
        CallerHoldsRole(7);

        await Assert.ThrowsAsync<ForbiddenException>(() => _sut.SetPermissionsAsync(7,
            new SetRolePermissionsRequest(new() { Permissions.StudentsView }, Version), default));
        await Assert.ThrowsAsync<ForbiddenException>(() => _sut.UpdateAsync(7,
            new UpdateRoleRequest("Exam Cell 2", null, Version), default));
        await Assert.ThrowsAsync<ForbiddenException>(() => _sut.ChangeStatusAsync(7,
            new ChangeRoleStatusRequest(false, Version), default));
    }

    // ------------------------------------------------------------------ escalation on edit

    [Fact]
    public async Task Adding_a_permission_the_caller_lacks_is_refused()
    {
        GivenRole(7, "Exam Cell", false, Permissions.StudentsView);
        CallerHolds(Permissions.RolesEdit, Permissions.StudentsView);

        await Assert.ThrowsAsync<ForbiddenException>(() => _sut.SetPermissionsAsync(7,
            new SetRolePermissionsRequest(new() { Permissions.StudentsView, Permissions.ParentsView }, Version), default));
    }

    [Fact]
    public async Task Removing_a_permission_the_caller_lacks_is_refused_too()
    {
        GivenRole(7, "Exam Cell", false, Permissions.StudentsView, Permissions.ParentsView);
        CallerHolds(Permissions.RolesEdit, Permissions.StudentsView);

        await Assert.ThrowsAsync<ForbiddenException>(() => _sut.SetPermissionsAsync(7,
            new SetRolePermissionsRequest(new() { Permissions.StudentsView }, Version), default));
    }

    [Fact]
    public async Task Reactivating_a_role_needs_every_permission_it_grants()
    {
        var role = GivenRole(7, "Exam Cell", false, Permissions.StudentsView, Permissions.ParentsView);
        role.IsActive = false;
        CallerHolds(Permissions.RolesEdit, Permissions.StudentsView);

        await Assert.ThrowsAsync<ForbiddenException>(() =>
            _sut.ChangeStatusAsync(7, new ChangeRoleStatusRequest(true, Version), default));
    }

    // ------------------------------------------------------------------ Admin + system roles

    [Fact]
    public async Task The_Admin_role_permissions_cannot_be_edited()
    {
        GivenRole(1, RoleNames.Admin, isSystem: true);

        await Assert.ThrowsAsync<BusinessRuleException>(() => _sut.SetPermissionsAsync(1,
            new SetRolePermissionsRequest(new(), Version), default));
    }

    [Fact]
    public async Task The_Admin_role_cannot_be_deactivated()
    {
        GivenRole(1, RoleNames.Admin, isSystem: true);

        await Assert.ThrowsAsync<BusinessRuleException>(() =>
            _sut.ChangeStatusAsync(1, new ChangeRoleStatusRequest(false, Version), default));
    }

    [Fact]
    public async Task A_system_role_cannot_be_renamed_but_its_description_can_change()
    {
        GivenRole(3, RoleNames.Clerk, isSystem: true);

        await Assert.ThrowsAsync<BusinessRuleException>(() =>
            _sut.UpdateAsync(3, new UpdateRoleRequest("Office Clerk", null, Version), default));

        var ok = await _sut.UpdateAsync(3, new UpdateRoleRequest(RoleNames.Clerk, "Front office", Version), default);
        Assert.Equal("Front office", ok.Description);
    }

    [Fact]
    public async Task A_custom_role_cannot_be_renamed_to_a_system_name()
    {
        GivenRole(7, "Exam Cell");

        await Assert.ThrowsAsync<BusinessRuleException>(() =>
            _sut.UpdateAsync(7, new UpdateRoleRequest("supervisor", null, Version), default));
    }

    // ------------------------------------------------------------------ concurrency + cache

    [Fact]
    public async Task Edits_check_the_loaded_version_and_clear_the_cache_after_saving()
    {
        var role = GivenRole(7, "Exam Cell", false, Permissions.StudentsView);
        _roles.Setup(r => r.GetUserIdsInRoleAsync(7, It.IsAny<CancellationToken>())).ReturnsAsync(new List<int> { 41, 42 });

        var saved = false;
        _roles.Setup(r => r.SaveChangesAsync(It.IsAny<CancellationToken>())).Callback(() => saved = true).Returns(Task.CompletedTask);
        _cache.Setup(c => c.InvalidateUser(It.IsAny<int>()))
            .Callback(() => Assert.True(saved, "the cache must be cleared only AFTER the save committed"));

        await _sut.SetPermissionsAsync(7,
            new SetRolePermissionsRequest(new() { Permissions.StudentsView, Permissions.ParentsView }, Version), default);

        _concurrency.Verify(c => c.Expect(role, Version), Times.Once);
        _cache.Verify(c => c.InvalidateUser(41), Times.Once);
        _cache.Verify(c => c.InvalidateUser(42), Times.Once);
        Assert.Equal(2, role.RolePermissions.Count);
    }

    [Fact]
    public async Task Admin_role_reads_as_holding_the_whole_catalog()
    {
        GivenRole(1, RoleNames.Admin, isSystem: true);

        var admin = await _sut.GetByIdAsync(1, default);

        Assert.True(admin.HasAllPermissions);
        Assert.Equal(Permissions.All.Count, admin.Permissions.Count);
    }
}
