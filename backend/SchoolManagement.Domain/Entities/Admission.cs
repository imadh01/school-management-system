namespace SchoolManagement.Domain.Entities;

public class Admission
{
    public int Id { get; set; }
    public string RegNo { get; set; } = string.Empty;

    public string FirstName { get; set; } = string.Empty;
    public string? MiddleName { get; set; }
    public string LastName { get; set; } = string.Empty;
    public Student? Student { get; set; }
    public string Gender { get; set; } = string.Empty;
    public DateOnly DateOfBirth { get; set; }

    public int AcademicYearId { get; set; }
    public AcademicYear AcademicYear { get; set; } = null!;

    public int AppliedForClassSectionId { get; set; }
    public ClassSection AppliedForClassSection { get; set; } = null!;

    public string AdmissionType { get; set; } = "New";
    public string? PreviousSchool { get; set; }
    public string Phone { get; set; } = string.Empty;
    public string? Email { get; set; }
    public DateOnly RegistrationDate { get; set; }

    public string Status { get; set; } = "Registered";
    public string? RejectionReason { get; set; }

    public string? AddressLine { get; set; }
    public string? City { get; set; }
    public string? State { get; set; }
    public string? Pincode { get; set; }

    // Confirm Admission stage
    public decimal? AdmissionFee { get; set; }
    public string? AdmissionFeeReference { get; set; }
    public string? BloodGroup { get; set; }
    public string? Religion { get; set; }
    public string? Category { get; set; }
    public string? MedicalNotes { get; set; }
    public string? Remarks { get; set; }

    // Enroll to Class stage
    public string? RollNumber { get; set; }
    public string? AdmissionNumber { get; set; }
    public DateOnly? AdmissionDate { get; set; }
    public string? EntryPoint { get; set; }
    public bool TransportRequired { get; set; }
    public int? AllottedClassSectionId { get; set; }
    public ClassSection? AllottedClassSection { get; set; }

    public DateTime CreatedAt { get; set; }
    public DateTime UpdatedAt { get; set; }
    public int? CreatedBy { get; set; }
    public int? UpdatedBy { get; set; }
    public bool IsDeleted { get; set; }
    public DateTime? DeletedAt { get; set; }
    public int? DeletedBy { get; set; }

    // Guardians captured on the application (father/mother/guardian rows).
    // Staff confirm which existing Parent each one maps to at enrolment.
    public ICollection<AdmissionGuardian> Guardians { get; set; } = new List<AdmissionGuardian>();
}