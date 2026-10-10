using Microsoft.AspNetCore.Authorization;

namespace SchoolManagement.Infrastructure.Security;

/// <summary>
/// Requires the authenticated user to hold a single named permission.
/// Resolved server-side from the permission cache — NOT from JWT claims.
/// </summary>
public class PermissionRequirement : IAuthorizationRequirement
{
    public string PermissionName { get; }
    public PermissionRequirement(string permissionName) => PermissionName = permissionName;
}

/// <summary>
/// Requires the authenticated user to hold ANY ONE of the listed permissions.
/// Used for composite policies like Attendance.Write (Manage OR Mark).
/// </summary>
public class AnyPermissionRequirement : IAuthorizationRequirement
{
    public IReadOnlyList<string> PermissionNames { get; }
    public AnyPermissionRequirement(params string[] permissionNames) => PermissionNames = permissionNames;
}
