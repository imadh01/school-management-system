namespace SchoolManagement.Application.DTOs.Admissions;

public record CreateAdmissionRequest(
    string FirstName, string? MiddleName, string LastName,
    string Gender, DateOnly DateOfBirth,
    int AppliedForClassSectionId,
    string AdmissionType, string? PreviousSchool,
    string Phone, string? Email,
    string? AddressLine, string? City, string? State, string? Pincode,
    string? Remarks,
    List<AdmissionGuardianRequest>? Guardians);