using SchoolManagement.Domain.Auditing;

namespace SchoolManagement.Domain.Entities;

/// <summary>
/// E2: the person behind a staff login (Admin, Supervisor, Clerk, Teacher, custom roles), one per user.
/// The login itself (username, password, status, role) stays on <see cref="User"/>: that row is read on
/// every request and is about signing in; this one is about the person.
///
/// Teaching data (specialization, assignments) stays on <see cref="Teacher"/>. Until E2c the teacher's
/// Name/Phone columns are kept in step with FullName/Mobile here (written together), then dropped.
/// Edits are protected by the user's version: every profile save calls User.NewVersion().
/// </summary>
public class StaffProfile
{
    public int Id { get; set; }

    public int UserId { get; set; }
    public User User { get; set; } = null!;

    /// <summary>For users copied in by the E2a migration without a teacher record this is the username
    /// until someone edits it; the Users list flags that as "needs name".</summary>
    public string FullName { get; set; } = string.Empty;

    /// <summary>Male / Female (Genders). Required by the forms; nullable for rows copied in by the migration.</summary>
    public string? Gender { get; set; }
    public DateOnly? DateOfBirth { get; set; }
    public string? Mobile { get; set; }
    public DateOnly? JoiningDate { get; set; }
    public string? Qualification { get; set; }

    /// <summary>Optional; unique when present (decision 4).</summary>
    public string? EmployeeNumber { get; set; }

    [AuditMasked]
    public string? BloodGroup { get; set; }
    [AuditMasked]
    public string? EmergencyContactName { get; set; }
    [AuditMasked]
    public string? EmergencyContactPhone { get; set; }

    public ICollection<Address> Addresses { get; set; } = new List<Address>();

    public DateTime CreatedAt { get; set; }
    public DateTime UpdatedAt { get; set; }
    public int? CreatedBy { get; set; }
    public int? UpdatedBy { get; set; }

    public bool IsDeleted { get; set; }
    public DateTime? DeletedAt { get; set; }
    public int? DeletedBy { get; set; }
}
