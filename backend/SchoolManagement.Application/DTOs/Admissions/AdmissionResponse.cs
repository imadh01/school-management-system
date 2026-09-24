namespace SchoolManagement.Application.DTOs.Admissions;

public record AdmissionResponse(
    int Id, string RegNo,
    string FirstName, string? MiddleName, string LastName,
    string Gender, DateOnly DateOfBirth,
    string AcademicYearName,
    int AppliedForClassSectionId, string AppliedForClassSectionName,
    string AdmissionType, string? PreviousSchool,
    string Phone, string? Email, DateOnly RegistrationDate,
    string Status, string? RejectionReason,
    string? FatherName, string? FatherMobile,
    string? MotherName, string? MotherMobile,
    string? GuardianName, string? GuardianRelation, string? GuardianMobile,
    string? AddressLine, string? City, string? State, string? Pincode,
    decimal? AdmissionFee, string? AdmissionFeeReference,
    string? BloodGroup, string? Religion, string? Category,
    string? MedicalNotes, string? Remarks,
    string? RollNumber, string? AdmissionNumber, DateOnly? AdmissionDate,
    string? EntryPoint, bool TransportRequired,
    int? AllottedClassSectionId, string? AllottedClassSectionName);