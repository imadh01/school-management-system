namespace SchoolManagement.Domain.Entities;

/// <summary>
/// G1b: a kind of document (birth certificate, passport, Emirates ID...) and the rules that come
/// with it. Types are data, not code: the school can add one with a row (later from the Settings
/// page), no release needed. Seeded by the G1b migration, keyed by <see cref="Code"/>.
/// </summary>
public class DocumentType
{
    public int Id { get; set; }

    /// <summary>Stable key used by code and seed data, e.g. "BirthCertificate". Never shown.</summary>
    public string Code { get; set; } = string.Empty;

    /// <summary>What the forms and lists show, e.g. "Birth certificate".</summary>
    public string Name { get; set; } = string.Empty;

    /// <summary>Identity and medical documents: downloading one also needs Students.ViewSensitive.</summary>
    public bool IsSensitive { get; set; }

    /// <summary>Passport, visa, Emirates ID: the upload must carry an expiry date.</summary>
    public bool HasExpiry { get; set; }

    /// <summary>Offered for students and applicants.</summary>
    public bool ForStudents { get; set; } = true;

    /// <summary>Offered for staff (used from E2).</summary>
    public bool ForStaff { get; set; }

    public int SortOrder { get; set; }

    /// <summary>A retired type stays on old documents but is no longer offered for uploads.</summary>
    public bool IsActive { get; set; } = true;
}
