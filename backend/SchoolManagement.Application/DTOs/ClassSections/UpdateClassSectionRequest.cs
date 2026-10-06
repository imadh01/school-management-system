namespace SchoolManagement.Application.DTOs.ClassSections;

public record UpdateClassSectionRequest(
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
    bool IsActive);