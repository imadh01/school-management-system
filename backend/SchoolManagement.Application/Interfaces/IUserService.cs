using SchoolManagement.Application.DTOs.Users;

namespace SchoolManagement.Application.Interfaces;

/// <summary>
/// E2: staff accounts and their profiles (the Users page) and My Account. Every security change
/// (status, password, role, delete) goes through <see cref="IUserAccountService"/>.
/// </summary>
public interface IUserService
{
    Task<List<UserListItemResponse>> GetAllAsync(CancellationToken cancellationToken);

    Task<UserResponse> GetByIdAsync(int id, CancellationToken cancellationToken);

    /// <summary>
    /// A new staff account with its profile and addresses. The password is temporary (MustChangePassword).
    /// Role Teacher also creates the teacher record. Student/Parent roles are refused (decision 1).
    /// </summary>
    Task<UserResponse> CreateUserAsync(CreateUserRequest request, CancellationToken cancellationToken);

    /// <summary>Account, profile, addresses and (optionally) a new role, saved together. 409 when stale.</summary>
    Task<UserResponse> UpdateAsync(int id, UpdateUserRequest request, CancellationToken cancellationToken);

    Task<UserResponse> ChangeStatusAsync(int id, ChangeUserStatusRequest request, CancellationToken cancellationToken);

    Task ResetPasswordAsync(int id, ResetUserPasswordRequest request, CancellationToken cancellationToken);

    Task DeleteAsync(int id, CancellationToken cancellationToken);

    /// <summary>My Account: the signed-in user's own account and profile. 404 when they have no staff profile.</summary>
    Task<UserResponse> GetMyProfileAsync(CancellationToken cancellationToken);

    /// <summary>My Account: e-mail, profile and addresses only (no username, role, status, employee number).</summary>
    Task<UserResponse> UpdateMyProfileAsync(UpdateMyProfileRequest request, CancellationToken cancellationToken);
}