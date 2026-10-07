namespace SchoolManagement.Application.DTOs.Teachers;

/// <summary>All SubjectIds must belong to ClassSectionId.</summary>
public record AssignSubjectsRequest(int ClassSectionId, List<int> SubjectIds, bool MakeClassTeacher = false);