namespace SchoolManagement.Domain.Entities;

/// <summary>
/// A guardian (father / mother / guardian) listed on an admission application.
/// At enrolment each row is matched to an existing <see cref="Parent"/> or becomes a new one.
/// </summary>
public class AdmissionGuardian
{
    public int Id { get; set; }

    public int AdmissionId { get; set; }
    public Admission Admission { get; set; } = null!;

    public string RelationType { get; set; } = string.Empty; // Father | Mother | Guardian
    public string Name { get; set; } = string.Empty;

    // Optional while the application is a draft; enrolment is blocked until it is set.
    public string? Mobile { get; set; }
    public string? Email { get; set; }
    public bool IsPrimaryContact { get; set; }

    /// <summary>Database-computed digits-only copy of Mobile (see Parent.MobileKey).</summary>
    public string? MobileKey { get; private set; }

    public DateTime CreatedAt { get; set; }
    public DateTime UpdatedAt { get; set; }
}
