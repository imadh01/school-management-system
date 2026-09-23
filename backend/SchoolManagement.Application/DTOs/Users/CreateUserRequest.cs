namespace SchoolManagement.Application.DTOs.Users;

public record CreateUserRequest(string Username, string Email, string Password, string RoleName);