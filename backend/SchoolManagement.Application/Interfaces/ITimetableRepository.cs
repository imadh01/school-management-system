using SchoolManagement.Domain.Entities;

namespace SchoolManagement.Application.Interfaces;

/// <summary>Another class already has this teacher at the same day and period.</summary>
public record TimetableClash(string ClassName, DayOfWeek Day, string PeriodLabel);

public interface ITimetableRepository
{
    // ---- periods
    /// <summary>Live periods of one academic year, in grid order (not tracked).</summary>
    Task<List<TimetablePeriod>> GetPeriodsAsync(int academicYearId, CancellationToken cancellationToken);
    /// <summary>One live period, tracked for editing.</summary>
    Task<TimetablePeriod?> GetPeriodAsync(int id, CancellationToken cancellationToken);
    /// <summary>Saves the new period (409 if its label or order was taken at the same moment).</summary>
    Task AddPeriodAsync(TimetablePeriod period, CancellationToken cancellationToken);
    /// <summary>Live cells per period id for one academic year.</summary>
    Task<Dictionary<int, int>> CountCellsByPeriodAsync(int academicYearId, CancellationToken cancellationToken);
    Task<int> CountCellsInPeriodAsync(int periodId, CancellationToken cancellationToken);

    // ---- cells
    /// <summary>A class's live cells with subject, teacher and period loaded (not tracked).</summary>
    Task<List<TimetableEntry>> GetClassEntriesAsync(int classSectionId, CancellationToken cancellationToken);
    /// <summary>A class's live cells, tracked so they can be soft deleted.</summary>
    Task<List<TimetableEntry>> GetClassEntriesForUpdateAsync(int classSectionId, CancellationToken cancellationToken);
    /// <summary>The live cell at (class, day, period), tracked; null if the slot is empty.</summary>
    Task<TimetableEntry?> GetEntryAsync(int classSectionId, DayOfWeek day, int periodId, CancellationToken cancellationToken);
    void AddEntry(TimetableEntry entry);
    /// <summary>Every live cell whose subject is taught by this teacher in one academic year (not tracked).</summary>
    Task<List<TimetableEntry>> GetTeacherEntriesAsync(int teacherId, int academicYearId, CancellationToken cancellationToken);
    /// <summary>Every live cell of one subject, with its class and period (not tracked).</summary>
    Task<List<TimetableEntry>> GetSubjectEntriesAsync(int subjectId, CancellationToken cancellationToken);
    /// <summary>Active subjects of one class with their teachers (not tracked), for Copy From.</summary>
    Task<List<Subject>> GetClassSubjectsAsync(int classSectionId, CancellationToken cancellationToken);

    /// <summary>
    /// The cell, in a class other than <paramref name="excludeClassSectionId"/>, where this teacher already
    /// teaches on this day and period; null if the teacher is free.
    /// </summary>
    Task<TimetableClash?> FindTeacherClashAsync(
        int teacherId, DayOfWeek day, int periodId, int excludeClassSectionId, CancellationToken cancellationToken);

    /// <summary>How many live lessons (and in how many classes) of one academic year sit on any of these days.</summary>
    Task<(int Cells, int Classes)> CountCellsOnDaysAsync(
        int academicYearId, IReadOnlyCollection<DayOfWeek> days, CancellationToken cancellationToken);

    // ---- transaction and lock
    /// <summary>
    /// Runs <paramref name="operation"/> in a transaction that first takes the timetable lock of one academic
    /// year (sp_getapplock "Timetable:{id}"). Two saves that could create a teacher clash run one after the other,
    /// so the second one sees the first one's cells. Do not nest calls.
    /// </summary>
    Task<T> ExecuteLockedAsync<T>(int academicYearId, Func<Task<T>> operation, CancellationToken cancellationToken);

    Task SaveChangesAsync(CancellationToken cancellationToken);
}
