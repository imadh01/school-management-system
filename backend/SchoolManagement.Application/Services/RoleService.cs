using SchoolManagement.Application.DTOs.Roles;
using SchoolManagement.Application.Interfaces;
using SchoolManagement.Domain.Constants;
using SchoolManagement.Domain.Entities;
using SchoolManagement.Domain.Exceptions;

namespace SchoolManagement.Application.Services;

/// <summary>
/// Roles and their permissions (Batch E1). Every change goes through these guards:
///
///   1. OWN ROLE: you cannot change a role you hold (its permissions, name or status). Nobody can
///      raise their own access, and nobody can lock themselves out by accident.
///   2. NO ESCALATION: every permission you add or remove must be one you hold yourself, so a
///      custom role can never exceed its creator. "What you hold" is the same answer the
///      authorization handler uses, so Admin (whole catalog in code) is never wrongly blocked.
///      Activating or deactivating a role counts as changing all of its permissions.
///   3. ADMIN ROLE: its permissions can't be edited (it always has everything) and it can't be
///      deactivated.
///   4. SYSTEM ROLES: can't be renamed, and no other role may take a system name (code looks
///      some of them up by name, e.g. "Teacher" when creating a teacher).
///   5. DEPENDENCIES: a permission is only granted together with what it needs (PermissionDependencies).
///   6. CONCURRENCY: edits carry the RowVersion the client loaded; a stale one gets 409. Changing
///      only the permission list still updates the role row, so the check always applies.
///
/// After a change commits, the permission cache is cleared for every user holding the role.
/// (The cache is in-process: this assumes a single API instance.)
/// </summary>
public class RoleService : IRoleService
{
    private readonly IRoleRepository _roles;
    private readonly ICurrentUser _currentUser;
    private readonly ICurrentUserPermissions _callerPermissions;
    private readonly IConcurrencyGuard _concurrency;
    private readonly IPermissionCacheService _permissionCache;

    public RoleService(
        IRoleRepository roles,
        ICurrentUser currentUser,
        ICurrentUserPermissions callerPermissions,
        IConcurrencyGuard concurrency,
        IPermissionCacheService permissionCache)
    {
        _roles = roles;
        _currentUser = currentUser;
        _callerPermissions = callerPermissions;
        _concurrency = concurrency;
        _permissionCache = permissionCache;
    }

    // ------------------------------------------------------------------ reads

    public IReadOnlyList<PermissionModuleResponse> GetPermissionCatalog() =>
        Permissions.Definitions
            .GroupBy(d => d.Module)
            .Select(g => new PermissionModuleResponse(
                g.Key,
                g.Select(d => new PermissionResponse(
                        d.Name, d.Description, PermissionDependencies.RequiredFor(d.Name).ToList()))
                    .ToList()))
            .ToList();

    public async Task<IReadOnlyList<RoleSummaryResponse>> GetAllAsync(CancellationToken cancellationToken)
    {
        var rows = await _roles.GetListAsync(cancellationToken);
        return rows
            .Select(r =>
            {
                var isAdmin = IsAdminRole(r.Name);
                return new RoleSummaryResponse(
                    r.Id, r.Name, r.Description, r.IsSystem, r.IsActive,
                    HasAllPermissions: isAdmin,
                    r.UserCount,
                    PermissionCount: isAdmin ? Permissions.All.Count : r.PermissionCount);
            })
            .ToList();
    }

    public async Task<RoleResponse> GetByIdAsync(int id, CancellationToken cancellationToken) =>
        ToResponse(await LoadAsync(id, cancellationToken));

    // ------------------------------------------------------------------ create

    public async Task<RoleResponse> CreateAsync(CreateRoleRequest request, CancellationToken cancellationToken)
    {
        var name = NormalizeName(request.Name);
        await GuardNameAsync(name, excludeRoleId: null, cancellationToken);

        var requested = Distinct(request.Permissions);
        var permissions = await LoadPermissionsAsync(requested, cancellationToken);
        GuardDependencies(requested);
        await GuardCallerHoldsAsync(requested, "grant", cancellationToken);

        var role = new Role
        {
            Name = name,
            Description = NormalizeDescription(request.Description),
            IsSystem = false,
            IsActive = true,
        };
        foreach (var permission in permissions)
            role.RolePermissions.Add(new RolePermission { Permission = permission });

        await _roles.AddAsync(role, cancellationToken);
        // A new role has no users yet: nothing to clear from the cache.

        return ToResponse(role);
    }

    // ------------------------------------------------------------------ edit

    public async Task<RoleResponse> UpdateAsync(int id, UpdateRoleRequest request, CancellationToken cancellationToken)
    {
        var role = await LoadAsync(id, cancellationToken);
        await GuardNotOwnRoleAsync(role, cancellationToken);

        var name = NormalizeName(request.Name);
        if (role.IsSystem)
        {
            if (!string.Equals(name, role.Name, StringComparison.Ordinal))
                throw new BusinessRuleException($"'{role.Name}' is a system role and cannot be renamed.");
        }
        else if (!string.Equals(name, role.Name, StringComparison.Ordinal))
        {
            await GuardNameAsync(name, role.Id, cancellationToken);
        }

        _concurrency.Expect(role, request.RowVersion);
        role.Name = name;
        role.Description = NormalizeDescription(request.Description);

        await SaveAndInvalidateAsync(role, cancellationToken);
        return ToResponse(role);
    }

    public async Task<RoleResponse> SetPermissionsAsync(
        int id, SetRolePermissionsRequest request, CancellationToken cancellationToken)
    {
        var role = await LoadAsync(id, cancellationToken);
        await GuardNotOwnRoleAsync(role, cancellationToken);

        if (IsAdminRole(role.Name))
            throw new BusinessRuleException("The Admin role always has every permission; its permissions cannot be changed.");

        var requested = Distinct(request.Permissions);
        var permissions = await LoadPermissionsAsync(requested, cancellationToken);
        GuardDependencies(requested);

        var current = role.RolePermissions.Select(rp => rp.Permission.Name).ToHashSet(StringComparer.Ordinal);
        var changed = current.Except(requested).Concat(requested.Except(current)).ToList();
        await GuardCallerHoldsAsync(changed, "add or remove", cancellationToken);

        _concurrency.Expect(role, request.RowVersion); // also marks the role row as updated

        foreach (var removed in role.RolePermissions.Where(rp => !requested.Contains(rp.Permission.Name)).ToList())
            role.RolePermissions.Remove(removed);
        foreach (var added in permissions.Where(p => !current.Contains(p.Name)))
            role.RolePermissions.Add(new RolePermission { RoleId = role.Id, Permission = added });

        await SaveAndInvalidateAsync(role, cancellationToken);
        return ToResponse(role);
    }

    public async Task<RoleResponse> ChangeStatusAsync(
        int id, ChangeRoleStatusRequest request, CancellationToken cancellationToken)
    {
        var role = await LoadAsync(id, cancellationToken);
        await GuardNotOwnRoleAsync(role, cancellationToken);

        if (IsAdminRole(role.Name) && !request.IsActive)
            throw new BusinessRuleException("The Admin role cannot be deactivated.");

        // Switching a role on or off grants or removes all of its permissions at once.
        var rolePermissions = role.RolePermissions.Select(rp => rp.Permission.Name).ToList();
        await GuardCallerHoldsAsync(rolePermissions, request.IsActive ? "activate a role granting" : "deactivate a role granting", cancellationToken);

        _concurrency.Expect(role, request.RowVersion);
        role.IsActive = request.IsActive;

        await SaveAndInvalidateAsync(role, cancellationToken);
        return ToResponse(role);
    }

    // ------------------------------------------------------------------ guards

    private async Task GuardNotOwnRoleAsync(Role role, CancellationToken cancellationToken)
    {
        var callerId = _currentUser.UserId
                       ?? throw new ForbiddenException("Your session is invalid. Please sign in again.");

        if (await _roles.UserHoldsRoleAsync(callerId, role.Id, cancellationToken))
            throw new ForbiddenException($"You cannot change the '{role.Name}' role because you hold it yourself.");
    }

    private async Task GuardCallerHoldsAsync(
        IReadOnlyCollection<string> permissions, string action, CancellationToken cancellationToken)
    {
        if (permissions.Count == 0)
            return;

        var held = await _callerPermissions.GetPermissionsAsync(cancellationToken);
        var notHeld = permissions.Where(p => !held.Contains(p)).OrderBy(p => p, StringComparer.Ordinal).ToList();
        if (notHeld.Count > 0)
            throw new ForbiddenException(
                $"You cannot {action} permissions you do not hold yourself: {string.Join(", ", notHeld)}.");
    }

    private async Task GuardNameAsync(string name, int? excludeRoleId, CancellationToken cancellationToken)
    {
        if (RoleNames.All.Contains(name, StringComparer.OrdinalIgnoreCase))
            throw new BusinessRuleException($"'{name}' is reserved for a system role.");

        if (await _roles.NameExistsAsync(name, excludeRoleId, cancellationToken))
            throw new ConflictException($"A role named '{name}' already exists.");
    }

    private static void GuardDependencies(IReadOnlyCollection<string> requested)
    {
        var missing = PermissionDependencies.FindMissing(requested);
        if (missing.Count > 0)
            throw new BusinessRuleException(
                "Some permissions need others granted with them: " +
                string.Join("; ", missing.Select(m => $"{m.Permission} needs {m.Missing}")) + ".");
    }

    // ------------------------------------------------------------------ helpers

    private async Task<Role> LoadAsync(int id, CancellationToken cancellationToken) =>
        await _roles.GetWithPermissionsAsync(id, cancellationToken)
        ?? throw new NotFoundException($"Role {id} could not be found.");

    /// <summary>Unknown names are refused (422) instead of being silently dropped.</summary>
    private async Task<List<Permission>> LoadPermissionsAsync(
        IReadOnlyCollection<string> names, CancellationToken cancellationToken)
    {
        var unknown = names.Where(n => !Permissions.All.Contains(n)).OrderBy(n => n, StringComparer.Ordinal).ToList();
        if (unknown.Count > 0)
            throw new BusinessRuleException($"Unknown permission(s): {string.Join(", ", unknown)}.");

        var permissions = await _roles.GetPermissionsByNamesAsync(names, cancellationToken);
        if (permissions.Count != names.Count)
        {
            // In the code catalog but missing from the table: a migration was not applied.
            var missingRows = names.Except(permissions.Select(p => p.Name)).OrderBy(n => n, StringComparer.Ordinal);
            throw new BusinessRuleException(
                $"These permissions are not in the database yet (pending migration?): {string.Join(", ", missingRows)}.");
        }

        return permissions;
    }

    private async Task SaveAndInvalidateAsync(Role role, CancellationToken cancellationToken)
    {
        await _roles.SaveChangesAsync(cancellationToken);

        // AFTER the commit, so no request can reload the old grants into the cache in between.
        foreach (var userId in await _roles.GetUserIdsInRoleAsync(role.Id, cancellationToken))
            _permissionCache.InvalidateUser(userId);
    }

    private static RoleResponse ToResponse(Role role)
    {
        var isAdmin = IsAdminRole(role.Name);
        var permissions = isAdmin
            ? Permissions.All.OrderBy(p => p, StringComparer.Ordinal).ToList()
            : role.RolePermissions.Select(rp => rp.Permission.Name).OrderBy(p => p, StringComparer.Ordinal).ToList();

        return new RoleResponse(
            role.Id, role.Name, role.Description, role.IsSystem, role.IsActive,
            HasAllPermissions: isAdmin,
            permissions,
            Convert.ToBase64String(role.RowVersion));
    }

    private static bool IsAdminRole(string name) => string.Equals(name, RoleNames.Admin, StringComparison.Ordinal);

    private static HashSet<string> Distinct(IEnumerable<string>? names) =>
        (names ?? Enumerable.Empty<string>())
            .Where(n => !string.IsNullOrWhiteSpace(n))
            .Select(n => n.Trim())
            .ToHashSet(StringComparer.Ordinal);

    /// <summary>Trims and collapses inner whitespace: "  Exam   Cell " → "Exam Cell".</summary>
    private static string NormalizeName(string name) =>
        string.Join(' ', (name ?? string.Empty).Split(' ', StringSplitOptions.RemoveEmptyEntries | StringSplitOptions.TrimEntries));

    private static string? NormalizeDescription(string? description) =>
        string.IsNullOrWhiteSpace(description) ? null : description.Trim();
}
