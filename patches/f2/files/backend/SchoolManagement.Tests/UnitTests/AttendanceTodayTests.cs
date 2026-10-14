using Moq;
using SchoolManagement.Application.DTOs.Attendance;
using SchoolManagement.Application.Interfaces;
using SchoolManagement.Application.Services;
using SchoolManagement.Domain.Entities;
using Xunit;

namespace SchoolManagement.Tests.UnitTests;

/// <summary>
/// F2: the attendance rules ("no future dates", "non-admins edit only the last 7 days") use the SCHOOL's today.
/// Setup: the server's UTC date is the 14th, but it is already the 15th at the school (India).
/// </summary>
public class AttendanceTodayTests
{
    private static readonly DateOnly UtcToday = new(2026, 10, 14);
    private static readonly DateOnly SchoolToday = new(2026, 10, 15);

    private static readonly AttendanceActor Admin = new(UserId: 1, CanManageAll: true, IsAdmin: true);
    private static readonly AttendanceActor ClassTeacher = new(UserId: 2, CanManageAll: true, IsAdmin: false);

    private static (AttendanceService Service, Mock<IAttendanceRepository> Repo) Build(DateOnly schoolToday)
    {
        var section = new ClassSection
        {
            Id = 5, Name = "Class 1", Section = "A", AcademicYear = new AcademicYear { Name = "AY" },
        };
        var repo = new Mock<IAttendanceRepository>();
        repo.Setup(r => r.GetClassSectionAsync(5, It.IsAny<CancellationToken>())).ReturnsAsync(section);
        repo.Setup(r => r.CanSeeSessionsAsync(5, null, It.IsAny<DataScope>(), It.IsAny<CancellationToken>())).ReturnsAsync(true);
        repo.Setup(r => r.GetSessionAsync(5, It.IsAny<DateOnly>(), null, false, It.IsAny<CancellationToken>()))
            .ReturnsAsync((AttendanceSession?)null);
        repo.Setup(r => r.GetRosterStudentsAsync(5, It.IsAny<IReadOnlyCollection<int>>(), It.IsAny<CancellationToken>()))
            .ReturnsAsync(new List<AttendanceStudentInfo>());
        repo.Setup(r => r.GetRecentSessionsAsync(5, It.IsAny<DateOnly>(), null, It.IsAny<int>(), It.IsAny<CancellationToken>()))
            .ReturnsAsync(new List<AttendanceSessionRef>());

        var clock = new Mock<ISchoolClock>();
        clock.Setup(c => c.TodayAsync(It.IsAny<CancellationToken>())).ReturnsAsync(schoolToday);

        // The TimeProvider says it is the 14th (UTC); only the school clock knows it is the 15th.
        var time = new FixedTime(new DateTimeOffset(UtcToday.ToDateTime(new TimeOnly(20, 0)), TimeSpan.Zero));
        return (new AttendanceService(repo.Object, time, clock.Object), repo);
    }

    private static Task<AttendanceRosterResponse> RosterAsync(
        AttendanceService service, AttendanceActor actor, DateOnly date) =>
        service.GetRosterAsync(actor, DataScope.Unrestricted, 5, date, null, CancellationToken.None);

    [Fact]
    public async Task The_schools_today_can_be_marked_even_though_it_is_still_yesterday_in_UTC()
    {
        var (service, _) = Build(SchoolToday);

        var roster = await RosterAsync(service, Admin, SchoolToday);

        Assert.True(roster.CanEdit, roster.CannotEditReason);
    }

    [Fact]
    public async Task The_day_after_the_schools_today_is_a_future_date()
    {
        var (service, _) = Build(SchoolToday);

        var roster = await RosterAsync(service, Admin, SchoolToday.AddDays(1));

        Assert.False(roster.CanEdit);
        Assert.Contains("future", roster.CannotEditReason);
    }

    [Fact]
    public async Task The_seven_day_window_for_non_admins_counts_back_from_the_schools_today()
    {
        var (service, _) = Build(SchoolToday);

        var edge = await RosterAsync(service, ClassTeacher, SchoolToday.AddDays(-AttendanceRules.EditWindowDays));
        var tooOld = await RosterAsync(service, ClassTeacher, SchoolToday.AddDays(-AttendanceRules.EditWindowDays - 1));

        Assert.True(edge.CanEdit, edge.CannotEditReason);
        Assert.False(tooOld.CanEdit);
        Assert.Contains("older than", tooOld.CannotEditReason);
    }

    [Fact]
    public async Task A_student_history_without_dates_ends_on_the_schools_today()
    {
        var (service, repo) = Build(SchoolToday);
        repo.Setup(r => r.GetStudentAsync(9, It.IsAny<DataScope>(), It.IsAny<CancellationToken>()))
            .ReturnsAsync(new AttendanceStudentInfo(9, "Test", null, "Student", "A1", "1", 5, "Active"));
        repo.Setup(r => r.GetStudentRecordsAsync(9, It.IsAny<DateOnly>(), It.IsAny<DateOnly>(), It.IsAny<DataScope>(), It.IsAny<CancellationToken>()))
            .ReturnsAsync(new List<StudentAttendanceRow>());

        var history = await service.GetStudentHistoryAsync(DataScope.Unrestricted, 9, null, null, CancellationToken.None);

        Assert.Equal(SchoolToday, history.To);
        Assert.Equal(SchoolToday.AddDays(-29), history.From);
    }

    [Fact]
    public async Task Without_the_school_clock_the_same_request_would_have_been_refused_which_is_the_bug_F2_fixes()
    {
        // Documents the "before": with today = UTC date (the 14th), the school's 15th is a future date.
        var (service, _) = Build(UtcToday);

        var roster = await RosterAsync(service, Admin, SchoolToday);

        Assert.False(roster.CanEdit);
    }

    private sealed class FixedTime : TimeProvider
    {
        private readonly DateTimeOffset _now;
        public FixedTime(DateTimeOffset now) => _now = now;
        public override DateTimeOffset GetUtcNow() => _now;
    }
}
