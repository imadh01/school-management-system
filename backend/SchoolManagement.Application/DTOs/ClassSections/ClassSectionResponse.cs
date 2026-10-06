namespace SchoolManagement.Application.DTOs.ClassSections;

public record ClassSectionResponse(
    int Id,
    string Name,
    string Section,
    string Code,
    int? Grade,
    string Stage,
    string Medium,
    string Stream,
    int? Capacity,
    int Enrolled,
    string? Building,
    int? Floor,
    string? Room,
    string Status,
    string DisplayName,
    int AcademicYearId,
    string AcademicYearName);