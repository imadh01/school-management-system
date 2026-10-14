using Moq;
using SchoolManagement.Application.Interfaces;
using SchoolManagement.Application.Services;
using Xunit;

namespace SchoolManagement.Tests.UnitTests;

/// <summary>F2: "today" is the date in the school's time zone, not the server's UTC date.</summary>
public class SchoolClockTests
{
    private static DateTimeOffset Utc(int y, int m, int d, int h, int min = 0) =>
        new(y, m, d, h, min, 0, TimeSpan.Zero);

    [Theory]
    // India is UTC+05:30: 20:00 UTC on the 14th is already 01:30 on the 15th.
    [InlineData("Asia/Kolkata", 2026, 10, 14, 20, 0, "2026-10-15")]
    [InlineData("Asia/Kolkata", 2026, 10, 14, 18, 29, "2026-10-14")] // 23:59 local: still the 14th
    [InlineData("Asia/Kolkata", 2026, 10, 14, 18, 30, "2026-10-15")] // 00:00 local: the 15th begins
    [InlineData("UTC", 2026, 10, 14, 20, 0, "2026-10-14")]
    // West of UTC the local date is BEHIND: 03:00 UTC is still the evening before in Los Angeles.
    [InlineData("America/Los_Angeles", 2026, 10, 14, 3, 0, "2026-10-13")]
    // Daylight saving is followed (London is UTC+1 in summer, so 23:30 UTC on 1 July is already the 2nd).
    [InlineData("Europe/London", 2026, 7, 1, 23, 30, "2026-07-02")]
    [InlineData("Europe/London", 2026, 12, 1, 23, 30, "2026-12-01")]
    public void Today_is_the_calendar_date_in_the_school_time_zone(
        string zone, int y, int m, int d, int h, int min, string expected)
    {
        Assert.Equal(DateOnly.Parse(expected), SchoolClock.TodayIn(zone, Utc(y, m, d, h, min)));
    }

    [Theory]
    [InlineData(null)]
    [InlineData("")]
    [InlineData("   ")]
    [InlineData("Not/A_Zone")]
    public void An_empty_or_unknown_zone_falls_back_to_UTC_which_is_how_it_behaved_before_F2(string? zone)
    {
        Assert.Equal(new DateOnly(2026, 10, 14), SchoolClock.TodayIn(zone, Utc(2026, 10, 14, 20)));
    }

    [Fact]
    public async Task The_clock_reads_the_zone_from_the_school_settings_and_the_instant_from_the_TimeProvider()
    {
        var settings = new Mock<ISchoolSettingsProvider>();
        settings.Setup(s => s.GetAsync(It.IsAny<CancellationToken>())).ReturnsAsync(
            new SchoolSettingsSnapshot("School", null, null, "Asia/Kolkata", "INR", 0));
        var clock = new SchoolClock(settings.Object, new FixedTime(Utc(2026, 10, 14, 20)));

        Assert.Equal(new DateOnly(2026, 10, 15), await clock.TodayAsync(CancellationToken.None));
    }

    [Fact]
    public async Task Changing_the_setting_changes_today_on_the_next_call()
    {
        var zone = "UTC";
        var settings = new Mock<ISchoolSettingsProvider>();
        settings.Setup(s => s.GetAsync(It.IsAny<CancellationToken>()))
            .ReturnsAsync(() => new SchoolSettingsSnapshot("School", null, null, zone, "INR", 0));
        var clock = new SchoolClock(settings.Object, new FixedTime(Utc(2026, 10, 14, 20)));

        Assert.Equal(new DateOnly(2026, 10, 14), await clock.TodayAsync(CancellationToken.None));
        zone = "Asia/Kolkata";
        Assert.Equal(new DateOnly(2026, 10, 15), await clock.TodayAsync(CancellationToken.None));
    }

    private sealed class FixedTime : TimeProvider
    {
        private readonly DateTimeOffset _now;
        public FixedTime(DateTimeOffset now) => _now = now;
        public override DateTimeOffset GetUtcNow() => _now;
    }
}
