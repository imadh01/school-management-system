using SchoolManagement.Domain.Auditing;

namespace SchoolManagement.Domain.Entities;

/// <summary>
/// G1a: one address of a student, parent or applicant. Exactly one owner column is set (CHECK
/// constraint), at most one address of each type per owner and at most one primary (filtered
/// unique indexes). Addresses are saved together with their owner, so they have no rowversion
/// of their own: the owner's rowversion protects them.
/// </summary>
public class Address
{
    public int Id { get; set; }

    public int? StudentId { get; set; }
    public Student? Student { get; set; }

    public int? ParentId { get; set; }
    public Parent? Parent { get; set; }

    /// <summary>The applicant's address while the application is in progress.</summary>
    public int? AdmissionId { get; set; }
    public Admission? Admission { get; set; }

    /// <summary>Home / Permanent / Mailing (AddressTypes).</summary>
    public string AddressType { get; set; } = AddressTypes.Home;
    public bool IsPrimary { get; set; }

    // The text of the address is masked in the audit log: the log records who changed which
    // address type and when, not where a child lives.
    [AuditMasked]
    public string Line1 { get; set; } = string.Empty;
    [AuditMasked]
    public string? Line2 { get; set; }
    [AuditMasked]
    public string City { get; set; } = string.Empty;
    [AuditMasked]
    public string? PostalCode { get; set; }

    public int CountryId { get; set; }
    public Country Country { get; set; } = null!;

    /// <summary>Emirate or state. Must belong to CountryId (composite foreign key).</summary>
    public int? RegionId { get; set; }
    public CountryRegion? Region { get; set; }

    public DateTime CreatedAt { get; set; }
    public DateTime UpdatedAt { get; set; }
    public int? CreatedBy { get; set; }
    public int? UpdatedBy { get; set; }
}
