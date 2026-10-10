using SchoolManagement.Domain.Entities;

namespace SchoolManagement.Application.Interfaces;

/// <summary>A role as the list screen needs it, computed in SQL.</summary>
public record RoleListRow(
    int Id, string Name, string? Description, bool IsSystem, bool IsActive, int UserCount, int PermissionCount);

public interface IRoleRepository
{
    /// <summary>Used when assigning a default role (e.g. "Student") at registration.</summary>
    Task<Role?> GetByNameAsync(string name, CancellationToken cancellationToken);

    Task<List<RoleListRow>> GetListAsync(CancellationToken cancellationToken);

    /// <summary>Tracked, with RolePermissions → Permission loaded.</summary>
    Task<Role?> GetWithPermissionsAsync(int id, CancellationToken cancellationToken);

    /// <summary>Case-insensitive (the column's collation), ignoring <paramref name="excludeRoleId"/>.</summary>
    Task<bool> NameExistsAsync(string name, int? excludeRoleId, CancellationToken cancellationToken);

    Task<List<Permission>> GetPermissionsByNamesAsync(IReadOnlyCollection<string> names, CancellationToken cancellationToken);

    Task<bool> UserHoldsRoleAsync(int userId, int roleId, CancellationToken cancellationToken);

    /// <summary>Every user holding the role, for cache invalidation.</summary>
    Task<List<int>> GetUserIdsInRoleAsync(int roleId, CancellationToken cancellationToken);

    /// <summary>Adds and saves. A duplicate name (lost race) becomes a ConflictException.</summary>
    Task AddAsync(Role role, CancellationToken cancellationToken);

    /// <summary>Saves tracked changes. A duplicate name becomes a ConflictException.</summary>
    Task SaveChangesAsync(CancellationToken cancellationToken);
}
