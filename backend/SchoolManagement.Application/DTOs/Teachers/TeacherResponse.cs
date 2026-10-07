namespace SchoolManagement.Application.DTOs.Teachers;

public record TeacherResponse(
    int Id, int UserId, string Name, string Username, string Email,
    string? Phone, string? Specialization, string Status,
    int ClassCount, int SubjectCount, int ClassTeacherOfCount);
