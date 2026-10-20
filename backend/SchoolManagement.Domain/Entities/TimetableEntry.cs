namespace SchoolManagement.Domain.Entities;

/// <summary>
/// One cell of a class's weekly grid: on <see cref="DayOfWeek"/> in <see cref="Period"/>, this class has
/// <see cref="Subject"/>. The teacher is not stored: it is whoever the subject's <c>TeacherId</c> is, so the
/// timetable can never disagree with the subject assignment. A cell is soft deleted when cleared, so the unique
/// index (one cell per class, day and period) only looks at live rows.
/// </summary>
public class TimetableEntry : IHasRowVersion
{
    public int Id { get; set; }

    public int ClassSectionId { get; set; }
    public ClassSection ClassSection { get; set; } = null!;

    public int PeriodId { get; set; }
    public TimetablePeriod Period { get; set; } = null!;

    /// <summary>Sunday = 0 ... Saturday = 6 (the .NET values), stored as a tinyint.</summary>
    public DayOfWeek DayOfWeek { get; set; }

    public int SubjectId { get; set; }
    public Subject Subject { get; set; } = null!;

    /// <summary>Optional room for this cell only. Null = the class section's own room.</summary>
    public string? Room { get; set; }

    public DateTime CreatedAt { get; set; }
    public DateTime UpdatedAt { get; set; }
    public int? CreatedBy { get; set; }
    public int? UpdatedBy { get; set; }

    public byte[] RowVersion { get; set; } = Array.Empty<byte>();

    public bool IsDeleted { get; set; }
    public DateTime? DeletedAt { get; set; }
    public int? DeletedBy { get; set; }
}
