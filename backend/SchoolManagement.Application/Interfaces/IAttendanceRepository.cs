using SchoolManagement.Domain.Entities;

namespace SchoolManagement.Application.Interfaces;

// Light projections so attendance never loads the 70-column Student row.
public record AttendanceStudentInfo(
    int Id,
    string RollNumber,
    string FirstName,
    string? MiddleName,
    string LastName,
    string AdmNo,
    int ClassSectionId,
    string Status);

public record AttendanceSessionRef(int Id, DateOnly Date);
public record AttendanceStatusRow(int SessionId, int StudentId, string Status);
public record AttendanceCountRow(int StudentId, string Status, int Count);
public record StudentAttendanceRow(DateOnly Date, string? SubjectName, string Status, string? Remarks);

public interface IAttendanceRepository
{
    Task<ClassSection?> GetClassSectionAsync(int id, CancellationToken cancellationToken);
    Task<Subject?> GetSubjectAsync(int id, CancellationToken cancellationToken);
    Task<int?> GetTeacherIdByUserIdAsync(int userId, CancellationToken cancellationToken);
    Task<string?> GetUsernameAsync(int userId, CancellationToken cancellationToken);

    /// <summary>The session for a class/date/subject with its records. Tracked only when forUpdate is true.</summary>
    Task<AttendanceSession?> GetSessionAsync(
        int classSectionId, DateOnly date, int? subjectId, bool forUpdate, CancellationToken cancellationToken);

    /// <summary>Active students of the class plus any extra students (e.g. ones with a saved record who have since left).</summary>
    Task<List<AttendanceStudentInfo>> GetRosterStudentsAsync(
        int classSectionId, IReadOnlyCollection<int> extraStudentIds, CancellationToken cancellationToken);

    Task<AttendanceStudentInfo?> GetStudentAsync(int studentId, CancellationToken cancellationToken);

    /// <summary>The most recent sessions of the same kind before a date, newest first.</summary>
    Task<List<AttendanceSessionRef>> GetRecentSessionsAsync(
        int classSectionId, DateOnly beforeDate, int? subjectId, int take, CancellationToken cancellationToken);

    Task<List<AttendanceStatusRow>> GetStatusRowsAsync(
        IReadOnlyCollection<int> sessionIds, CancellationToken cancellationToken);

    Task<List<StudentAttendanceRow>> GetStudentRecordsAsync(
        int studentId, DateOnly from, DateOnly to, CancellationToken cancellationToken);

    Task<List<AttendanceCountRow>> GetDailyCountsAsync(
        int classSectionId, DateOnly from, DateOnly to, CancellationToken cancellationToken);

    Task<int> CountDailySessionsAsync(
        int classSectionId, DateOnly from, DateOnly to, CancellationToken cancellationToken);

    Task AddSessionAsync(AttendanceSession session, CancellationToken cancellationToken);
    Task SaveChangesAsync(CancellationToken cancellationToken);
}