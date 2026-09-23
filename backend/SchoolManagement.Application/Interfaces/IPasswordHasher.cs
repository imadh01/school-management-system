namespace SchoolManagement.Application.Interfaces;

public interface IPasswordHasher
{
    string HashPassword(string password);

    /// <summary>True if the provided password matches the stored hash.</summary>
    bool VerifyPassword(string hashedPassword, string providedPassword);
}