namespace SchoolManagement.Application.DTOs.Auth;

public record AuthResult(
    int UserId,
    string Username,
    string Email,
    IReadOnlyList<string> Roles,
    string Token,
    DateTime ExpiresAtUtc);