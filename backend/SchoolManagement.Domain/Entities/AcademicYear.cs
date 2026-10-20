namespace SchoolManagement.Domain.Entities;

/// <summary>
/// Life of an academic year: Upcoming -> Current -> Ended -> Closed.
/// Upcoming: created, may hold classes and periods, not yet the working year.
/// Current: exactly one at a time; defaults and teacher data scope follow it.
/// Ended: another year became current. Its data is untouched and still editable by the usual rules.
/// Closed: read-only history. Set by an explicit Close; only an Ended year can be closed.
/// </summary>
public static class AcademicYearStatuses
{
    public const string Upcoming = "Upcoming";
    public const string Current = "Current";
    public const string Ended = "Ended";
    public const string Closed = "Closed";

    public static readonly string[] All = { Upcoming, Current, Ended, Closed };

    public static bool IsValid(string? value) => value is not null && All.Contains(value);
}

public class AcademicYear : IHasRowVersion
{
    public int Id { get; set; }

    public string Name { get; set; } = string.Empty;
    public DateOnly StartDate { get; set; }
    public DateOnly EndDate { get; set; }

    /// <summary>
    /// Mirror of <c>Status == Current</c>, kept so one filtered unique index can guarantee a single current year.
    /// Written only by the academic year service; a CHECK constraint rejects the two disagreeing.
    /// </summary>
    public bool IsCurrent { get; set; }

    /// <summary>See <see cref="AcademicYearStatuses"/>.</summary>
    public string Status { get; set; } = AcademicYearStatuses.Upcoming;

    public DateTime CreatedAt { get; set; }
    public DateTime UpdatedAt { get; set; }
    public int? CreatedBy { get; set; }
    public int? UpdatedBy { get; set; }

    public byte[] RowVersion { get; set; } = Array.Empty<byte>();

    public DateTime? ClosedAt { get; set; }
    public int? ClosedBy { get; set; }

    public bool IsDeleted { get; set; }
    public DateTime? DeletedAt { get; set; }
    public int? DeletedBy { get; set; }

    public ICollection<ClassSection> ClassSections { get; set; } = new List<ClassSection>();
}
