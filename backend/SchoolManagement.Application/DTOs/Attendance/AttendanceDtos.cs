namespace SchoolManagement.Application.DTOs.Attendance;

/// <summary>Who is calling, as far as attendance rules care. Built by the controller from JWT claims.</summary>
/// <param name="CanManageAll">Holds Attendance.Manage — may mark any class (Admin, Supervisor, Clerk).</param>
/// <param name="IsAdmin">May edit attendance older than the normal edit window.</param>
public record AttendanceActor(int UserId, bool CanManageAll, bool IsAdmin);

// ---------- Save ----------

public record AttendanceEntry(int StudentId, string Status, string? Remarks);

/// <summary>SubjectId null = daily attendance; set = subject-wise attendance.</summary>
public record SaveAttendanceRequest(
    int ClassSectionId,
    DateOnly Date,
    int? SubjectId,
    List<AttendanceEntry> Records);

// ---------- Roster ----------

/// <param name="Last7">Statuses of the previous (up to) 7 sessions of the same kind, oldest first. null = student not recorded that day.</param>
public record AttendanceRosterStudent(
    int StudentId,
    string RollNumber,
    string Name,
    string AdmNo,
    bool IsActiveInClass,
    string? Status,
    string? Remarks,
    List<string?> Last7);

public record AttendanceRosterResponse(
    int ClassSectionId,
    string ClassName,
    DateOnly Date,
    int? SubjectId,
    string? SubjectName,
    bool IsSaved,
    DateTime? SavedAtUtc,
    string? SavedBy,
    bool CanEdit,
    string? CannotEditReason,
    DateOnly? PreviousSessionDate,
    List<AttendanceRosterStudent> Students);

// ---------- Counts / summaries ----------

/// <param name="Total">All recorded days including Leave. Percentage ignores Leave days.</param>
public record AttendanceCounts(
    int Present,
    int Absent,
    int Late,
    int HalfDay,
    int Leave,
    int Total,
    decimal? Percentage);

public record StudentAttendanceRecordResponse(
    DateOnly Date,
    string Mode,
    string? SubjectName,
    string Status,
    string? Remarks);

/// <summary>Summary counts use daily attendance only; Records lists daily and subject-wise entries.</summary>
public record StudentAttendanceResponse(
    int StudentId,
    string Name,
    string AdmNo,
    DateOnly From,
    DateOnly To,
    AttendanceCounts Summary,
    List<StudentAttendanceRecordResponse> Records);

public record AttendanceSummaryStudent(
    int StudentId,
    string RollNumber,
    string Name,
    string AdmNo,
    AttendanceCounts Counts);

public record ClassAttendanceSummaryResponse(
    int ClassSectionId,
    string ClassName,
    DateOnly From,
    DateOnly To,
    int DaysMarked,
    decimal? ClassPercentage,
    List<AttendanceSummaryStudent> Students);