using Microsoft.Data.SqlClient;
using Microsoft.EntityFrameworkCore;
using SchoolManagement.Application.Interfaces;
using SchoolManagement.Domain.Entities;
using SchoolManagement.Domain.Exceptions;
using SchoolManagement.Infrastructure.Persistence;

namespace SchoolManagement.Infrastructure.Repositories;

public class AdmissionRepository : IAdmissionRepository
{
    private readonly ApplicationDbContext _context;

    public AdmissionRepository(ApplicationDbContext context)
    {
        _context = context;
    }

    // Split query: Guardians is a collection, so a single JOIN would repeat every admission row per guardian.
    private IQueryable<Admission> WithIncludes() =>
        _context.Admissions
            .Include(a => a.AcademicYear)
            .Include(a => a.AppliedForClassSection)
            .Include(a => a.AllottedClassSection)
            .Include(a => a.Student)
            .Include(a => a.Guardians)
            .Include(a => a.Addresses).ThenInclude(x => x.Country)
            .Include(a => a.Addresses).ThenInclude(x => x.Region)
            .AsSplitQuery();

    public Task<List<Admission>> GetAllAsync(CancellationToken cancellationToken) =>
        WithIncludes().OrderByDescending(a => a.Id).ToListAsync(cancellationToken);

    public async Task<List<DeletedAdmissionRow>> GetDeletedAsync(CancellationToken cancellationToken)
    {
        var rows = await _context.Admissions.IgnoreQueryFilters()
            .Where(a => a.IsDeleted)
            .Select(a => new
            {
                a.Id, a.RegNo, a.FirstName, a.LastName,
                ClassName = a.AppliedForClassSection.Name,
                AcademicYearName = a.AcademicYear.Name,
                a.Status, a.Phone, a.DeletedAt, a.DeletedBy,
            })
            .ToListAsync(cancellationToken);

        var userIds = rows.Where(r => r.DeletedBy != null).Select(r => r.DeletedBy!.Value).Distinct().ToList();
        var names = await _context.Users.IgnoreQueryFilters()
            .Where(u => userIds.Contains(u.Id))
            .ToDictionaryAsync(u => u.Id, u => u.Username, cancellationToken);

        return rows
            .OrderByDescending(r => r.DeletedAt)
            .Select(r => new DeletedAdmissionRow(
                r.Id, r.RegNo, r.FirstName, r.LastName, r.ClassName, r.AcademicYearName, r.Status, r.Phone,
                r.DeletedAt ?? DateTime.MinValue,
                r.DeletedBy is { } id && names.TryGetValue(id, out var n) ? n : null))
            .ToList();
    }

    public Task<Admission?> GetByIdAsync(int id, CancellationToken cancellationToken) =>
        WithIncludes().FirstOrDefaultAsync(a => a.Id == id, cancellationToken);

    public async Task AddAsync(Admission admission, CancellationToken cancellationToken)
    {
        await _context.Admissions.AddAsync(admission, cancellationToken);
        await _context.SaveChangesAsync(cancellationToken);
    }

    /// <summary>
    /// Runs <paramref name="operation"/> in one database transaction: if it throws, everything
    /// it saved is rolled back. A unique-index violation (two staff enrolling the same
    /// application, same admission number, ...) becomes a 409 instead of a 500.
    /// </summary>
    public async Task<T> ExecuteInTransactionAsync<T>(Func<Task<T>> operation, CancellationToken cancellationToken)
    {
        var strategy = _context.Database.CreateExecutionStrategy();
        return await strategy.ExecuteAsync(async () =>
        {
            await using var transaction = await _context.Database.BeginTransactionAsync(cancellationToken);
            try
            {
                var result = await operation();
                await transaction.CommitAsync(cancellationToken);
                return result;
            }
            catch (DbUpdateException ex) when (ex.InnerException is SqlException { Number: 2601 or 2627 })
            {
                throw new ConflictException(
                    "This record conflicts with one just saved by someone else " +
                    "(for example the same admission number or roll number). Reload and try again.");
            }
        });
    }

    public async Task LockAdmissionNumbersAsync(int year, CancellationToken cancellationToken)
    {
        if (_context.Database.CurrentTransaction is null)
            throw new InvalidOperationException("LockAdmissionNumbersAsync must be called inside a transaction.");

        var result = new SqlParameter("@result", System.Data.SqlDbType.Int) { Direction = System.Data.ParameterDirection.Output };
        await _context.Database.ExecuteSqlRawAsync(
            "EXEC @result = sp_getapplock @Resource = @resource, @LockMode = 'Exclusive', " +
            "@LockOwner = 'Transaction', @LockTimeout = 10000;",
            new object[] { result, new SqlParameter("@resource", $"AdmissionNumber:{year}") },
            cancellationToken);

        if ((int)result.Value < 0)
            throw new ConflictException("Admission numbers are being issued right now. Please try again.");
    }

    public async Task LockForEnrolmentAsync(int admissionId, CancellationToken cancellationToken)
    {
        if (_context.Database.CurrentTransaction is null)
            throw new InvalidOperationException("LockForEnrolmentAsync must be called inside a transaction.");

        // Same technique as the class-seat lock (ClassSectionRepository.LockSeatsAsync): an application
        // lock owned by the transaction, released automatically on commit or rollback.
        var result = new SqlParameter("@result", System.Data.SqlDbType.Int) { Direction = System.Data.ParameterDirection.Output };
        await _context.Database.ExecuteSqlRawAsync(
            "EXEC @result = sp_getapplock @Resource = @resource, @LockMode = 'Exclusive', " +
            "@LockOwner = 'Transaction', @LockTimeout = 10000;",
            new object[] { result, new SqlParameter("@resource", $"AdmissionEnrol:{admissionId}") },
            cancellationToken);

        // 0 / 1 = granted. Negative = timeout (-1), cancelled (-2), deadlock victim (-3), error (-999).
        if ((int)result.Value < 0)
            throw new ConflictException("This application is being enrolled by someone else right now. Please try again.");
    }

    public Task SaveChangesAsync(CancellationToken cancellationToken) =>
        _context.SaveChangesAsync(cancellationToken);

    // The join happens in SQL on the two persisted MobileKey columns, so "same number"
    // is defined in exactly one place (MobileKeySql).
    public Task<List<ParentCandidateRow>> GetParentCandidatesAsync(int admissionId, CancellationToken cancellationToken) =>
        (from g in _context.AdmissionGuardians
         where g.AdmissionId == admissionId && g.MobileKey != null && g.MobileKey != ""
         join p in _context.Parents on g.MobileKey equals p.MobileKey
         orderby g.Id, p.Id
         select new ParentCandidateRow(g.Id, p.Id, p.Name, p.Mobile, p.Email))
        .ToListAsync(cancellationToken);

    public Task<List<ParentSummaryRow>> SearchParentsAsync(string? nameText, string? digits, int take, CancellationToken cancellationToken)
    {
        // EF turns Contains into a parameterised LIKE and escapes % and _ in the text.
        var hasName = !string.IsNullOrEmpty(nameText);
        var hasDigits = !string.IsNullOrEmpty(digits);
        if (!hasName && !hasDigits)
            return Task.FromResult(new List<ParentSummaryRow>());

        var parents = _context.Parents.AsNoTracking();
        var query =
            hasName && hasDigits ? parents.Where(p => p.Name.Contains(nameText!) || (p.MobileKey != null && p.MobileKey.Contains(digits!))) :
            hasName ? parents.Where(p => p.Name.Contains(nameText!)) :
            parents.Where(p => p.MobileKey != null && p.MobileKey.Contains(digits!));

        return query
            .OrderBy(p => p.Name).ThenBy(p => p.Id)
            .Take(take)
            .Select(p => new ParentSummaryRow(p.Id, p.Name, p.Mobile, p.Email))
            .ToListAsync(cancellationToken);
    }

    public Task<List<ParentChildRow>> GetLinkedChildrenAsync(IReadOnlyCollection<int> parentIds, CancellationToken cancellationToken) =>
        _context.StudentGuardians
            .Where(sg => parentIds.Contains(sg.ParentId))
            .Select(sg => new ParentChildRow(sg.ParentId, sg.Student.FirstName + " " + sg.Student.LastName))
            .ToListAsync(cancellationToken);
}
