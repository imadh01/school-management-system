using SchoolManagement.Application.DTOs.Addresses;

namespace SchoolManagement.Application.DTOs.Admissions;

public record UpdateAdmissionRequest(
    string FirstName, string? MiddleName, string LastName,
    string Gender, DateOnly DateOfBirth,
    int AppliedForClassSectionId,
    string AdmissionType, string? PreviousSchool,
    string Phone, string? Email,
    List<AddressRequest>? Addresses,
    string? Remarks,
    List<AdmissionGuardianRequest>? Guardians,
    string RowVersion);
