using SchoolManagement.Application.DTOs.Attendance;
using SchoolManagement.Application.Interfaces;
using SchoolManagement.Domain.Entities;
using SchoolManagement.Domain.Exceptions;

namespace SchoolManagement.Application.Services;

public class AttendanceService : IAttendanceService
{
    private const int LastSessionsShown = 7;

    private readonly IAttendanceRepository _attendance;
    private readonly TimeProvider _time;

    public AttendanceService(IAttendanceRepository attendance, TimeProvider time)
    {
        _attendance = attendance;
        _time = time;
    }

    // "Today" is the server's UTC date for now. If the school's local date can differ
    // from UTC during school hours, introduce a configured school time zone here.
    private DateOnly Today => DateOnly.FromDateTime(_time.GetUtcNow().UtcDateTime);

    // ---------- Roster ----------

    public async Task<AttendanceRosterResponse> GetRosterAsync(
        AttendanceActor actor, int classSectionId, DateOnly date, int? subjectId, CancellationToken cancellationToken)
    {
        if (date == default) throw new BusinessRuleException("Date is required.");

        var (classSection, subject) = await LoadScopeAsync(classSectionId, subjectId, cancellationToken);
        var access = await EvaluateAccessAsync(actor, classSection, subject, date, cancellationToken);
        return await BuildRosterAsync(classSection, subject, date, access, cancellationToken);
    }

    // ---------- Save (one transaction: session + all records in one SaveChanges) ----------

    public async Task<AttendanceRosterResponse> SaveAsync(
        AttendanceActor actor, SaveAttendanceRequest request, CancellationToken cancellationToken)
    {
        var (classSection, subject) = await LoadScopeAsync(request.ClassSectionId, request.SubjectId, cancellationToken);

        var access = await EvaluateAccessAsync(actor, classSection, subject, request.Date, cancellationToken);
        access.ThrowIfDenied();

        var session = await _attendance.GetSessionAsync(
            classSection.Id, request.Date, subject?.Id, forUpdate: true, cancellationToken);

        // Roster = active students of the class + anyone already recorded in this session.
        var existingStudentIds = session?.Records.Select(r => r.StudentId).ToList() ?? new List<int>();
        var rosterStudents = await _attendance.GetRosterStudentsAsync(classSection.Id, existingStudentIds, cancellationToken);
        var allowedIds = rosterStudents.Select(s => s.Id).ToHashSet();
        var submittedIds = request.Records.Select(r => r.StudentId).ToHashSet();

        if (submittedIds.Any(id => !allowedIds.Contains(id)))
            throw new BusinessRuleException("Some students do not belong to this class.");

        // Decision 4: a saved session is complete — every active student needs a status.
        var unmarked = rosterStudents
            .Where(s => IsActiveInClass(s, classSection.Id) && !submittedIds.Contains(s.Id))
            .ToList();
        if (unmarked.Count > 0)
        {
            var names = string.Join(", ", unmarked.Take(5).Select(FullName));
            var more = unmarked.Count > 5 ? $" and {unmarked.Count - 5} more" : string.Empty;
            throw new BusinessRuleException(
                $"{unmarked.Count} student(s) have no status yet: {names}{more}.");
        }

        var now = _time.GetUtcNow().UtcDateTime;

        if (session is null)
        {
            session = new AttendanceSession
            {
                ClassSectionId = classSection.Id,
                Date = request.Date,
                SubjectId = subject?.Id,
                TakenByUserId = actor.UserId,
                CreatedAt = now,
                CreatedBy = actor.UserId,
                UpdatedAt = now,
                UpdatedBy = actor.UserId,
            };
            foreach (var entry in request.Records)
                session.Records.Add(NewRecord(entry));

            await _attendance.AddSessionAsync(session, cancellationToken);
        }
        else
        {
            session.UpdatedAt = now;
            session.UpdatedBy = actor.UserId;

            foreach (var entry in request.Records)
            {
                var existing = session.Records.FirstOrDefault(r => r.StudentId == entry.StudentId);
                if (existing is null)
                {
                    session.Records.Add(NewRecord(entry));
                }
                else
                {
                    existing.Status = entry.Status;
                    existing.Remarks = Clean(entry.Remarks);
                }
            }

            await _attendance.SaveChangesAsync(cancellationToken);
        }

        return await BuildRosterAsync(classSection, subject, request.Date, access, cancellationToken);
    }

    // ---------- Student history ----------

    public async Task<StudentAttendanceResponse> GetStudentHistoryAsync(
        int studentId, DateOnly? from, DateOnly? to, CancellationToken cancellationToken)
    {
        var student = await _attendance.GetStudentAsync(studentId, cancellationToken)
            ?? throw new NotFoundException("Student could not be found.");

        var end = to ?? Today;
        var start = from ?? end.AddDays(-29);
        ValidateRange(start, end);

        var rows = await _attendance.GetStudentRecordsAsync(studentId, start, end, cancellationToken);

        // Percentage and counts use daily attendance only so subject-wise entries don't double count.
        var daily = rows.Where(r => r.SubjectName is null).ToList();
        var summary = AttendanceRules.BuildCounts(
            Count(daily, AttendanceRules.Present),
            Count(daily, AttendanceRules.Absent),
            Count(daily, AttendanceRules.Late),
            Count(daily, AttendanceRules.HalfDay),
            Count(daily, AttendanceRules.Leave));

        var records = rows
            .Select(r => new StudentAttendanceRecordResponse(
                r.Date, r.SubjectName is null ? "Daily" : "Subject Wise", r.SubjectName, r.Status, r.Remarks))
            .ToList();

        return new StudentAttendanceResponse(student.Id, FullName(student), student.AdmNo, start, end, summary, records);
    }

    // ---------- Class summary ----------

    public async Task<ClassAttendanceSummaryResponse> GetSummaryAsync(
        int classSectionId, DateOnly from, DateOnly to, CancellationToken cancellationToken)
    {
        if (from == default || to == default)
            throw new BusinessRuleException("Both 'from' and 'to' dates are required.");
        ValidateRange(from, to);

        var classSection = await _attendance.GetClassSectionAsync(classSectionId, cancellationToken)
            ?? throw new NotFoundException("Class section could not be found.");

        var counts = await _attendance.GetDailyCountsAsync(classSectionId, from, to, cancellationToken);
        var daysMarked = await _attendance.CountDailySessionsAsync(classSectionId, from, to, cancellationToken);

        var recordedIds = counts.Select(c => c.StudentId).Distinct().ToList();
        var students = await _attendance.GetRosterStudentsAsync(classSectionId, recordedIds, cancellationToken);

        var byStudent = counts.ToLookup(c => c.StudentId);
        int totalPresent = 0, totalAbsent = 0, totalLate = 0, totalHalf = 0;

        var rows = students.Select(s =>
        {
            var mine = byStudent[s.Id].ToDictionary(c => c.Status, c => c.Count);
            int Get(string status) => mine.GetValueOrDefault(status);

            totalPresent += Get(AttendanceRules.Present);
            totalAbsent += Get(AttendanceRules.Absent);
            totalLate += Get(AttendanceRules.Late);
            totalHalf += Get(AttendanceRules.HalfDay);

            return new AttendanceSummaryStudent(
                s.Id, s.RollNumber, FullName(s), s.AdmNo,
                AttendanceRules.BuildCounts(
                    Get(AttendanceRules.Present), Get(AttendanceRules.Absent), Get(AttendanceRules.Late),
                    Get(AttendanceRules.HalfDay), Get(AttendanceRules.Leave)));
        }).ToList();

        var classPercentage = AttendanceRules.Percentage(totalPresent, totalLate, totalHalf, totalAbsent);

        return new ClassAttendanceSummaryResponse(
            classSection.Id, classSection.DisplayName, from, to, daysMarked, classPercentage, rows);
    }

    // ---------- Helpers ----------

    private async Task<(ClassSection ClassSection, Subject? Subject)> LoadScopeAsync(
        int classSectionId, int? subjectId, CancellationToken cancellationToken)
    {
        var classSection = await _attendance.GetClassSectionAsync(classSectionId, cancellationToken)
            ?? throw new NotFoundException("Class section could not be found.");

        Subject? subject = null;
        if (subjectId.HasValue)
        {
            subject = await _attendance.GetSubjectAsync(subjectId.Value, cancellationToken)
                ?? throw new NotFoundException("Subject could not be found.");
            if (subject.ClassSectionId != classSection.Id)
                throw new BusinessRuleException("This subject does not belong to the selected class.");
        }

        return (classSection, subject);
    }

    /// <summary>
    /// Decisions 2 and 3. Scope first (who), then dates (when):
    /// teachers only mark their own class (daily) or their own subject; non-admins only the
    /// last 7 days; nobody marks the future.
    /// </summary>
    private async Task<AccessDecision> EvaluateAccessAsync(
        AttendanceActor actor, ClassSection classSection, Subject? subject, DateOnly date,
        CancellationToken cancellationToken)
    {
        if (!actor.CanManageAll)
        {
            var teacherId = await _attendance.GetTeacherIdByUserIdAsync(actor.UserId, cancellationToken);
            if (teacherId is null)
                return AccessDecision.Forbidden("You are not allowed to mark attendance.");

            if (subject is null && classSection.ClassTeacherId != teacherId)
                return AccessDecision.Forbidden("Only the class teacher can take daily attendance for this class.");

            if (subject is not null && subject.TeacherId != teacherId)
                return AccessDecision.Forbidden("Only the teacher assigned to this subject can take its attendance.");
        }

        var today = Today;
        if (date > today)
            return AccessDecision.RuleViolation("Attendance cannot be marked for a future date.");

        if (!actor.IsAdmin && date < today.AddDays(-AttendanceRules.EditWindowDays))
            return AccessDecision.RuleViolation(
                $"Only an Admin can change attendance older than {AttendanceRules.EditWindowDays} days.");

        return AccessDecision.Allowed;
    }

    private async Task<AttendanceRosterResponse> BuildRosterAsync(
        ClassSection classSection, Subject? subject, DateOnly date, AccessDecision access,
        CancellationToken cancellationToken)
    {
        var session = await _attendance.GetSessionAsync(
            classSection.Id, date, subject?.Id, forUpdate: false, cancellationToken);

        var saved = session?.Records.ToDictionary(r => r.StudentId) ?? new Dictionary<int, AttendanceRecord>();
        var students = await _attendance.GetRosterStudentsAsync(classSection.Id, saved.Keys.ToList(), cancellationToken);

        // Last 7 sessions of the same kind before this date → one dot each.
        var recent = await _attendance.GetRecentSessionsAsync(
            classSection.Id, date, subject?.Id, LastSessionsShown, cancellationToken);
        var statusLookup = recent.Count == 0
            ? new Dictionary<(int, int), string>()
            : (await _attendance.GetStatusRowsAsync(recent.Select(r => r.Id).ToList(), cancellationToken))
                .ToDictionary(r => (r.SessionId, r.StudentId), r => r.Status);
        var oldestFirst = recent.OrderBy(r => r.Date).ToList();

        var rosterStudents = students.Select(s =>
        {
            saved.TryGetValue(s.Id, out var record);
            var last7 = oldestFirst
                .Select(r => statusLookup.TryGetValue((r.Id, s.Id), out var st) ? st : null)
                .ToList();

            return new AttendanceRosterStudent(
                s.Id, s.RollNumber, FullName(s), s.AdmNo, IsActiveInClass(s, classSection.Id),
                record?.Status, record?.Remarks, last7);
        }).ToList();

        string? savedBy = null;
        if (session is not null)
            savedBy = await _attendance.GetUsernameAsync(session.UpdatedBy ?? session.TakenByUserId, cancellationToken);

        return new AttendanceRosterResponse(
            classSection.Id,
            classSection.DisplayName,
            date,
            subject?.Id,
            subject?.Name,
            IsSaved: session is not null,
            SavedAtUtc: session?.UpdatedAt,
            SavedBy: savedBy,
            CanEdit: access.IsAllowed,
            CannotEditReason: access.IsAllowed ? null : access.Message,
            PreviousSessionDate: recent.Count == 0 ? null : recent[0].Date,
            Students: rosterStudents);
    }

    private static AttendanceRecord NewRecord(AttendanceEntry entry) => new()
    {
        StudentId = entry.StudentId,
        Status = entry.Status,
        Remarks = Clean(entry.Remarks),
    };

    private static bool IsActiveInClass(AttendanceStudentInfo s, int classSectionId) =>
        s.Status == "Active" && s.ClassSectionId == classSectionId;

    private static string FullName(AttendanceStudentInfo s) =>
        string.Join(' ', new[] { s.FirstName, s.MiddleName, s.LastName }.Where(p => !string.IsNullOrWhiteSpace(p)));

    private static int Count(IEnumerable<StudentAttendanceRow> rows, string status) =>
        rows.Count(r => r.Status == status);

    private static string? Clean(string? value) =>
        string.IsNullOrWhiteSpace(value) ? null : value.Trim();

    private static void ValidateRange(DateOnly from, DateOnly to)
    {
        if (from > to)
            throw new BusinessRuleException("'From' date cannot be after 'to' date.");
        if (to.DayNumber - from.DayNumber > AttendanceRules.MaxRangeDays)
            throw new BusinessRuleException($"Date range cannot exceed {AttendanceRules.MaxRangeDays} days.");
    }

    /// <summary>Outcome of the "may this person edit this roster?" check.</summary>
    private readonly record struct AccessDecision(AccessKind Kind, string? Message)
    {
        public static AccessDecision Allowed => new(AccessKind.Allowed, null);
        public static AccessDecision Forbidden(string message) => new(AccessKind.Forbidden, message);
        public static AccessDecision RuleViolation(string message) => new(AccessKind.RuleViolation, message);

        public bool IsAllowed => Kind == AccessKind.Allowed;

        public void ThrowIfDenied()
        {
            switch (Kind)
            {
                case AccessKind.Forbidden: throw new ForbiddenException(Message!);
                case AccessKind.RuleViolation: throw new BusinessRuleException(Message!);
            }
        }
    }

    private enum AccessKind { Allowed, Forbidden, RuleViolation }
}