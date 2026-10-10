namespace SchoolManagement.Domain.Entities;

public class ClassSection : IHasRowVersion
{
    public int Id { get; set; }

    public int AcademicYearId { get; set; }
    public AcademicYear AcademicYear { get; set; } = null!;

    public string Name { get; set; } = string.Empty;
    public string Section { get; set; } = string.Empty;
    public int? Grade { get; set; }

    /// <summary>Pre-Primary | Primary | Secondary.</summary>
    public string Stage { get; set; } = "Primary";
    /// <summary>English | Hindi | Arabic.</summary>
    public string Medium { get; set; } = "English";
    /// <summary>General | Science | Commerce.</summary>
    public string Stream { get; set; } = "General";

    public int? Capacity { get; set; }
    public string? Building { get; set; }
    public int? Floor { get; set; }
    public string? Room { get; set; }
    public string Status { get; set; } = "Active";
    /// <summary>Class (homeroom) teacher. Null = none assigned.</summary>
    public int? ClassTeacherId { get; set; }
    public Teacher? ClassTeacher { get; set; }

    public DateTime CreatedAt { get; set; }
    public DateTime UpdatedAt { get; set; }

    /// <summary>Concurrency stamp maintained by SQL Server (rowversion). Sent to the client and sent back on update.</summary>
    public byte[] RowVersion { get; set; } = Array.Empty<byte>();
    public int? CreatedBy { get; set; }
    public int? UpdatedBy { get; set; }

    public bool IsDeleted { get; set; }
    public DateTime? DeletedAt { get; set; }
    public int? DeletedBy { get; set; }

    /// <summary>Display label like "Class 1 A (2026-2027)" — computed, never stored.</summary>
    public string DisplayName => BuildDisplayName(Name, Section, AcademicYear?.Name);

    /// <summary>
    /// The single definition of the display label, usable where only raw columns are available
    /// (for example list queries that project name, section and year separately).
    /// </summary>
    public static string BuildDisplayName(string name, string section, string? academicYear) =>
        $"{name} {section} ({academicYear})";

    /// <summary>Short code like "C1A", "C10B", "LKGA" — computed, never stored.</summary>
    public string Code =>
        $"{Name.Trim().Replace("Class ", "C").Replace(" ", "")}{Section.Trim()}".ToUpperInvariant();
}