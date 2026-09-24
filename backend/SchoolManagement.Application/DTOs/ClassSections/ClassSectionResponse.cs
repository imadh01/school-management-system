namespace SchoolManagement.Application.DTOs.ClassSections;

public record ClassSectionResponse(
    int Id,
    string Name,
    string Section,
    int? Grade,
    int? Capacity,
    string? Room,
    string Status,
    string DisplayName,
    int AcademicYearId,
    string AcademicYearName);