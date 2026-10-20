namespace SchoolManagement.Application.Services;

/// <summary>Small pure rules of the timetable, kept apart so they can be unit tested without a database.</summary>
public static class TimetableRules
{
    public const string TimeFormat = "HH:mm";

    /// <summary>"08:00" -> 08:00. Exactly HH:mm, 24-hour.</summary>
    public static bool TryParseTime(string? text, out TimeOnly time) =>
        TimeOnly.TryParseExact(text?.Trim(), TimeFormat, System.Globalization.CultureInfo.InvariantCulture,
            System.Globalization.DateTimeStyles.None, out time);

    public static string FormatTime(TimeOnly time) =>
        time.ToString(TimeFormat, System.Globalization.CultureInfo.InvariantCulture);

    /// <summary>Two periods overlap when each starts before the other ends (touching ends do not overlap).</summary>
    public static bool Overlap(TimeOnly startA, TimeOnly endA, TimeOnly startB, TimeOnly endB) =>
        startA < endB && startB < endA;

    /// <summary>Monday first, Sunday last, as the Settings page and the grid list days.</summary>
    public static int DaySortKey(DayOfWeek day) => ((int)day + 6) % 7;

    public static string? CleanRoom(string? room) =>
        string.IsNullOrWhiteSpace(room) ? null : room.Trim();
}
