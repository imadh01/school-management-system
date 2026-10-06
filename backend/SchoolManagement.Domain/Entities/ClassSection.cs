namespace SchoolManagement.Domain.Entities;

public class ClassSection
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

    public DateTime CreatedAt { get; set; }
    public DateTime UpdatedAt { get; set; }
    public int? CreatedBy { get; set; }
    public int? UpdatedBy { get; set; }

    public bool IsDeleted { get; set; }
    public DateTime? DeletedAt { get; set; }
    public int? DeletedBy { get; set; }

    /// <summary>Display label like "Class 1 A (2026-2027)" — computed, never stored.</summary>
    public string DisplayName => $"{Name} {Section} ({AcademicYear?.Name})";

    /// <summary>Short code like "C1A", "C10B", "LKGA" — computed, never stored.</summary>
    public string Code =>
        $"{Name.Trim().Replace("Class ", "C").Replace(" ", "")}{Section.Trim()}".ToUpperInvariant();
}