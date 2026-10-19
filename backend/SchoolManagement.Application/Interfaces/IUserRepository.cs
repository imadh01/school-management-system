using SchoolManagement.Application.DTOs.Users;
using SchoolManagement.Domain.Entities;

namespace SchoolManagement.Application.Interfaces;

public interface IUserRepository
{
    Task<User?> GetByIdAsync(int id, CancellationToken cancellationToken);

    /// <summary>E2: the user with roles, staff profile and the profile's addresses (country, region), tracked.</summary>
    Task<User?> GetWithProfileAsync(int id, CancellationToken cancellationToken);

    /// <summary>E2: the Users list (every non-deleted login), newest first. One query, no tracking.</summary>
    Task<List<UserListItemResponse>> GetListAsync(DateTime nowUtc, CancellationToken cancellationToken);

    /// <summary>E2: the teacher record of this user, if any (null for non-teachers).</summary>
    Task<int?> GetTeacherIdAsync(int userId, CancellationToken cancellationToken);
    Task<User?> GetByUsernameOrEmailAsync(string usernameOrEmail, CancellationToken cancellationToken);
    /// <param name="excludeUserId">The user being edited (its own value is not a clash), or null.</param>
    Task<bool> ExistsByUsernameAsync(string username, int? excludeUserId, CancellationToken cancellationToken);
    Task<bool> ExistsByEmailAsync(string email, int? excludeUserId, CancellationToken cancellationToken);
    /// <summary>The highest serial among employee numbers shaped prefix + digits (deleted staff included); 0 when none.</summary>
    Task<int> GetMaxEmployeeSerialAsync(string prefix, CancellationToken cancellationToken);
    Task<bool> ExistsByEmployeeNumberAsync(string employeeNumber, int? excludeUserId, CancellationToken cancellationToken);
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

    /// <summary>Persists changes to an already-tracked User entity. A unique-index clash becomes a 409.</summary>
    Task SaveChangesAsync(CancellationToken cancellationToken);
}
