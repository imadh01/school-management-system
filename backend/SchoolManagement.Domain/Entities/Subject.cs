namespace SchoolManagement.Domain.Entities;

public class Subject
{
    public int Id { get; set; }

    public string Name { get; set; } = string.Empty;
    public string Code { get; set; } = string.Empty;

    public int ClassSectionId { get; set; }
    public ClassSection ClassSection { get; set; } = null!;
    /// <summary>The teacher who teaches this subject in this class (null = unassigned).</summary>
    public int? TeacherId { get; set; }
    public Teacher? Teacher { get; set; }

    /// <summary>Theory | Practical | Both.</summary>
    public string Type { get; set; } = "Theory";

    // Populated when Type is Theory or Practical; null when Type is Both.
    public int? MaxMarks { get; set; }
    public int? PassMarks { get; set; }

    // Populated when Type is Both; null otherwise.
    public int? TheoryMax { get; set; }
    public int? TheoryPass { get; set; }
    public int? PracticalMax { get; set; }
    public int? PracticalPass { get; set; }

    public string Status { get; set; } = "Active";

    public DateTime CreatedAt { get; set; }
    public DateTime UpdatedAt { get; set; }
    public int? CreatedBy { get; set; }
    public int? UpdatedBy { get; set; }

    public bool IsDeleted { get; set; }
    public DateTime? DeletedAt { get; set; }
    public int? DeletedBy { get; set; }

}
