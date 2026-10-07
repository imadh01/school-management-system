
namespace SchoolManagement.Domain.Exceptions;

/// <summary>The user is signed in but not allowed to act on this specific resource (HTTP 403).</summary>
public class ForbiddenException : AppException
{
    public ForbiddenException(string message) : base(message) { }
}