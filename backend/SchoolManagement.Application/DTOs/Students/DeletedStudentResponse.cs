namespace SchoolManagement.Application.DTOs.Students;

/// <summary>One row of the Deleted students list.</summary>
public record DeletedStudentResponse(
    int Id, string AdmNo, string StudentName, string ClassSection, string Status, DateTime DeletedAt, string? DeletedBy);
