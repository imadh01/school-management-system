using System.Data;
using Microsoft.Data.SqlClient;
using Microsoft.EntityFrameworkCore;
using SchoolManagement.Application.Interfaces;
using SchoolManagement.Domain.Entities;
using SchoolManagement.Domain.Exceptions;
using SchoolManagement.Infrastructure.Persistence;

namespace SchoolManagement.Infrastructure.Repositories;

public class TimetableRepository : ITimetableRepository
{
    private readonly ApplicationDbContext _context;

    public TimetableRepository(ApplicationDbContext context)
    {
        _context = context;
    }

    // ------------------------------------------------------------------ periods

    public Task<List<TimetablePeriod>> GetPeriodsAsync(int academicYearId, CancellationToken cancellationToken) =>
        _context.TimetablePeriods
            .AsNoTracking()
            .Where(p => p.AcademicYearId == academicYearId)
            .OrderBy(p => p.SortOrder)
            .ToListAsync(cancellationToken);

    public Task<TimetablePeriod?> GetPeriodAsync(int id, CancellationToken cancellationToken) =>
        _context.TimetablePeriods.FirstOrDefaultAsync(p => p.Id == id, cancellationToken);

    public async Task AddPeriodAsync(TimetablePeriod period, CancellationToken cancellationToken)
    {
        await _context.TimetablePeriods.AddAsync(period, cancellationToken);
        await SaveChangesAsync(cancellationToken);
    }

    public Task<Dictionary<int, int>> CountCellsByPeriodAsync(int academicYearId, CancellationToken cancellationToken) =>
        _context.TimetableEntries
            .Where(e => e.Period.AcademicYearId == academicYearId)
            .GroupBy(e => e.PeriodId)
            .Select(g => new { PeriodId = g.Key, Count = g.Count() })
            .ToDictionaryAsync(x => x.PeriodId, x => x.Count, cancellationToken);

    public Task<int> CountCellsInPeriodAsync(int periodId, CancellationToken cancellationToken) =>
        _context.TimetableEntries.CountAsync(e => e.PeriodId == periodId, cancellationToken);

    // ------------------------------------------------------------------ cells

    public Task<List<TimetableEntry>> GetClassEntriesAsync(int classSectionId, CancellationToken cancellationToken) =>
        _context.TimetableEntries
            .AsNoTracking()
            .Include(e => e.Period)
            .Include(e => e.Subject).ThenInclude(s => s.Teacher).ThenInclude(t => t!.User).ThenInclude(u => u.StaffProfile)
            .Where(e => e.ClassSectionId == classSectionId)
            .OrderBy(e => e.Period.SortOrder).ThenBy(e => e.DayOfWeek)
            .ToListAsync(cancellationToken);

    public Task<List<TimetableEntry>> GetClassEntriesForUpdateAsync(int classSectionId, CancellationToken cancellationToken) =>
        _context.TimetableEntries
            .Where(e => e.ClassSectionId == classSectionId)
            .ToListAsync(cancellationToken);

    public Task<TimetableEntry?> GetEntryAsync(int classSectionId, DayOfWeek day, int periodId, CancellationToken cancellationToken) =>
        _context.TimetableEntries.FirstOrDefaultAsync(
            e => e.ClassSectionId == classSectionId && e.DayOfWeek == day && e.PeriodId == periodId,
            cancellationToken);

    public void AddEntry(TimetableEntry entry) => _context.TimetableEntries.Add(entry);

    public Task<List<TimetableEntry>> GetTeacherEntriesAsync(int teacherId, int academicYearId, CancellationToken cancellationToken) =>
        _context.TimetableEntries
            .AsNoTracking()
            .Include(e => e.Period)
            .Include(e => e.Subject)
            .Include(e => e.ClassSection).ThenInclude(c => c.AcademicYear)
            .Where(e => e.Subject.TeacherId == teacherId && e.ClassSection.AcademicYearId == academicYearId)
            .OrderBy(e => e.Period.SortOrder).ThenBy(e => e.DayOfWeek)
            .ToListAsync(cancellationToken);

    public Task<List<TimetableEntry>> GetSubjectEntriesAsync(int subjectId, CancellationToken cancellationToken) =>
        _context.TimetableEntries
            .AsNoTracking()
            .Include(e => e.Period)
            .Include(e => e.ClassSection).ThenInclude(c => c.AcademicYear)
            .Where(e => e.SubjectId == subjectId)
            .ToListAsync(cancellationToken);

    public Task<List<Subject>> GetClassSubjectsAsync(int classSectionId, CancellationToken cancellationToken) =>
        _context.Subjects
            .AsNoTracking()
            .Include(s => s.Teacher).ThenInclude(t => t!.User).ThenInclude(u => u.StaffProfile)
            .Where(s => s.ClassSectionId == classSectionId && s.Status == "Active")
            .ToListAsync(cancellationToken);

    public async Task<TimetableClash?> FindTeacherClashAsync(
        int teacherId, DayOfWeek day, int periodId, int excludeClassSectionId, CancellationToken cancellationToken)
    {
        var hit = await _context.TimetableEntries
            .AsNoTracking()
            .Where(e => e.Subject.TeacherId == teacherId
                        && e.DayOfWeek == day
                        && e.PeriodId == periodId
                        && e.ClassSectionId != excludeClassSectionId)
            .Select(e => new
            {
                e.ClassSection.Name,
                e.ClassSection.Section,
                Year = e.ClassSection.AcademicYear.Name,
                e.DayOfWeek,
                PeriodLabel = e.Period.Label,
            })
            .FirstOrDefaultAsync(cancellationToken);

        return hit is null
            ? null
            : new TimetableClash(ClassSection.BuildDisplayName(hit.Name, hit.Section, hit.Year), hit.DayOfWeek, hit.PeriodLabel);
    }

    public async Task<(int Cells, int Classes)> CountCellsOnDaysAsync(
        int academicYearId, IReadOnlyCollection<DayOfWeek> days, CancellationToken cancellationToken)
    {
        var dayList = days.ToList();
        var onDays = _context.TimetableEntries
            .Where(e => e.ClassSection.AcademicYearId == academicYearId && dayList.Contains(e.DayOfWeek));
        var cells = await onDays.CountAsync(cancellationToken);
        var classes = await onDays.Select(e => e.ClassSectionId).Distinct().CountAsync(cancellationToken);
        return (cells, classes);
    }

    // ------------------------------------------------------------------ transaction and lock

    public async Task<T> ExecuteLockedAsync<T>(int academicYearId, Func<Task<T>> operation, CancellationToken cancellationToken)
    {
        var strategy = _context.Database.CreateExecutionStrategy();
        return await strategy.ExecuteAsync(async () =>
        {
            await using var transaction = await _context.Database.BeginTransactionAsync(cancellationToken);

            // sp_getapplock locks a NAME and is released at COMMIT or ROLLBACK (Owner = Transaction). One name per
            // academic year: every code path that can put a teacher into a slot takes it, so the clash check and
            // the save of one request are never interleaved with another request's.
            var result = new SqlParameter("@result", SqlDbType.Int) { Direction = ParameterDirection.Output };
            await _context.Database.ExecuteSqlRawAsync(
                "EXEC @result = sp_getapplock @Resource = @resource, @LockMode = 'Exclusive', " +
                "@LockOwner = 'Transaction', @LockTimeout = 10000;",
                new object[] { result, new SqlParameter("@resource", $"Timetable:{academicYearId}") },
                cancellationToken);

            // 0 / 1 = granted. Negative = timeout (-1), cancelled (-2), deadlock victim (-3), error (-999).
            if ((int)result.Value < 0)
                throw new ConflictException("The timetable is being updated by someone else right now. Please try again.");

            var value = await operation();
            await transaction.CommitAsync(cancellationToken);
            return value;
        });
    }

    // The services check first; the unique indexes catch two saves at the same moment as a 409 instead of a 500.
    public async Task SaveChangesAsync(CancellationToken cancellationToken)
    {
        try
        {
            await _context.SaveChangesAsync(cancellationToken);
        }
        catch (DbUpdateException ex) when (ex.InnerException is SqlException { Number: 2601 or 2627 })
        {
            throw new ConflictException(
                "Someone else changed this timetable at the same moment. Reload and try again.");
        }
    }
}
