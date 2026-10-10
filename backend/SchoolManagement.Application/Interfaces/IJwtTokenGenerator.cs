using SchoolManagement.Domain.Entities;

namespace SchoolManagement.Application.Interfaces;

public interface IJwtTokenGenerator
{
    /// <summary>
    /// Generates a JWT containing ONLY: sub (user id), security_stamp,
    /// and jti. No roles, no permissions — those are resolved server-side
    /// from the permission cache on every request.
    /// </summary>
    (string Token, DateTime ExpiresAtUtc) GenerateToken(User user);
}
