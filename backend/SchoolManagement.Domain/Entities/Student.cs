namespace SchoolManagement.Domain.Entities;

public class Student
{
    public int Id { get; set; }

    public int? AdmissionId { get; set; }
    public Admission? Admission { get; set; }

    public int? UserId { get; set; }
    public User? User { get; set; }

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
    public string? BloodGroup { get; set; }
    public string? AadhaarNumber { get; set; }

    public string? Mobile { get; set; }
    public string? Email { get; set; }
    public string? AddressLine { get; set; }
    public string? City { get; set; }
    public string? State { get; set; }
    public string? Pincode { get; set; }

    public string? FatherName { get; set; }
    public string? FatherOccupation { get; set; }
    public string? FatherMobile { get; set; }
    public string? MotherName { get; set; }
    public string? MotherOccupation { get; set; }
    public string? MotherMobile { get; set; }
    public string? GuardianName { get; set; }
    public string? GuardianRelation { get; set; }
    public string? GuardianMobile { get; set; }

    public string Category { get; set; } = "General";
    public string? Religion { get; set; }
    public string? PreviousSchool { get; set; }
    public bool TransportRequired { get; set; }
    public string? TransportRoute { get; set; }
    public string? MedicalNotes { get; set; }

    public string? Nationality { get; set; }
    public string? SecondNationality { get; set; }
    public string? CountryOfBirth { get; set; }
    public string? PreferredName { get; set; }
    public string? PassportNumber { get; set; }
    public DateOnly? PassportExpiry { get; set; }
    public string? VisaType { get; set; }
    public DateOnly? VisaExpiry { get; set; }

    public string? MotherTongue { get; set; }
    public string? HomeLanguage { get; set; }
    public string? EnglishProficiency { get; set; }
    public string? CurriculumTrack { get; set; }
    public string AdmissionType { get; set; } = "Fresh Admission";

    public string? CustodyArrangement { get; set; }
    public string? PrimaryContactParent { get; set; }
    public string? AuthorizedPickupPersons { get; set; }
    public bool MediaConsent { get; set; } = true;

    public string? DietaryRequirements { get; set; } // comma-delimited tags
    public string? Allergies { get; set; }
    public string? InsuranceProvider { get; set; }
    public DateOnly? InsurancePolicyExpiry { get; set; }

    public string? House { get; set; }
    public string? EalCode { get; set; }

    public decimal? FeeConcessionPercent { get; set; }
    public string? SpecialEducationalNeeds { get; set; }

    public DateTime CreatedAt { get; set; }
    public DateTime UpdatedAt { get; set; }
    public int? CreatedBy { get; set; }
    public int? UpdatedBy { get; set; }
    public bool IsDeleted { get; set; }
    public DateTime? DeletedAt { get; set; }
    public int? DeletedBy { get; set; }
}