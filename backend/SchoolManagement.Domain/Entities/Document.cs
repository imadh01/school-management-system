using SchoolManagement.Domain.Auditing;

namespace SchoolManagement.Domain.Entities;

/// <summary>
/// G1b: one uploaded file of a student or an applicant. The row holds the metadata and the review
/// state; the bytes live in file storage (IFileStorage) under <see cref="StorageKey"/>, never in the
/// database.
///
/// Rules kept by the database (see DocumentConfiguration): exactly one owner; the status is one of
/// Pending / Verified / Rejected; a reviewed document names its reviewer; a rejected one has a
/// reason; the reviewer is never the uploader; the same file (SHA-256) is attached to one owner
/// only once while it is not deleted.
/// </summary>
public class Document : IHasRowVersion
{
    public int Id { get; set; }

    public int? StudentId { get; set; }
    public Student? Student { get; set; }

    /// <summary>Set while the applicant is not a student yet. Enrolment moves the document to the student.</summary>
    public int? AdmissionId { get; set; }
    public Admission? Admission { get; set; }

    public int DocumentTypeId { get; set; }
    public DocumentType DocumentType { get; set; } = null!;

    /// <summary>Optional label, e.g. "Passport (old)".</summary>
    public string? Title { get; set; }

    /// <summary>The name the file had on the uploader's computer. Shown and offered as the download
    /// name (with the extension corrected to the real type); never used as a path.</summary>
    public string OriginalFileName { get; set; } = string.Empty;

    /// <summary>Detected from the file's own bytes, never from its name or the browser (DocumentContentTypes).</summary>
    public string ContentType { get; set; } = string.Empty;

    public int SizeBytes { get; set; }

    /// <summary>SHA-256 of the content: spots the same file uploaded twice and a file changed on disk.</summary>
    [AuditIgnore]
    public byte[] Sha256 { get; set; } = Array.Empty<byte>();

    /// <summary>Where IFileStorage keeps the bytes, e.g. "2026/10/7f3c...e1.pdf". Random, never the user's file name.</summary>
    [AuditIgnore]
    public string StorageKey { get; set; } = string.Empty;

    /// <summary>Required when the type HasExpiry (checked by the service).</summary>
    public DateOnly? ExpiryDate { get; set; }

    /// <summary>Pending / Verified / Rejected (DocumentStatuses). Unverify returns to Pending.</summary>
    public string Status { get; set; } = DocumentStatuses.Pending;

    /// <summary>Who verified or rejected it. Never the uploader (CreatedBy).</summary>
    public int? VerifiedBy { get; set; }
    public User? VerifiedByUser { get; set; }
    public DateTime? VerifiedAt { get; set; }
    public string? RejectionReason { get; set; }

    public DateTime CreatedAt { get; set; }
    public DateTime UpdatedAt { get; set; }
    /// <summary>The uploader (stamped by the audit interceptor, like on every table).</summary>
    public int? CreatedBy { get; set; }
    public int? UpdatedBy { get; set; }

    public byte[] RowVersion { get; set; } = Array.Empty<byte>();

    public bool IsDeleted { get; set; }
    public DateTime? DeletedAt { get; set; }
    public int? DeletedBy { get; set; }

    /// <summary>When the purge job removed the file of a deleted document (after the retention period).
    /// The row stays as the record that the document existed.</summary>
    public DateTime? FilePurgedAt { get; set; }
}
