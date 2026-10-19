using SchoolManagement.Application.DTOs.Attendance;

namespace SchoolManagement.Application.Services;

/// <summary>Approved attendance business rules in one place (decisions 1 and 3).</summary>
public static class AttendanceRules
{
    public const string Present = "Present";
    public const string Absent = "Absent";
    public const string Late = "Late";
    public const string HalfDay = "Half Day";
    public const string Leave = "Leave";

    public static readonly string[] Statuses = { Present, Absent, Late, HalfDay, Leave };

    /// <summary>
    /// Non-admins may only change attendance for today and the previous N days. N = 1 means
    /// "yesterday and today": about 24 hours after the register is taken (counted by calendar date,
    /// not by the minute the register was saved).
    /// </summary>
    public const int EditWindowDays = 1;

    /// <summary>
    /// A saved register is locked for non-admins this many hours after it was FIRST saved (an Admin
    /// can always change it). This is on top of the date window above, which stops back-filling
    /// registers that were never taken.
    /// </summary>
    public const int LockAfterHours = 24;

    /// <summary>Longest date range a history/summary query may cover.</summary>
    public const int MaxRangeDays = 366;

    /// <summary>
    /// One formula used everywhere: Present = 1, Late = 1, Half Day = 0.5, Absent = 0.
    /// Leave days are excluded from the denominator. Returns null when nothing counts.
    /// </summary>
    public static decimal? Percentage(int present, int late, int halfDay, int absent)
    {
        var denominator = present + late + halfDay + absent;
        if (denominator == 0) return null;
        var numerator = present + late + 0.5m * halfDay;
        return Math.Round(numerator / denominator * 100m, 1);
    }

    public static AttendanceCounts BuildCounts(int present, int absent, int late, int halfDay, int leave) =>
        new(present, absent, late, halfDay, leave,
            present + absent + late + halfDay + leave,
            Percentage(present, late, halfDay, absent));
}