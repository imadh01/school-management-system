using Microsoft.Data.SqlClient;
using Microsoft.EntityFrameworkCore;
using SchoolManagement.Application.Interfaces;
using SchoolManagement.Domain.Entities;
using SchoolManagement.Domain.Exceptions;
using SchoolManagement.Infrastructure.Persistence;

namespace SchoolManagement.Infrastructure.Repositories;

public class RoleRepository : IRoleRepository
{
    private readonly ApplicationDbContext _context;

    public RoleRepository(ApplicationDbContext context)
    {
        _context = context;
    }

    public Task<Role?> GetByNameAsync(string name, CancellationToken cancellationToken) =>
        _context.Roles.FirstOrDefaultAsync(r => r.Name == name, cancellationToken);

    public Task<List<RoleListRow>> GetListAsync(CancellationToken cancellationToken) =>
        _context.Roles
            .AsNoTracking()
            .OrderByDescending(r => r.IsSystem)
            .ThenBy(r => r.Name)
            .Select(r => new RoleListRow(
                r.Id,
                r.Name,
                r.Description,
                r.IsSystem,
                r.IsActive,
                r.UserRoles.Count(ur => !ur.User.IsDeleted),
                r.RolePermissions.Count))
            .ToListAsync(cancellationToken);

    public Task<Role?> GetWithPermissionsAsync(int id, CancellationToken cancellationToken) =>
        _context.Roles
            .Include(r => r.RolePermissions)
                .ThenInclude(rp => rp.Permission)
            .FirstOrDefaultAsync(r => r.Id == id, cancellationToken);

    public Task<bool> NameExistsAsync(string name, int? excludeRoleId, CancellationToken cancellationToken) =>
        _context.Roles.AnyAsync(
            r => r.Name == name && (excludeRoleId == null || r.Id != excludeRoleId),
            cancellationToken);

    public Task<List<Permission>> GetPermissionsByNamesAsync(
        IReadOnlyCollection<string> names, CancellationToken cancellationToken)
    {
        var list = names.ToList();
        return _context.Permissions.Where(p => list.Contains(p.Name)).ToListAsync(cancellationToken);
    }

    public Task<bool> UserHoldsRoleAsync(int userId, int roleId, CancellationToken cancellationToken) =>
        _context.UserRoles.AnyAsync(ur => ur.UserId == userId && ur.RoleId == roleId, cancellationToken);

    public Task<List<int>> GetUserIdsInRoleAsync(int roleId, CancellationToken cancellationToken) =>
        _context.UserRoles
            .Where(ur => ur.RoleId == roleId)
            .Select(ur => ur.UserId)
            .ToListAsync(cancellationToken);

    public async Task AddAsync(Role role, CancellationToken cancellationToken)
    {
        _context.Roles.Add(role);
        await SaveChangesAsync(cancellationToken);
    }

    /// <summary>The service checks the name first; two admins racing can still collide on the unique index.</summary>
    public async Task SaveChangesAsync(CancellationToken cancellationToken)
    {
        try
        {
            await _context.SaveChangesAsync(cancellationToken);
        }
        catch (DbUpdateException ex) when (ex.InnerException is SqlException { Number: 2601 or 2627 })
        {
            throw new ConflictException("A role with this name was just created by someone else. Use a different name.");
        }
    }
}
