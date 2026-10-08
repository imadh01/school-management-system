namespace SchoolManagement.Domain.Entities;

/// <summary>
/// One period during which a student belonged to one class section (with one roll number).
/// A student has many over time; exactly one is Active. Fees and exams will reference this
/// row so "Grade 6 results" stay tied to Grade 6 after the student moves on.
/// Students.ClassSectionId / RollNumber remain a copy of the Active row.
/// </summary>
public class StudentEnrollment
{
    public int Id { get; set; }

    public int StudentId { get; set; }
    public Student Student { get; set; } = null!;

    /// <summary>Always equals ClassSection.AcademicYearId; stored so the database can enforce uniqueness per year.</summary>
    public int AcademicYearId { get; set; }
    public AcademicYear AcademicYear { get; set; } = null!;

    public int ClassSectionId { get; set; }
    public ClassSection ClassSection { get; set; } = null!;

    public string RollNumber { get; set; } = string.Empty;

    public DateOnly StartDate { get; set; }
    /// <summary>Null while Active; set when the period is closed.</summary>
    public DateOnly? EndDate { get; set; }

    public string Status { get; set; } = EnrollmentStatuses.Active;
    public string? Remarks { get; set; }

    public DateTime CreatedAt { get; set; }
    public DateTime UpdatedAt { get; set; }
    public int? CreatedBy { get; set; }
    public int? UpdatedBy { get; set; }
}
