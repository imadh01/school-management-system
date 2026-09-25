namespace SchoolManagement.Application.DTOs.Users;

public record UserResponse(int Id, string Username, string Email, string Status, IReadOnlyList<string> Roles);