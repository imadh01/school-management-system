namespace SchoolManagement.Application.DTOs.Admissions;

public record AdmissionResponse(
    int Id, string RegNo,
    string FirstName, string? MiddleName, string LastName,
    string Gender, DateOnly DateOfBirth,
    string AcademicYearName, string AppliedForClassSectionName,
    string AdmissionType, string? PreviousSchool,
    string Phone, string? Email, DateOnly RegistrationDate,
    string Status, string? RejectionReason,
    decimal? AdmissionFee, string? RollNumber, string? AdmissionNumber,
    string? AllottedClassSectionName);