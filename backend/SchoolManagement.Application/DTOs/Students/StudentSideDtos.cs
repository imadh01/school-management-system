namespace SchoolManagement.Application.DTOs.Students;

/// <summary>Health and insurance block — used both as request and response.</summary>
public record StudentHealthDto(
    string? BloodGroup, string? Allergies, string? DietaryRequirements,
    string? MedicalNotes, string? SpecialEducationalNeeds,
    string? InsuranceProvider, DateOnly? InsurancePolicyExpiry);

public record PickupPersonRequest(string Name, string Relation, string Phone, string? IdNote);

public record PickupPersonResponse(int Id, string Name, string Relation, string Phone, string? IdNote);

/// <summary>
/// Identity numbers. When <see cref="IsMasked"/> is true the caller lacks Students.ViewSensitive and
/// AadhaarNumber / PassportNumber contain only their last characters (e.g. "********9012").
/// </summary>
public record StudentIdentityResponse(
    string? AadhaarNumber, string? PassportNumber, DateOnly? PassportExpiry,
    string? VisaType, DateOnly? VisaExpiry, bool IsMasked);

public record UpdateStudentIdentityRequest(
    string? AadhaarNumber, string? PassportNumber, DateOnly? PassportExpiry,
    string? VisaType, DateOnly? VisaExpiry);