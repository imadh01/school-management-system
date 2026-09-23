namespace SchoolManagement.Application.DTOs.Auth;

public record LoginRequest(string UsernameOrEmail, string Password);