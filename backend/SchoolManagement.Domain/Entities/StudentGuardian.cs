namespace SchoolManagement.Domain.Entities;

public class StudentGuardian
{
    public int StudentId { get; set; }
    public Student Student { get; set; } = null!;

    public int ParentId { get; set; }
    public Parent Parent { get; set; } = null!;

    public string RelationType { get; set; } = string.Empty;
    public bool IsPrimaryContact { get; set; }
}
