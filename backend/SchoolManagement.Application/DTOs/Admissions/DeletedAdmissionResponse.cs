namespace SchoolManagement.Application.DTOs.Admissions;

/// <summary>A deleted application, as the "Deleted" list shows it: what it was and who removed it, and when.</summary>
public record DeletedAdmissionResponse(
    int Id, string RegNo, string ApplicantName, string AppliedForClassSectionName,
    string Status, string Phone, DateTime DeletedAt, string? DeletedBy);
