using System.Data;
using Microsoft.Data.SqlClient;
using Microsoft.EntityFrameworkCore;
using SchoolManagement.Application.Interfaces;
using SchoolManagement.Domain.Constants;
using SchoolManagement.Domain.Entities;
using SchoolManagement.Domain.Exceptions;
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

    /// <summary>
    /// UserService checks "is this username/email taken?" first, but two admins creating the
    /// same user at the same moment can both pass that check. The unique index then rejects
    /// the second INSERT; report that as a 409 Conflict instead of a 500.
    /// </summary>
    public async Task AddAsync(User user, CancellationToken cancellationToken)
    {
        await _context.Users.AddAsync(user, cancellationToken);
        try
        {
            await _context.SaveChangesAsync(cancellationToken);
        }
        catch (DbUpdateException ex) when (ex.InnerException is SqlException { Number: 2601 or 2627 })
        {
            throw new ConflictException(
                "A user with this username or email was just created by someone else. Use a different one.");
        }
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

    public async Task<T> ExecuteInTransactionAsync<T>(Func<Task<T>> operation, CancellationToken cancellationToken)
    {
        var strategy = _context.Database.CreateExecutionStrategy();
        return await strategy.ExecuteAsync(async () =>
        {
            await using var transaction = await _context.Database.BeginTransactionAsync(cancellationToken);
            var result = await operation();
            await transaction.CommitAsync(cancellationToken);
            return result;
        });
    }

    // sp_getapplock locks a NAME, released automatically at COMMIT or ROLLBACK (Owner = Transaction).
    // One name for all account changes: they are rare, and one lock makes "is there another active
    // Admin?" safe whatever the change is (status, role or delete).
    public async Task LockAccountChangesAsync(CancellationToken cancellationToken)
    {
        if (_context.Database.CurrentTransaction is null)
            throw new InvalidOperationException("LockAccountChangesAsync must be called inside a transaction.");

        var result = new SqlParameter("@result", SqlDbType.Int) { Direction = ParameterDirection.Output };
        await _context.Database.ExecuteSqlRawAsync(
            "EXEC @result = sp_getapplock @Resource = @resource, @LockMode = 'Exclusive', " +
            "@LockOwner = 'Transaction', @LockTimeout = 10000;",
            new object[] { result, new SqlParameter("@resource", "UserAccountChanges") },
            cancellationToken);

        // 0 / 1 = granted. Negative = timeout (-1), cancelled (-2), deadlock victim (-3), error (-999).
        if ((int)result.Value < 0)
            throw new ConflictException("Another account change is being saved right now. Please try again.");
    }

    public Task<int> CountActiveAdminsAsync(int? excludeUserId, CancellationToken cancellationToken) =>
        _context.Users // the query filter already leaves out deleted users
            .Where(u => u.Status == "Active" && u.Id != (excludeUserId ?? 0))
            .CountAsync(u => u.UserRoles.Any(ur => ur.Role.Name == RoleNames.Admin), cancellationToken);

    public async Task SaveChangesAsync(CancellationToken cancellationToken)
    {
        await _context.SaveChangesAsync(cancellationToken);
    }
}