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

    // ── D1: Security infrastructure ────────────────────────────────────
    /// <summary>
    /// Rotated on every security-critical change (password, role, status).
    /// The JWT carries this value; OnTokenValidated compares it against
    /// the cached copy to give immediate revocation.
    /// </summary>
    public Guid SecurityStamp { get; set; } = Guid.NewGuid();

    /// <summary>When true, every endpoint except change-password and /auth/me is blocked.</summary>
    public bool MustChangePassword { get; set; }

    /// <summary>UTC instant until which the account is locked. Null = not locked.</summary>
    public DateTime? LockoutEnd { get; set; }

    /// <summary>Consecutive failed login attempts. Reset to 0 on success.</summary>
    public int AccessFailedCount { get; set; }

    // ── Audit (UTC, set by SaveChanges interceptor) ────────────────────
    public DateTime CreatedAt { get; set; }
    public DateTime UpdatedAt { get; set; }
    public int? CreatedBy { get; set; }
    public int? UpdatedBy { get; set; }

    // ── Soft delete ────────────────────────────────────────────────────
    public bool IsDeleted { get; set; }
    public DateTime? DeletedAt { get; set; }
    public int? DeletedBy { get; set; }

    public ICollection<UserRole> UserRoles { get; set; } = new List<UserRole>();
}

