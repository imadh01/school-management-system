namespace SchoolManagement.Domain.Entities;

/// <summary>A person authorised to collect a student from school.</summary>
public class StudentPickupPerson
{
    public int Id { get; set; }

    public int StudentId { get; set; }
    public Student Student { get; set; } = null!;

    public string Name { get; set; } = string.Empty;
    public string Relation { get; set; } = string.Empty;
    public string Phone { get; set; } = string.Empty;
    public string? IdNote { get; set; } // e.g. "Emirates ID checked at gate"

    public DateTime CreatedAt { get; set; }
    public DateTime UpdatedAt { get; set; }
    public int? CreatedBy { get; set; }
    public int? UpdatedBy { get; set; }
}
