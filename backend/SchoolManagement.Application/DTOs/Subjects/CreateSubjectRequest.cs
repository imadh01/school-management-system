namespace SchoolManagement.Application.DTOs.Subjects;

public record CreateSubjectRequest(
    string Name, string? Code, int ClassSectionId, string Type,
    int? MaxMarks, int? PassMarks,
    int? TheoryMax, int? TheoryPass, int? PracticalMax, int? PracticalPass,
    bool IsActive);