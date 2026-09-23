using Microsoft.EntityFrameworkCore;
using SchoolManagement.Application.Interfaces;
using SchoolManagement.Domain.Entities;
using SchoolManagement.Infrastructure.Persistence;

namespace SchoolManagement.Infrastructure.Repositories;

public class UserRepository : IUserRepository
{
    private readonly ApplicationDbContext _context;

    public UserRepository(ApplicationDbContext context)
    {
        _context = context;
    }

    public Task<User?> GetByIdAsync(int id, CancellationToken cancellationToken) =>
        _context.Users
            .Include(u => u.UserRoles)
                .ThenInclude(ur => ur.Role)
            .FirstOrDefaultAsync(u => u.Id == id, cancellationToken);

    public Task<User?> GetByUsernameOrEmailAsync(string usernameOrEmail, CancellationToken cancellationToken) =>
        _context.Users
            .Include(u => u.UserRoles)
                .ThenInclude(ur => ur.Role)
            .FirstOrDefaultAsync(
                u => u.Username == usernameOrEmail || u.Email == usernameOrEmail,
                cancellationToken);

    public Task<bool> ExistsByUsernameAsync(string username, CancellationToken cancellationToken) =>
        _context.Users.AnyAsync(u => u.Username == username, cancellationToken);

    public Task<bool> ExistsByEmailAsync(string email, CancellationToken cancellationToken) =>
        _context.Users.AnyAsync(u => u.Email == email, cancellationToken);

    public async Task AddAsync(User user, CancellationToken cancellationToken)
    {
        await _context.Users.AddAsync(user, cancellationToken);
        await _context.SaveChangesAsync(cancellationToken);
    }

    public Task<IReadOnlyList<string>> GetRoleNamesAsync(int userId, CancellationToken cancellationToken) =>
        _context.UserRoles
            .Where(ur => ur.UserId == userId)
            .Select(ur => ur.Role.Name)
            .ToListAsync(cancellationToken)
            .ContinueWith(t => (IReadOnlyList<string>)t.Result, cancellationToken);

    public Task<IReadOnlyList<string>> GetPermissionNamesAsync(int userId, CancellationToken cancellationToken) =>
        _context.UserRoles
            .Where(ur => ur.UserId == userId)
            .SelectMany(ur => ur.Role.RolePermissions)
            .Select(rp => rp.Permission.Name)
            .Distinct()
            .ToListAsync(cancellationToken)
            .ContinueWith(t => (IReadOnlyList<string>)t.Result, cancellationToken);
}