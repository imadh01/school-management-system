using SchoolManagement.Application.DTOs.Timetable;
using SchoolManagement.Application.Services;
using SchoolManagement.Application.Validators;
using Xunit;

namespace SchoolManagement.Tests.UnitTests;

/// <summary>The pure Timetable rules and the request validators: no database, no web host.</summary>
public class TimetableRulesUnitTests
{
    // ---- time text

    [Theory]
    [InlineData("08:00", true)]
    [InlineData("23:59", true)]
    [InlineData(" 08:45 ", true)]
    [InlineData("8:00", false)]
    [InlineData("08:00:00", false)]
    [InlineData("24:00", false)]
    [InlineData("8am", false)]
    [InlineData("", false)]
    [InlineData(null, false)]
    public void Only_HH_mm_is_accepted_as_a_time(string? text, bool valid) =>
        Assert.Equal(valid, TimetableRules.TryParseTime(text, out _));

    [Fact]
    public void A_time_is_written_back_as_HH_mm() =>
        Assert.Equal("09:05", TimetableRules.FormatTime(new TimeOnly(9, 5)));

    // ---- overlap

    [Theory]
    [InlineData("08:00", "08:45", "08:45", "09:30", false)] // touching ends
    [InlineData("08:00", "08:45", "08:30", "09:00", true)]  // partial
    [InlineData("08:00", "09:00", "08:15", "08:30", true)]  // one inside the other
    [InlineData("08:00", "08:45", "07:00", "08:00", false)] // touching, the other way
    [InlineData("08:00", "08:45", "10:00", "10:45", false)] // apart
    public void Two_periods_overlap_only_when_each_starts_before_the_other_ends(
        string startA, string endA, string startB, string endB, bool expected) =>
        Assert.Equal(expected, TimetableRules.Overlap(
            TimeOnly.Parse(startA), TimeOnly.Parse(endA), TimeOnly.Parse(startB), TimeOnly.Parse(endB)));

    // ---- days

    [Fact]
    public void Days_sort_Monday_first_and_Sunday_last()
    {
        var sorted = Enum.GetValues<DayOfWeek>().OrderBy(TimetableRules.DaySortKey).ToList();
        Assert.Equal(DayOfWeek.Monday, sorted.First());
        Assert.Equal(DayOfWeek.Sunday, sorted.Last());
    }

    [Theory]
    [InlineData(null, null)]
    [InlineData("", null)]
    [InlineData("   ", null)]
    [InlineData(" A-101 ", "A-101")]
    public void A_blank_room_means_no_room(string? room, string? expected) =>
        Assert.Equal(expected, TimetableRules.CleanRoom(room));

    // ---- period validators

    private static CreateTimetablePeriodRequest Period(
        string label = "Period 1", string start = "08:00", string end = "08:45", string kind = "Teaching", int order = 1) =>
        new(1, label, start, end, kind, order);

    [Fact]
    public void A_good_period_is_valid() =>
        Assert.True(new CreateTimetablePeriodRequestValidator().Validate(Period()).IsValid);

    [Theory]
    [InlineData("", "08:00", "08:45", "Teaching", 1)]            // no name
    [InlineData("P", "8:00", "08:45", "Teaching", 1)]            // bad start
    [InlineData("P", "08:00", "08:45pm", "Teaching", 1)]         // bad end
    [InlineData("P", "09:00", "09:00", "Teaching", 1)]           // zero length
    [InlineData("P", "09:00", "08:00", "Teaching", 1)]           // ends before it starts
    [InlineData("P", "08:00", "08:45", "Lunch", 1)]              // unknown type
    [InlineData("P", "08:00", "08:45", "Teaching", 0)]           // order too low
    [InlineData("P", "08:00", "08:45", "Teaching", 31)]          // order too high
    public void A_bad_period_is_refused(string label, string start, string end, string kind, int order) =>
        Assert.False(new CreateTimetablePeriodRequestValidator().Validate(Period(label, start, end, kind, order)).IsValid);

    [Fact]
    public void A_period_name_cannot_be_longer_than_50_characters() =>
        Assert.False(new CreateTimetablePeriodRequestValidator().Validate(Period(label: new string('x', 51))).IsValid);

    [Fact]
    public void Updating_a_period_needs_a_valid_row_version() =>
        Assert.False(new UpdateTimetablePeriodRequestValidator()
            .Validate(new UpdateTimetablePeriodRequest("P", "08:00", "08:45", "Teaching", 1, "not base64!")).IsValid);

    // ---- cell validators

    private static SaveTimetableCellsRequest Cells(params TimetableCellInput[] cells) => new(cells.ToList());

    [Fact]
    public void A_good_cell_request_is_valid() =>
        Assert.True(new SaveTimetableCellsRequestValidator()
            .Validate(Cells(new TimetableCellInput(1, 5, 7, "A-101"), new TimetableCellInput(2, 5, 7, null))).IsValid);

    [Fact]
    public void An_empty_cell_request_is_refused() =>
        Assert.False(new SaveTimetableCellsRequestValidator().Validate(Cells()).IsValid);

    [Theory]
    [InlineData(7, 1, 1)]   // day out of range
    [InlineData(-1, 1, 1)]
    [InlineData(1, 0, 1)]   // no period
    [InlineData(1, 1, 0)]   // no subject
    public void A_cell_with_a_bad_day_period_or_subject_is_refused(int day, int period, int subject) =>
        Assert.False(new SaveTimetableCellsRequestValidator().Validate(Cells(new TimetableCellInput(day, period, subject, null))).IsValid);

    [Fact]
    public void A_room_cannot_be_longer_than_30_characters() =>
        Assert.False(new SaveTimetableCellsRequestValidator().Validate(Cells(new TimetableCellInput(1, 1, 1, new string('r', 31)))).IsValid);

    [Fact]
    public void The_same_day_and_period_twice_in_one_request_is_refused() =>
        Assert.False(new SaveTimetableCellsRequestValidator().Validate(Cells(new TimetableCellInput(1, 5, 7, null), new TimetableCellInput(1, 5, 8, null))).IsValid);

    [Fact]
    public void More_than_200_cells_in_one_request_is_refused()
    {
        var many = Enumerable.Range(0, 201).Select(i => new TimetableCellInput(i % 7, i + 1, 1, null)).ToArray();
        Assert.False(new SaveTimetableCellsRequestValidator().Validate(Cells(many)).IsValid);
    }
}
