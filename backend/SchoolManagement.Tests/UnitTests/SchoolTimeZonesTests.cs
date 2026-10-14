using SchoolManagement.Application.Services;
using Xunit;

namespace SchoolManagement.Tests.UnitTests;

/// <summary>E2: the time zones offered on the school settings page.</summary>
public class SchoolTimeZonesTests
{
    [Fact]
    public void India_is_listed_by_its_current_name_Asia_Kolkata()
    {
        var ids = SchoolTimeZones.All(DateTime.UtcNow).Select(o => o.Id).ToList();

        Assert.Contains("Asia/Kolkata", ids);
        Assert.DoesNotContain("Asia/Calcutta", ids); // the former name of the same zone
    }

    [Theory]
    [InlineData("Asia/Kolkata")]
    [InlineData("Asia/Dubai")]
    public void Common_school_zones_are_valid(string id) =>
        Assert.True(SchoolTimeZones.IsValidIanaId(id));

    [Theory]
    [InlineData("")]
    [InlineData("India Standard Time")] // a Windows id: refused so the stored value never depends on the server OS
    [InlineData("Mars/Olympus")]
    public void Other_text_is_refused(string id) =>
        Assert.False(SchoolTimeZones.IsValidIanaId(id));

    [Fact]
    public void The_list_has_no_duplicate_ids()
    {
        var ids = SchoolTimeZones.All(DateTime.UtcNow).Select(o => o.Id).ToList();
        Assert.Equal(ids.Count, ids.Distinct().Count());
    }
}
