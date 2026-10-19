using SchoolManagement.Application.DTOs.Addresses;

namespace SchoolManagement.Application.DTOs.Students;

/// <summary>
/// Health, PickupPersons and Addresses are replaced when supplied and left untouched when null
/// (an empty list clears them). Identity numbers have their own endpoint.
/// </summary>
public record UpdateStudentRequest(
    string RollNumber, int ClassSectionId, string Status,
    string FirstName, string? MiddleName, string LastName, string Gender, DateOnly DateOfBirth,
    string? Mobile, string? Email,
    string Category, string? Religion, string? PreviousSchool,
    bool TransportRequired, string? TransportRoute,
    string? Nationality, string? SecondNationality, string? CountryOfBirth, string? PreferredName,
    string? MotherTongue, string? HomeLanguage, string? EnglishProficiency, string? CurriculumTrack, string AdmissionType,
    string? CustodyArrangement, bool MediaConsent,
    string? House, string? EalCode, decimal? FeeConcessionPercent,
    StudentHealthDto? Health,
    List<PickupPersonRequest>? PickupPersons,
    List<AddressRequest>? Addresses,
    string RowVersion);
