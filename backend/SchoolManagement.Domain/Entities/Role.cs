namespace SchoolManagement.Domain.Entities;

/// <summary>
/// A named set of permissions. Six system roles (Admin, Supervisor, Clerk, Teacher, Student,
/// Parent) exist from the start; admins can add custom roles. A role's access is data
/// (RolePermissions), except Admin, which holds the whole catalog in code.
///
/// Roles are never deleted (they appear in audit history); they are deactivated instead.
/// An inactive role grants nothing to the users who hold it.
/// </summary>
public class Role : IHasRowVersion
{
    public int Id { get; set; }

    public string Name { get; set; } = string.Empty;
    public string? Description { get; set; }

    /// <summary>One of the six seeded roles: cannot be renamed. Code looks some of them up by name.</summary>
    public bool IsSystem { get; set; }

    /// <summary>Inactive roles grant no permissions. Admin can never be deactivated.</summary>
    public bool IsActive { get; set; } = true;

    // ── Audit (UTC, set by the SaveChanges interceptor) ───────────────
    public DateTime CreatedAt { get; set; }
    public DateTime UpdatedAt { get; set; }
    public int? CreatedBy { get; set; }
    public int? UpdatedBy { get; set; }

    /// <summary>
    /// Optimistic concurrency: two admins editing the same role → the second save gets 409.
    /// Changing only the permission list still updates this row (UpdatedAt), so the check applies.
    /// </summary>
    public byte[] RowVersion { get; set; } = Array.Empty<byte>();

    public ICollection<UserRole> UserRoles { get; set; } = new List<UserRole>();
    public ICollection<RolePermission> RolePermissions { get; set; } = new List<RolePermission>();
}
