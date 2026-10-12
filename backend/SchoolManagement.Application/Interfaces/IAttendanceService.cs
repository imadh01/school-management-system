using SchoolManagement.Application.DTOs.Attendance;

namespace SchoolManagement.Application.Interfaces;

public interface IAttendanceService
{
    // Reads take the caller's DataScope (F1): Attendance.View → everything; Attendance.ViewScoped →
    // own classes (class teacher), own subjects, own children. Out of scope = 404, same body as missing.

    Task<AttendanceRosterResponse> GetRosterAsync(
        AttendanceActor actor, DataScope scope, int classSectionId, DateOnly date, int? subjectId,
        CancellationToken cancellationToken);

    /// <summary>Creates or replaces the whole roster for a class/date/subject in one transaction.</summary>
    Task<AttendanceRosterResponse> SaveAsync(
        AttendanceActor actor, SaveAttendanceRequest request, CancellationToken cancellationToken);

    /// <summary>Only the sessions the scope can see are listed AND counted in the summary.</summary>
    Task<StudentAttendanceResponse> GetStudentHistoryAsync(
        DataScope scope, int studentId, DateOnly? from, DateOnly? to, CancellationToken cancellationToken);

    /// <summary>Daily attendance of a whole class: scoped callers must be its class teacher this year.</summary>
    Task<ClassAttendanceSummaryResponse> GetSummaryAsync(
        DataScope scope, int classSectionId, DateOnly from, DateOnly to, CancellationToken cancellationToken);
}
