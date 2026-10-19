using SchoolManagement.Application.Services;
using Xunit;

namespace SchoolManagement.Tests.UnitTests;

/// <summary>
/// The year in REG-yyyy-nnnn and the registration date use the school's today, not the server's UTC date.
/// Abu Dhabi is UTC+4 all year: from 20:00 UTC on 31 December it is already 1 January there.
/// </summary>
public class SchoolTodayTests
{
    [Fact]
    public void A_few_hours_before_UTC_new_year_it_is_already_the_new_year_in_Abu_Dhabi()
    {
        var utc = new DateTimeOffset(2026, 12, 31, 21, 30, 0, TimeSpan.Zero);
        Assert.Equal(new DateOnly(2027, 1, 1), SchoolTimeZones.Today("Asia/Dubai", utc));
        Assert.Equal(new DateOnly(2026, 12, 31), SchoolTimeZones.Today("UTC", utc));
    }

    [Fact]
    public void Just_after_UTC_new_year_a_zone_behind_UTC_is_still_in_the_old_year()
    {
        var utc = new DateTimeOffset(2027, 1, 1, 2, 0, 0, TimeSpan.Zero);
        Assert.Equal(new DateOnly(2026, 12, 31), SchoolTimeZones.Today("America/New_York", utc));
    }

    [Fact]
    public void An_unknown_or_blank_zone_falls_back_to_UTC()
    {
        var utc = new DateTimeOffset(2026, 12, 31, 21, 30, 0, TimeSpan.Zero);
        Assert.Equal(new DateOnly(2026, 12, 31), SchoolTimeZones.Today("Not/AZone", utc));
        Assert.Equal(new DateOnly(2026, 12, 31), SchoolTimeZones.Today(null, utc));
    }
}
