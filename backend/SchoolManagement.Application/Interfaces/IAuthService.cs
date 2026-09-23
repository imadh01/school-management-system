using SchoolManagement.Application.DTOs.Auth;

namespace SchoolManagement.Application.Interfaces;

public interface IAuthService
{
    /// <summary>Null return means invalid credentials — the service doesn't
    /// distinguish "wrong username" from "wrong password" in its result,
    /// so the API layer can't leak which one was wrong.</summary>
    Task<AuthResult?> LoginAsync(LoginRequest request, CancellationToken cancellationToken);
}