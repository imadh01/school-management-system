namespace SchoolManagement.Domain.Entities;

public class AcademicYear
{
    public int Id { get; set; }

    public string Name { get; set; } = string.Empty;
    public DateOnly StartDate { get; set; }
    public DateOnly EndDate { get; set; }
    public bool IsCurrent { get; set; }
    public string Status { get; set; } = "Upcoming";

    public DateTime CreatedAt { get; set; }
    public DateTime UpdatedAt { get; set; }
    public int? CreatedBy { get; set; }
    public int? UpdatedBy { get; set; }

    public ICollection<ClassSection> ClassSections { get; set; } = new List<ClassSection>();
}