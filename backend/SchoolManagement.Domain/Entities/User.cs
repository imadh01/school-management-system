using SchoolManagement.Domain.Auditing;

namespace SchoolManagement.Domain.Entities;

/// <summary>
/// One row per login-capable person, regardless of role.
/// Role-specific profile data (Student/Teacher/Parent) lives in later
/// modules and links back here via a nullable UserId FK.
/// </summary>
public class User : IHasRowVersion
{
    public int Id { get; set; }

    public string Username { get; set; } = string.Empty;
    public string Email { get; set; } = string.Empty;
    /// <summary>Masked in the audit log (E2 security fix): the log shows that it changed, never the hash.</summary>
    [AuditMasked]
    public string PasswordHash { get; set; } = string.Empty;

    /// <summary>Active / Inactive / Suspended — enforced by a DB check constraint.</summary>
    public string Status { get; set; } = "Active";

    /// <summary>Left out of the audit log: it changes at every sign-in (noise). UpdatedAt still moves.</summary>
    [AuditIgnore]
    public DateTime? LastLoginAt { get; set; }

    // ── D1: Security infrastructure ────────────────────────────────────
    /// <summary>
    /// Rotated on every security-critical change (password, role, status).
    /// The JWT carries this value; OnTokenValidated compares it against
    /// the cached copy to give immediate revocation.
    /// </summary>
    [AuditMasked]
    public Guid SecurityStamp { get; set; } = Guid.NewGuid();

    /// <summary>When true, every endpoint except change-password and /auth/me is blocked.</summary>
    public bool MustChangePassword { get; set; }

    /// <summary>UTC instant until which the account is locked. Null = not locked.</summary>
    // Audited on purpose: a lockout (and an admin's unlock) is a security event worth a log row.
    public DateTime? LockoutEnd { get; set; }

    /// <summary>
    /// Consecutive failed login attempts. Reset to 0 on success. Left out of the audit log: it changes at
    /// every failed sign-in; the lockout itself is logged.
    /// </summary>
    [AuditIgnore]
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

    /// <summary>E2: the person behind a staff login (none for student/parent logins).</summary>
    public StaffProfile? StaffProfile { get; set; }

    /// <summary>
    /// E2: edits of the account or its profile from two screens at once → 409.
    /// Set by the application (binary(8)), NOT a SQL Server rowversion: signing in writes LastLoginAt to
    /// this row, and a sign-in must not make an administrator's open edit form "stale". Every edit of the
    /// account or its profile calls <see cref="NewVersion"/>.
    /// </summary>
    public byte[] RowVersion { get; set; } = NewVersionBytes();

    /// <summary>Marks the account as edited: a form loaded before this save gets a 409.</summary>
    public void NewVersion() => RowVersion = NewVersionBytes();

    private static byte[] NewVersionBytes() => Guid.NewGuid().ToByteArray()[..8];
}

