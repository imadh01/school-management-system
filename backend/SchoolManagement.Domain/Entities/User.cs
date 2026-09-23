namespace SchoolManagement.Domain.Entities;

/// <summary>
/// One row per login-capable person, regardless of role.
/// Role-specific profile data (Student/Teacher/Parent) lives in later
/// modules and links back here via a nullable UserId FK.
/// </summary>
public class User
{
    public int Id { get; set; }

    public string Username { get; set; } = string.Empty;
    public string Email { get; set; } = string.Empty;
    public string PasswordHash { get; set; } = string.Empty;

    /// <summary>Active / Inactive / Suspended — enforced by a DB check constraint.</summary>
    public string Status { get; set; } = "Active";

    public DateTime? LastLoginAt { get; set; }

    // Audit (UTC, set by SaveChanges interceptor — not a DB default)
    public DateTime CreatedAt { get; set; }
    public DateTime UpdatedAt { get; set; }
    public int? CreatedBy { get; set; }
    public int? UpdatedBy { get; set; }

    // Soft delete — only Users has this in Module 1 (referenced by nearly everything later)
    public bool IsDeleted { get; set; }
    public DateTime? DeletedAt { get; set; }
    public int? DeletedBy { get; set; }

    public ICollection<UserRole> UserRoles { get; set; } = new List<UserRole>();
}
