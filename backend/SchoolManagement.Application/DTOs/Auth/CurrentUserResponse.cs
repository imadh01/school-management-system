namespace SchoolManagement.Application.DTOs.Auth;

/// <summary>Response for GET /api/auth/me — the frontend uses this to show/hide UI elements.</summary>
public record CurrentUserResponse(
    int UserId,
    string Username,
    string Email,
    IReadOnlyList<string> Roles,
    IReadOnlyList<string> Permissions,
    bool MustChangePassword);
