namespace SchoolManagement.Domain.Entities;

/// <summary>
/// Fixed role catalog: Admin, Supervisor, Clerk, Teacher, Student, Parent.
/// A role's actual access is data (RolePermissions), not hardcoded here.
/// </summary>
public class Role
{
    public int Id { get; set; }

    public string Name { get; set; } = string.Empty;
    public string? Description { get; set; }

    public ICollection<UserRole> UserRoles { get; set; } = new List<UserRole>();
    public ICollection<RolePermission> RolePermissions { get; set; } = new List<RolePermission>();
}
