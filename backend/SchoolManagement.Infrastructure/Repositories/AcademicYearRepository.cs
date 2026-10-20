using System.Data;
using Microsoft.Data.SqlClient;
using Microsoft.EntityFrameworkCore;
using SchoolManagement.Application.Interfaces;
using SchoolManagement.Domain.Entities;
using SchoolManagement.Domain.Exceptions;
using SchoolManagement.Infrastructure.Persistence;

namespace SchoolManagement.Infrastructure.Repositories;

public class AcademicYearRepository : IAcademicYearRepository
{
    private readonly ApplicationDbContext _context;

    public AcademicYearRepository(ApplicationDbContext context)
    {
        _context = context;
    }

    public Task<AcademicYear?> GetCurrentAsync(CancellationToken cancellationToken) =>
        _context.AcademicYears.FirstOrDefaultAsync(a => a.IsCurrent, cancellationToken);

    public Task<AcademicYear?> GetByIdAsync(int id, CancellationToken cancellationToken) =>
        _context.AcademicYears.FirstOrDefaultAsync(a => a.Id == id, cancellationToken);

    public Task<List<AcademicYear>> GetAllAsync(string? status, CancellationToken cancellationToken) =>
        _context.AcademicYears
            .AsNoTracking()
            .Where(a => status == null || a.Status == status)
            .OrderByDescending(a => a.StartDate)
            .ToListAsync(cancellationToken);

    public async Task<Dictionary<int, AcademicYearCounts>> GetCountsAsync(CancellationToken cancellationToken)
    {
        var classes = await _context.ClassSections
            .GroupBy(c => c.AcademicYearId)
            .Select(g => new { YearId = g.Key, Count = g.Count() })
            .ToDictionaryAsync(x => x.YearId, x => x.Count, cancellationToken);

        var enrollments = await _context.StudentEnrollments
            .Where(e => e.Status == EnrollmentStatuses.Active)
            .GroupBy(e => e.AcademicYearId)
            .Select(g => new { YearId = g.Key, Count = g.Count() })
            .ToDictionaryAsync(x => x.YearId, x => x.Count, cancellationToken);

        var periods = await _context.TimetablePeriods
            .GroupBy(p => p.AcademicYearId)
            .Select(g => new { YearId = g.Key, Count = g.Count() })
            .ToDictionaryAsync(x => x.YearId, x => x.Count, cancellationToken);

        var ids = classes.Keys.Union(enrollments.Keys).Union(periods.Keys);
        return ids.ToDictionary(
            id => id,
            id => new AcademicYearCounts(
                classes.GetValueOrDefault(id), enrollments.GetValueOrDefault(id), periods.GetValueOrDefault(id)));
    }

    public Task<bool> NameExistsAsync(string name, int? ignoreId, CancellationToken cancellationToken) =>
        _context.AcademicYears.AnyAsync(a => a.Name == name && (ignoreId == null || a.Id != ignoreId), cancellationToken);

    public Task<AcademicYear?> FindOverlappingAsync(
        DateOnly start, DateOnly end, int? ignoreId, CancellationToken cancellationToken) =>
        _context.AcademicYears
            .AsNoTracking()
            .Where(a => (ignoreId == null || a.Id != ignoreId) && a.StartDate <= end && start <= a.EndDate)
            .OrderBy(a => a.StartDate)
            .FirstOrDefaultAsync(cancellationToken);

    public async Task<(DateOnly? Earliest, DateOnly? Latest)> GetRecordedDateRangeAsync(int id, CancellationToken cancellationToken)
    {
        var sessionDates = _context.AttendanceSessions
            .Where(s => s.ClassSection.AcademicYearId == id)
            .Select(s => (DateOnly?)s.Date);
        var enrollmentStarts = _context.StudentEnrollments
            .Where(e => e.AcademicYearId == id)
            .Select(e => (DateOnly?)e.StartDate);
        var enrollmentEnds = _context.StudentEnrollments
            .Where(e => e.AcademicYearId == id && e.EndDate != null)
            .Select(e => e.EndDate);

        var mins = new[]
        {
            await sessionDates.MinAsync(cancellationToken),
            await enrollmentStarts.MinAsync(cancellationToken),
            await enrollmentEnds.MinAsync(cancellationToken),
        };
        var maxs = new[]
        {
            await sessionDates.MaxAsync(cancellationToken),
            await enrollmentStarts.MaxAsync(cancellationToken),
            await enrollmentEnds.MaxAsync(cancellationToken),
        };

        return (mins.Where(d => d.HasValue).Min(), maxs.Where(d => d.HasValue).Max());
    }

    public async Task AddAsync(AcademicYear year, CancellationToken cancellationToken)
    {
        await _context.AcademicYears.AddAsync(year, cancellationToken);
        await SaveChangesAsync(cancellationToken);
    }

    // The service checks first; the unique indexes catch two saves at the same moment as a 409 instead of a 500.
    public async Task SaveChangesAsync(CancellationToken cancellationToken)
    {
        try
        {
            await _context.SaveChangesAsync(cancellationToken);
        }
        catch (DbUpdateException ex) when (ex.InnerException is SqlException { Number: 2601 or 2627 })
        {
            throw new ConflictException("Someone else changed the academic years at the same moment. Reload and try again.");
        }
    }

    public async Task<T> ExecuteLockedAsync<T>(Func<Task<T>> operation, CancellationToken cancellationToken)
    {
        var strategy = _context.Database.CreateExecutionStrategy();
        return await strategy.ExecuteAsync(async () =>
        {
            await using var transaction = await _context.Database.BeginTransactionAsync(cancellationToken);

            // One lock name for every academic year change (create, edit, set current, close, delete): the overlap
            // and "one current year" checks read other rows, so these operations must not run at the same moment.
            // Same sp_getapplock pattern as TimetableRepository.ExecuteLockedAsync; released at COMMIT or ROLLBACK.
            var result = new SqlParameter("@result", SqlDbType.Int) { Direction = ParameterDirection.Output };
            await _context.Database.ExecuteSqlRawAsync(
                "EXEC @result = sp_getapplock @Resource = @resource, @LockMode = 'Exclusive', " +
                "@LockOwner = 'Transaction', @LockTimeout = 10000;",
                new object[] { result, new SqlParameter("@resource", "AcademicYears") },
                cancellationToken);

            if ((int)result.Value < 0)
                throw new ConflictException("The academic years are being updated by someone else right now. Please try again.");

            var value = await operation();
            await transaction.CommitAsync(cancellationToken);
            return value;
        });
    }
}
