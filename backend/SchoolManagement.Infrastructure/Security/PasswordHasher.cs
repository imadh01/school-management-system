using Microsoft.AspNetCore.Identity;
using SchoolManagement.Application.Interfaces;
using SchoolManagement.Domain.Entities;

namespace SchoolManagement.Infrastructure.Security;

/// <summary>
/// Wraps ASP.NET Core Identity's PasswordHasher<TUser> for its
/// well-tested PBKDF2 implementation — without pulling in the rest of
/// the Identity framework (its own Users/Roles tables, which would
/// conflict with our own RBAC schema).
/// </summary>
public class PasswordHasher : IPasswordHasher
{
    private readonly Microsoft.AspNetCore.Identity.PasswordHasher<User> _inner = new();

    public string HashPassword(string password) =>
        _inner.HashPassword(null!, password);

    public bool VerifyPassword(string hashedPassword, string providedPassword) =>
        _inner.VerifyHashedPassword(null!, hashedPassword, providedPassword)
            != PasswordVerificationResult.Failed;
}