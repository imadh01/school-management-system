namespace SchoolManagement.Application.DTOs.Students;

public record StudentResponse(
    int Id, string AdmNo, string RollNumber, string ClassSectionName,
    DateOnly AdmissionDate, string Status, string? PhotoUrl,
    string FirstName, string? MiddleName, string LastName, string Gender, DateOnly DateOfBirth,
    string? BloodGroup, string? AadhaarNumber,
    string? Mobile, string? Email, string? AddressLine, string? City, string? State, string? Pincode,
    string? FatherName, string? FatherOccupation, string? FatherMobile,
    string? MotherName, string? MotherOccupation, string? MotherMobile,
    string? GuardianName, string? GuardianRelation, string? GuardianMobile,
    string Category, string? Religion, string? PreviousSchool,
    bool TransportRequired, string? TransportRoute, string? MedicalNotes,
    string? Nationality, string? SecondNationality, string? CountryOfBirth, string? PreferredName,
    string? PassportNumber, DateOnly? PassportExpiry, string? VisaType, DateOnly? VisaExpiry,
    string? MotherTongue, string? HomeLanguage, string? EnglishProficiency, string? CurriculumTrack, string AdmissionType,
    string? CustodyArrangement, string? PrimaryContactParent, string? AuthorizedPickupPersons, bool MediaConsent,
    string? DietaryRequirements, string? Allergies, string? InsuranceProvider, DateOnly? InsurancePolicyExpiry,
    string? House, string? EalCode, decimal? FeeConcessionPercent, string? SpecialEducationalNeeds,
    string? AdmissionRegNo, int? AdmissionId);