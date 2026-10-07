namespace SchoolManagement.Application.DTOs.Teachers;

public record CreateTeacherRequest(
    string Name, string Username, string Email, string Password,
    string? Phone, string? Specialization, string Status = "Active");