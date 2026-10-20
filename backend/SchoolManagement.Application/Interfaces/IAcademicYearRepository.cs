using SchoolManagement.Domain.Entities;

namespace SchoolManagement.Application.Interfaces;

/// <summary>Live records that belong to one academic year.</summary>
public record AcademicYearCounts(int Classes, int ActiveEnrollments, int Periods);

public interface IAcademicYearRepository
{
    Task<AcademicYear?> GetCurrentAsync(CancellationToken cancellationToken);
    Task<AcademicYear?> GetByIdAsync(int id, CancellationToken cancellationToken);

    /// <summary>Every academic year, newest first (not tracked). A null status means all.</summary>
    Task<List<AcademicYear>> GetAllAsync(string? status, CancellationToken cancellationToken);

    /// <summary>Counts of classes, Active enrollments and periods for every year (years with none are missing).</summary>
    Task<Dictionary<int, AcademicYearCounts>> GetCountsAsync(CancellationToken cancellationToken);

    Task<bool> NameExistsAsync(string name, int? ignoreId, CancellationToken cancellationToken);

    /// <summary>Another live year whose dates overlap [start, end] (both ends inclusive), or null.</summary>
    Task<AcademicYear?> FindOverlappingAsync(DateOnly start, DateOnly end, int? ignoreId, CancellationToken cancellationToken);

    /// <summary>Earliest and latest date recorded against the year (attendance, enrollments); nulls if nothing yet.</summary>
    Task<(DateOnly? Earliest, DateOnly? Latest)> GetRecordedDateRangeAsync(int id, CancellationToken cancellationToken);

    Task AddAsync(AcademicYear year, CancellationToken cancellationToken);
    Task SaveChangesAsync(CancellationToken cancellationToken);

    /// <summary>
    /// Runs the operation in a transaction that holds one application lock for all academic year changes, so two
    /// creates, edits or "set current" calls never interleave. The lock does not nest.
    /// </summary>
    Task<T> ExecuteLockedAsync<T>(Func<Task<T>> operation, CancellationToken cancellationToken);
}
