using SchoolManagement.Application.DTOs.Timetable;

namespace SchoolManagement.Application.Interfaces;

public interface ITimetableService
{
    // ---- periods
    Task<List<TimetablePeriodResponse>> GetPeriodsAsync(int? academicYearId, CancellationToken cancellationToken);
    Task<TimetablePeriodResponse> CreatePeriodAsync(CreateTimetablePeriodRequest request, CancellationToken cancellationToken);
    Task<TimetablePeriodResponse> UpdatePeriodAsync(int id, UpdateTimetablePeriodRequest request, CancellationToken cancellationToken);
    Task DeletePeriodAsync(int id, CancellationToken cancellationToken);

    // ---- class grid
    Task<ClassTimetableResponse> GetClassTimetableAsync(int classSectionId, CancellationToken cancellationToken);
    Task<ClassTimetableResponse> SaveCellsAsync(int classSectionId, SaveTimetableCellsRequest request, CancellationToken cancellationToken);
    Task ClearCellAsync(int classSectionId, DayOfWeek day, int periodId, CancellationToken cancellationToken);
    Task<CopyTimetableResult> CopyFromAsync(int classSectionId, int sourceClassSectionId, CancellationToken cancellationToken);

    // ---- teacher's week
    /// <summary>
    /// A teacher's week in one academic year (default: the current one). With a scoped <paramref name="scope"/> the
    /// teacher must be the caller; any other teacher answers 404, the same as an unknown id.
    /// </summary>
    Task<TeacherTimetableResponse> GetTeacherTimetableAsync(
        int teacherId, int? academicYearId, DataScope scope, CancellationToken cancellationToken);

    // ---- used by Teachers and Subjects (wired in the next step)
    /// <summary>
    /// 409 if giving this subject to <paramref name="newTeacherId"/> would put that teacher in two classes at the same
    /// day and period. A null teacher (unassign) is always fine. Call it inside <see cref="RunLockedAsync{T}"/>.
    /// </summary>
    Task EnsureTeacherCanTakeSubjectAsync(int subjectId, int? newTeacherId, CancellationToken cancellationToken);

    /// <summary>409 if the subject is on any class's timetable (it cannot be moved to another class or deleted then).</summary>
    Task EnsureSubjectNotOnTimetableAsync(int subjectId, string action, CancellationToken cancellationToken);

    /// <summary>
    /// 409 if live lessons of the current academic year are on any of these days. Settings calls it before a
    /// working day is removed, so no lesson is left on a day the school is closed.
    /// </summary>
    Task EnsureDaysNotUsedAsync(IReadOnlyCollection<DayOfWeek> days, CancellationToken cancellationToken);

    /// <summary>Runs the operation under the same timetable lock the timetable saves use.</summary>
    Task<T> RunLockedAsync<T>(int academicYearId, Func<Task<T>> operation, CancellationToken cancellationToken);
}
