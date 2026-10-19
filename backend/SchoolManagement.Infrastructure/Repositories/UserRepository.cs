using System.Data;
using Microsoft.Data.SqlClient;
using Microsoft.EntityFrameworkCore;
using SchoolManagement.Application.DTOs.Users;
using SchoolManagement.Application.Interfaces;
using SchoolManagement.Application.Services;
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

    public Task<User?> GetWithProfileAsync(int id, CancellationToken cancellationToken) =>
        _context.Users
            .Include(u => u.UserRoles).ThenInclude(ur => ur.Role)
            .Include(u => u.StaffProfile!).ThenInclude(p => p.Addresses).ThenInclude(a => a.Country)
            .Include(u => u.StaffProfile!).ThenInclude(p => p.Addresses).ThenInclude(a => a.Region)
            .AsSplitQuery() // one user, a few roles and addresses: no row multiplication
            .FirstOrDefaultAsync(u => u.Id == id, cancellationToken);

    // A projection: the list needs one line per user, never the password hash or the addresses.
    // The roles table stays many-to-many, but a user holds one role (service rule), so Role is the first.
    public Task<List<UserListItemResponse>> GetListAsync(DateTime nowUtc, CancellationToken cancellationToken) =>
        _context.Users
            .AsNoTracking()
            .OrderByDescending(u => u.Id)
            .Select(u => new UserListItemResponse(
                u.Id,
                u.Username,
                u.StaffProfile != null ? u.StaffProfile.FullName : u.Username,
                u.StaffProfile == null || u.StaffProfile.FullName == u.Username,
                u.Email,
                u.StaffProfile != null ? u.StaffProfile.Mobile : null,
                u.UserRoles.OrderBy(ur => ur.RoleId).Select(ur => ur.Role.Name).FirstOrDefault(),
                u.StaffProfile != null ? u.StaffProfile.Gender : null,
                u.Status,
                u.StaffProfile != null ? u.StaffProfile.JoiningDate : null,
                u.LastLoginAt,
                u.LockoutEnd != null && u.LockoutEnd > nowUtc,
                u.MustChangePassword,
                _context.Teachers.Where(t => t.UserId == u.Id).Select(t => (int?)t.Id).FirstOrDefault(),
                u.StaffProfile != null ? u.StaffProfile.EmployeeNumber : null))
            .ToListAsync(cancellationToken);

    public Task<int?> GetTeacherIdAsync(int userId, CancellationToken cancellationToken) =>
        _context.Teachers.Where(t => t.UserId == userId).Select(t => (int?)t.Id).FirstOrDefaultAsync(cancellationToken);

    public Task<User?> GetByUsernameOrEmailAsync(string usernameOrEmail, CancellationToken cancellationToken) =>
        _context.Users
            .Include(u => u.UserRoles)
                .ThenInclude(ur => ur.Role)
            .FirstOrDefaultAsync(
                u => u.Username == usernameOrEmail || u.Email == usernameOrEmail,
                cancellationToken);

    public Task<bool> ExistsByUsernameAsync(string username, int? excludeUserId, CancellationToken cancellationToken) =>
        _context.Users.AnyAsync(u => u.Username == username && u.Id != (excludeUserId ?? 0), cancellationToken);

    public Task<bool> ExistsByEmailAsync(string email, int? excludeUserId, CancellationToken cancellationToken) =>
        _context.Users.AnyAsync(u => u.Email == email && u.Id != (excludeUserId ?? 0), cancellationToken);

    public async Task<int> GetMaxEmployeeSerialAsync(string prefix, CancellationToken cancellationToken)
    {
        // Deleted staff keep their number reserved, so a number is never handed out twice.
        var numbers = await _context.StaffProfiles
            .IgnoreQueryFilters()
            .Where(p => p.EmployeeNumber != null && p.EmployeeNumber.StartsWith(prefix))
            .Select(p => p.EmployeeNumber!)
            .ToListAsync(cancellationToken);
        return NumberFormats.MaxSerial(numbers, prefix);
    }

    public Task<bool> ExistsByEmployeeNumberAsync(string employeeNumber, int? excludeUserId, CancellationToken cancellationToken) =>
        _context.StaffProfiles.AnyAsync(
            p => p.EmployeeNumber == employeeNumber && p.UserId != (excludeUserId ?? 0), cancellationToken);

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
                "A user with this username, email or employee number was just created by someone else. Use a different one.");
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

    // The services check "is this username/email/employee number taken?" first; the unique indexes catch
    // two saves at the same moment. Same 409 as AddAsync instead of a 500.
    public async Task SaveChangesAsync(CancellationToken cancellationToken)
    {
        try
        {
            await _context.SaveChangesAsync(cancellationToken);
        }
        catch (DbUpdateException ex) when (ex.InnerException is SqlException { Number: 2601 or 2627 })
        {
            throw new ConflictException(
                "This username, email or employee number was just taken by someone else. Use a different one.");
        }
    }
}