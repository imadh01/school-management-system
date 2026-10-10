namespace SchoolManagement.Application.DTOs.Students;

/// <summary>
/// Health and PickupPersons are replaced when supplied and left untouched when null
/// (an empty PickupPersons list clears them). Identity numbers have their own endpoint.
/// </summary>
public record UpdateStudentRequest(
    string RollNumber, int ClassSectionId, string Status, string? PhotoUrl,
    string FirstName, string? MiddleName, string LastName, string Gender, DateOnly DateOfBirth,
    string? Mobile, string? Email, string? AddressLine, string? City, string? State, string? Pincode,
    string Category, string? Religion, string? PreviousSchool,
    bool TransportRequired, string? TransportRoute,
    string? Nationality, string? SecondNationality, string? CountryOfBirth, string? PreferredName,
    string? MotherTongue, string? HomeLanguage, string? EnglishProficiency, string? CurriculumTrack, string AdmissionType,
    string? CustodyArrangement, bool MediaConsent,
    string? House, string? EalCode, decimal? FeeConcessionPercent,
    StudentHealthDto? Health,
    List<PickupPersonRequest>? PickupPersons,
    string RowVersion);
