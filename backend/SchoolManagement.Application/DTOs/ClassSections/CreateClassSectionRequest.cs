namespace SchoolManagement.Application.DTOs.ClassSections;

public record CreateClassSectionRequest(
    string Name,
    string Section,
    int? Grade,
    int? Capacity,
    string? Room,
    int? AcademicYearId); // null = use the current year