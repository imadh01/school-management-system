namespace SchoolManagement.Domain.Entities;

/// <summary>
/// A single granular, action-level permission (e.g. "Student.Create").
/// Empty at Module 1 sign-off — seeded per-module as each module's
/// actions are designed (Decision #3), never guessed upfront.
/// </summary>
public class Permission
{
    public int Id { get; set; }

    public string Name { get; set; } = string.Empty;
    public string Module { get; set; } = string.Empty;
    public string? Description { get; set; }

    public ICollection<RolePermission> RolePermissions { get; set; } = new List<RolePermission>();
}
