using SchoolManagement.Application.DTOs.Attendance;

namespace SchoolManagement.Application.Interfaces;

public interface IAttendanceService
{
    Task<AttendanceRosterResponse> GetRosterAsync(
        AttendanceActor actor, int classSectionId, DateOnly date, int? subjectId, CancellationToken cancellationToken);

    /// <summary>Creates or replaces the whole roster for a class/date/subject in one transaction.</summary>
    Task<AttendanceRosterResponse> SaveAsync(
        AttendanceActor actor, SaveAttendanceRequest request, CancellationToken cancellationToken);

    Task<StudentAttendanceResponse> GetStudentHistoryAsync(
        int studentId, DateOnly? from, DateOnly? to, CancellationToken cancellationToken);

    Task<ClassAttendanceSummaryResponse> GetSummaryAsync(
        int classSectionId, DateOnly from, DateOnly to, CancellationToken cancellationToken);
}
