using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.DependencyInjection;
using SchoolManagement.Application.DTOs.Attendance;
using SchoolManagement.Application.Interfaces;
using SchoolManagement.Domain.Entities;
using SchoolManagement.Infrastructure.Persistence;
using SchoolManagement.Tests.Support;
using Xunit;

namespace SchoolManagement.Tests.DatabaseRules;

/// <summary>
/// F2: the school time zone decides what "today" means for NEW requests. It never rewrites attendance that was
/// already saved: a session keeps exactly the date it was saved under, whatever the setting is later.
/// </summary>
[Collection("Database")]
public class AttendanceDatesAndTimeZoneTests
{
    private readonly SqlServerWebApplicationFactory _factory;

    public AttendanceDatesAndTimeZoneTests(SqlServerWebApplicationFactory factory) => _factory = factory;

    [Fact]
    public async Task Saved_attendance_keeps_its_dates_when_the_school_time_zone_changes()
    {
        using var scope = _factory.Services.CreateScope();
        var db = scope.ServiceProvider.GetRequiredService<ApplicationDbContext>();
        var attendance = scope.ServiceProvider.GetRequiredService<IAttendanceService>();
        var clock = scope.ServiceProvider.GetRequiredService<ISchoolClock>();
        var provider = scope.ServiceProvider.GetRequiredService<ISchoolSettingsProvider>();

        var section = await TestData.CreateSectionAsync(db, capacity: 10);
        var student = TestData.NewStudent(section, "1");
        db.Students.Add(student);
        await db.SaveChangesAsync();

        var teacher = new User
        {
            Username = "att_" + TestData.Unique(), Email = $"att{TestData.Unique()}@test.local", PasswordHash = "x", Status = "Active",
        };
        db.Users.Add(teacher);
        await db.SaveChangesAsync();
        var admin = new AttendanceActor(teacher.Id, CanManageAll: true, IsAdmin: true);
        var originalZone = (await db.SchoolSettings.SingleAsync()).TimeZoneId;

        try
        {
            var today = await clock.TodayAsync(CancellationToken.None);
            var days = new[] { today.AddDays(-1), today.AddDays(-3) };
            foreach (var day in days)
                await attendance.SaveAsync(admin, new SaveAttendanceRequest(
                    section.Id, day, null, new List<AttendanceEntry> { new(student.Id, "Present", null) }),
                    CancellationToken.None);

            var before = await StoredDatesAsync(db, section.Id);
            Assert.Equal(days.OrderBy(d => d), before);

            // The two extremes (UTC+14 and UTC-11) and India: "today" moves, saved dates must not.
            foreach (var zone in new[] { "Pacific/Kiritimati", "Pacific/Pago_Pago", "Asia/Kolkata" })
            {
                await SetZoneAsync(db, provider, zone);

                Assert.Equal(before, await StoredDatesAsync(db, section.Id));
                foreach (var day in days)
                {
                    var roster = await attendance.GetRosterAsync(
                        admin, DataScope.Unrestricted, section.Id, day, null, CancellationToken.None);
                    Assert.True(roster.IsSaved, $"The session of {day} disappeared under {zone}.");
                    Assert.Equal(day, roster.Date);
                }
            }
        }
        finally
        {
            await SetZoneAsync(db, provider, originalZone); // the other tests share this database
        }
    }

    [Fact]
    public async Task Changing_the_time_zone_changes_the_clock_for_the_very_next_request()
    {
        using var scope = _factory.Services.CreateScope();
        var db = scope.ServiceProvider.GetRequiredService<ApplicationDbContext>();
        var clock = scope.ServiceProvider.GetRequiredService<ISchoolClock>();
        var provider = scope.ServiceProvider.GetRequiredService<ISchoolSettingsProvider>();
        var originalZone = (await db.SchoolSettings.SingleAsync()).TimeZoneId;

        try
        {
            await SetZoneAsync(db, provider, "Pacific/Kiritimati"); // UTC+14: ahead of everyone
            var ahead = await clock.TodayAsync(CancellationToken.None);
            await SetZoneAsync(db, provider, "Pacific/Pago_Pago");  // UTC-11: behind everyone
            var behind = await clock.TodayAsync(CancellationToken.None);

            Assert.True(ahead > behind, $"Expected {ahead} (UTC+14) to be later than {behind} (UTC-11).");
        }
        finally
        {
            await SetZoneAsync(db, provider, originalZone);
        }
    }

    private static async Task SetZoneAsync(ApplicationDbContext db, ISchoolSettingsProvider provider, string zone)
    {
        var settings = await db.SchoolSettings.SingleAsync();
        settings.TimeZoneId = zone;
        await db.SaveChangesAsync();
        provider.Invalidate(); // what SchoolSettingsService does after its save
    }

    private static Task<List<DateOnly>> StoredDatesAsync(ApplicationDbContext db, int classSectionId) =>
        db.AttendanceSessions.AsNoTracking()
            .Where(s => s.ClassSectionId == classSectionId)
            .OrderBy(s => s.Date)
            .Select(s => s.Date)
            .ToListAsync();
}
