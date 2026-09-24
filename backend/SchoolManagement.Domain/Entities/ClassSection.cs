namespace SchoolManagement.Domain.Entities;

public class ClassSection
{
    public int Id { get; set; }

    public int AcademicYearId { get; set; }
    public AcademicYear AcademicYear { get; set; } = null!;

    public string Name { get; set; } = string.Empty;
    public string Section { get; set; } = string.Empty;
    public int? Grade { get; set; }
    public int? Capacity { get; set; }
    public string? Room { get; set; }
    public string Status { get; set; } = "Active";

    public DateTime CreatedAt { get; set; }
    public DateTime UpdatedAt { get; set; }
    public int? CreatedBy { get; set; }
    public int? UpdatedBy { get; set; }

    public bool IsDeleted { get; set; }
    public DateTime? DeletedAt { get; set; }
    public int? DeletedBy { get; set; }

    /// <summary>Display label matching the prototype's "Class 1 A (2024-2025)"
    /// format — computed, never stored, per Decision #2.</summary>
    public string DisplayName => $"{Name} {Section} ({AcademicYear?.Name})";
}
