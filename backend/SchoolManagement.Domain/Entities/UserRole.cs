namespace SchoolManagement.Domain.Entities;

/// <summary>
/// Junction: a User can hold more than one Role. Explicit entity (rather
/// than an implicit EF many-to-many) because it carries AssignedAt.
/// </summary>
public class UserRole
{
    public int UserId { get; set; }
    public User User { get; set; } = null!;

    public int RoleId { get; set; }
    public Role Role { get; set; } = null!;

    public DateTime AssignedAt { get; set; }
}
