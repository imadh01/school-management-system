namespace SchoolManagement.Domain.Entities;

/// <summary>
/// Sensitive identity numbers of a student (1:1 with Student). Kept apart so reads of the
/// normal student record never touch them; the API masks them unless the caller has
/// the Students.ViewSensitive permission.
/// </summary>
public class StudentIdentityDocument
{
    public int StudentId { get; set; }
    public Student Student { get; set; } = null!;

    public string? AadhaarNumber { get; set; } // 12 digits, unique when present
    public string? PassportNumber { get; set; }
    public DateOnly? PassportExpiry { get; set; }
    public string? VisaType { get; set; }
    public DateOnly? VisaExpiry { get; set; }

    public DateTime CreatedAt { get; set; }
    public DateTime UpdatedAt { get; set; }
}
