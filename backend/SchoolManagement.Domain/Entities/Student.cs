using SchoolManagement.Domain.Auditing;
namespace SchoolManagement.Domain.Entities;

public class Student : IHasRowVersion
{
    public int Id { get; set; }

    public int? AdmissionId { get; set; }
    public Admission? Admission { get; set; }

    public int? UserId { get; set; }
    public User? User { get; set; }

    // Side tables split out of the old 70-column Students row.
    public StudentHealth? Health { get; set; }
    public StudentIdentityDocument? IdentityDocument { get; set; }
    public ICollection<StudentPickupPerson> PickupPersons { get; set; } = new List<StudentPickupPerson>();

    /// <summary>Academic history: one row per class/section period. ClassSectionId and RollNumber below mirror the Active one.</summary>
    public ICollection<StudentEnrollment> Enrollments { get; set; } = new List<StudentEnrollment>();

    public string AdmNo { get; set; } = string.Empty;
    public string RollNumber { get; set; } = string.Empty;

    public int ClassSectionId { get; set; }
    public ClassSection ClassSection { get; set; } = null!;

    public DateOnly AdmissionDate { get; set; }
    public string Status { get; set; } = "Active";
    public string? PhotoUrl { get; set; }

    public string FirstName { get; set; } = string.Empty;
    public string? MiddleName { get; set; }
    public string LastName { get; set; } = string.Empty;
    public string Gender { get; set; } = string.Empty;
    public DateOnly DateOfBirth { get; set; }

    public string? Mobile { get; set; }
    public string? Email { get; set; }
    public string? AddressLine { get; set; }
    public string? City { get; set; }
    public string? State { get; set; }
    public string? Pincode { get; set; }

    [AuditMasked]
    public string Category { get; set; } = "General";
    [AuditMasked]
    public string? Religion { get; set; }
    public string? PreviousSchool { get; set; }
    public bool TransportRequired { get; set; }
    public string? TransportRoute { get; set; }

    public string? Nationality { get; set; }
    public string? SecondNationality { get; set; }
    public string? CountryOfBirth { get; set; }
    public string? PreferredName { get; set; }

    public string? MotherTongue { get; set; }
    public string? HomeLanguage { get; set; }
    public string? EnglishProficiency { get; set; }
    public string? CurriculumTrack { get; set; }
    public string AdmissionType { get; set; } = AdmissionTypes.New;

    [AuditMasked]
    public string? CustodyArrangement { get; set; }
    public bool MediaConsent { get; set; } = true;

    public string? House { get; set; }
    public string? EalCode { get; set; }

    public decimal? FeeConcessionPercent { get; set; }

    public DateTime CreatedAt { get; set; }
    public DateTime UpdatedAt { get; set; }

    /// <summary>Concurrency stamp maintained by SQL Server (rowversion). Sent to the client and sent back on update.</summary>
    public byte[] RowVersion { get; set; } = Array.Empty<byte>();
    public int? CreatedBy { get; set; }
    public int? UpdatedBy { get; set; }
    public bool IsDeleted { get; set; }
    public DateTime? DeletedAt { get; set; }
    public int? DeletedBy { get; set; }
}