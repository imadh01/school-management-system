namespace SchoolManagement.Application.DTOs.Students;

/// <summary>One period of a student's academic history (GET /api/students/{id}/enrollments).</summary>
public record StudentEnrollmentResponse(
    int Id,
    int AcademicYearId, string AcademicYearName,
    int ClassSectionId, string ClassSectionName,
    string RollNumber,
    DateOnly StartDate, DateOnly? EndDate,
    string Status, string? Remarks);