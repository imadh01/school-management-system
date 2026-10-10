namespace SchoolManagement.Application.DTOs.Roles;

// ---------- Permission catalog (GET /api/permissions) ----------

/// <param name="Requires">Permissions that must be granted together with this one (see PermissionDependencies).</param>
public record PermissionResponse(string Name, string Description, IReadOnlyList<string> Requires);

public record PermissionModuleResponse(string Module, IReadOnlyList<PermissionResponse> Permissions);

// ---------- Roles ----------

/// <param name="HasAllPermissions">True for Admin, which holds the whole catalog in code.</param>
/// <param name="UserCount">Users (not deleted) holding this role.</param>
public record RoleSummaryResponse(
    int Id,
    string Name,
    string? Description,
    bool IsSystem,
    bool IsActive,
    bool HasAllPermissions,
    int UserCount,
    int PermissionCount);

/// <param name="RowVersion">Send it back unchanged with every edit; a stale value gets 409.</param>
public record RoleResponse(
    int Id,
    string Name,
    string? Description,
    bool IsSystem,
    bool IsActive,
    bool HasAllPermissions,
    IReadOnlyList<string> Permissions,
    string RowVersion);

public record CreateRoleRequest(string Name, string? Description, List<string> Permissions);

/// <summary>Name and description. A system role's name cannot change.</summary>
public record UpdateRoleRequest(string Name, string? Description, string RowVersion);

/// <summary>Replaces the role's whole permission set.</summary>
public record SetRolePermissionsRequest(List<string> Permissions, string RowVersion);

public record ChangeRoleStatusRequest(bool IsActive, string RowVersion);
