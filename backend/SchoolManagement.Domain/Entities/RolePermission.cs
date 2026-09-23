namespace SchoolManagement.Domain.Entities;

/// <summary>
/// Junction: a Role's set of Permissions. No extra columns today, but kept
/// as an explicit entity (not an implicit EF many-to-many) so both
/// junction tables in this module are configured the same way.
/// </summary>
public class RolePermission
{
    public int RoleId { get; set; }
    public Role Role { get; set; } = null!;

    public int PermissionId { get; set; }
    public Permission Permission { get; set; } = null!;
}
