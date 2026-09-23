using SchoolManagement.Domain.Entities;

namespace SchoolManagement.Application.Interfaces;

public interface IJwtTokenGenerator
{
    (string Token, DateTime ExpiresAtUtc) GenerateToken(
        User user,
        IReadOnlyList<string> roles,
        IReadOnlyList<string> permissions);
}