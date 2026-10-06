namespace SchoolManagement.Application.DTOs.ClassSections;

public record CreateClassSectionRequest(
    string Name,
    string Section,
    int? Grade,
    string Stage,
    string Medium,
    string Stream,
    int Capacity,
    string? Building,
    int? Floor,
    string? Room,
    int? AcademicYearId,   // null = use the current year
    bool IsActive = true);