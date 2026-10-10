using SchoolManagement.Application.DTOs.Parents;

namespace SchoolManagement.Application.DTOs.Students;

/// <summary>Full detail of one student (GET /api/students/{id}, create, update).</summary>
public record StudentResponse(
    int Id, string AdmNo, string RollNumber, int ClassSectionId, string ClassSectionName,
    DateOnly AdmissionDate, string Status, string? PhotoUrl,
    string FirstName, string? MiddleName, string LastName, string Gender, DateOnly DateOfBirth,
    string? Mobile, string? Email, string? AddressLine, string? City, string? State, string? Pincode,
    string Category, string? Religion, string? PreviousSchool,
    bool TransportRequired, string? TransportRoute,
    string? Nationality, string? SecondNationality, string? CountryOfBirth, string? PreferredName,
    string? MotherTongue, string? HomeLanguage, string? EnglishProficiency, string? CurriculumTrack, string AdmissionType,
    string? CustodyArrangement, bool MediaConsent,
    string? House, string? EalCode, decimal? FeeConcessionPercent,
    string? AdmissionRegNo, int? AdmissionId,
    StudentHealthDto Health,
    StudentIdentityResponse Identity,
    List<PickupPersonResponse> PickupPersons,
    List<StudentGuardianResponse> Guardians,
    string RowVersion);
