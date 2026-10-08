namespace SchoolManagement.Application.DTOs.Students;

/// <summary>
/// Direct creation (without an admission). Guardians are linked afterwards with
/// POST /api/students/{id}/guardians; identity numbers are set with PUT /api/students/{id}/identity.
/// </summary>
public record CreateStudentRequest(
    string AdmNo, string RollNumber, int ClassSectionId, DateOnly AdmissionDate, string? PhotoUrl,
    string FirstName, string? MiddleName, string LastName, string Gender, DateOnly DateOfBirth,
    string? Mobile, string? Email, string? AddressLine, string? City, string? State, string? Pincode,
    string Category, string? Religion, string? PreviousSchool,
    bool TransportRequired, string? TransportRoute,
    string? Nationality, string? SecondNationality, string? CountryOfBirth, string? PreferredName,
    string? MotherTongue, string? HomeLanguage, string? EnglishProficiency, string? CurriculumTrack, string AdmissionType,
    string? CustodyArrangement, bool MediaConsent,
    string? House, string? EalCode, decimal? FeeConcessionPercent,
    StudentHealthDto? Health,
    List<PickupPersonRequest>? PickupPersons);
