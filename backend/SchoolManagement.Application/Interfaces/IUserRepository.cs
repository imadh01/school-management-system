using SchoolManagement.Domain.Entities;

namespace SchoolManagement.Application.Interfaces;

public interface IUserRepository
{
    Task<User?> GetByIdAsync(int id, CancellationToken cancellationToken);
    Task<User?> GetByUsernameOrEmailAsync(string usernameOrEmail, CancellationToken cancellationToken);
    Task<bool> ExistsByUsernameAsync(string username, CancellationToken cancellationToken);
    Task<bool> ExistsByEmailAsync(string email, CancellationToken cancellationToken);
    Task AddAsync(User user, CancellationToken cancellationToken);
    Task<IReadOnlyList<string>> GetRoleNamesAsync(int userId, CancellationToken cancellationToken);

    /// <summary>Distinct permission names granted to this user across all their roles.</summary>
    Task<IReadOnlyList<string>> GetPermissionNamesAsync(int userId, CancellationToken cancellationToken);
}
