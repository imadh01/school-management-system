using SchoolManagement.Application.DTOs.Users;

namespace SchoolManagement.Application.Interfaces;

public interface IUserService
{
    /// <summary>Admin-initiated account creation. Throws if the username or
    /// email is already taken, or the given role name doesn't exist.</summary>
    Task<UserResponse> CreateUserAsync(CreateUserRequest request, CancellationToken cancellationToken);
}