namespace SchoolManagement.Application.DTOs.Subjects;

public record SubjectResponse(
    int Id, string Name, string Code, int ClassSectionId, string ClassSectionName,
    string Type, int? MaxMarks, int? PassMarks,
    int? TheoryMax, int? TheoryPass, int? PracticalMax, int? PracticalPass,
    string Status);