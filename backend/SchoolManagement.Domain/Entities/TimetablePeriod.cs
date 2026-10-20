namespace SchoolManagement.Domain.Entities;

/// <summary>What a timetable period is: lessons are scheduled in Teaching periods, a Break (recess, lunch) holds no lesson.</summary>
public static class PeriodKinds
{
    public const string Teaching = "Teaching";
    public const string Break = "Break";

    public static readonly string[] All = { Teaching, Break };

    public static bool IsValid(string? value) => value is not null && All.Contains(value);
}

/// <summary>
/// One slot of the school day for one academic year ("Period 1", 08:00-08:45, or "Lunch"). The same periods are
/// shared by every class section of that year, so "Period 3" means the same clock time everywhere and a teacher
/// cannot be in two places in the same slot. Periods are soft deleted; the unique indexes ignore deleted rows.
/// </summary>
public class TimetablePeriod : IHasRowVersion
{
    public int Id { get; set; }

    public int AcademicYearId { get; set; }
    public AcademicYear AcademicYear { get; set; } = null!;

    /// <summary>Shown on the grid, e.g. "Period 1", "Lunch". Unique within the academic year.</summary>
    public string Label { get; set; } = string.Empty;

    /// <summary>School-local clock times (no time zone, no date): the school day is the same every day.</summary>
    public TimeOnly StartTime { get; set; }
    public TimeOnly EndTime { get; set; }

    /// <summary>Teaching | Break. See <see cref="PeriodKinds"/>.</summary>
    public string Kind { get; set; } = PeriodKinds.Teaching;

    /// <summary>Order of the rows on the grid, 1 = first of the day. Unique within the academic year.</summary>
    public int SortOrder { get; set; }

    public DateTime CreatedAt { get; set; }
    public DateTime UpdatedAt { get; set; }
    public int? CreatedBy { get; set; }
    public int? UpdatedBy { get; set; }

    public byte[] RowVersion { get; set; } = Array.Empty<byte>();

    public bool IsDeleted { get; set; }
    public DateTime? DeletedAt { get; set; }
    public int? DeletedBy { get; set; }
}
