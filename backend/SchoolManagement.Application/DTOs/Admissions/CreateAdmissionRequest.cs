namespace SchoolManagement.Application.DTOs.Admissions;

public record CreateAdmissionRequest(
    string FirstName, string? MiddleName, string LastName,
    string Gender, DateOnly DateOfBirth,
    int AppliedForClassSectionId, string? Grade,
    string AdmissionType, string? PreviousSchool,
    string Phone, string? Email,
    string? FatherName, string? FatherMobile,
    string? MotherName, string? MotherMobile,
    string? GuardianName, string? GuardianRelation, string? GuardianMobile,
    string? AddressLine, string? City, string? State, string? Pincode,
    decimal? RegistrationFee, string? Notes);