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

    /// <summary>Runs <paramref name="operation"/> in one database transaction (committed at the end).</summary>
    Task<T> ExecuteInTransactionAsync<T>(Func<Task<T>> operation, CancellationToken cancellationToken);

    /// <summary>
    /// Holds the "account changes" lock until the current transaction ends, so status / role / delete
    /// changes run one after another. Must be called inside <see cref="ExecuteInTransactionAsync{T}"/>.
    /// </summary>
    Task LockAccountChangesAsync(CancellationToken cancellationToken);

    /// <summary>Active, not deleted users holding the Admin role, not counting <paramref name="excludeUserId"/>.</summary>
    Task<int> CountActiveAdminsAsync(int? excludeUserId, CancellationToken cancellationToken);

    /// <summary>Persists changes to an already-tracked User entity.</summary>
    Task SaveChangesAsync(CancellationToken cancellationToken);
}
