namespace SchoolManagement.Application.DTOs.Teachers;

/// <param name="NewPassword">Blank/null = keep the current password.</param>
public record UpdateTeacherRequest(
    string Name, string Username, string Email,
    string? Phone, string? Specialization, string Status, string? NewPassword);