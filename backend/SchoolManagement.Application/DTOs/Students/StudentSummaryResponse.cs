namespace SchoolManagement.Application.DTOs.Students;

/// <summary>One row of the student list (GET /api/students). No sensitive or side-table detail.</summary>
public record StudentSummaryResponse(
    int Id, string AdmNo, string RollNumber, int ClassSectionId, string ClassSectionName,
    DateOnly AdmissionDate, string Status, string? PhotoUrl,
    string FirstName, string? MiddleName, string LastName, string Gender, DateOnly DateOfBirth,
    string? Mobile, string Category, bool TransportRequired,
    string? Nationality, string? CurriculumTrack, string? House, string? EalCode,
    string? Allergies, string? AdmissionRegNo, int? AdmissionId);
