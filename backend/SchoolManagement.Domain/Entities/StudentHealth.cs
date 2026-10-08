namespace SchoolManagement.Domain.Entities;

/// <summary>Health and insurance details of a student (1:1 with Student, shares its key).</summary>
public class StudentHealth
{
    public int StudentId { get; set; }
    public Student Student { get; set; } = null!;

    public string? BloodGroup { get; set; }
    public string? Allergies { get; set; }
    public string? DietaryRequirements { get; set; } // comma-delimited tags
    public string? MedicalNotes { get; set; }
    public string? SpecialEducationalNeeds { get; set; }
    public string? InsuranceProvider { get; set; }
    public DateOnly? InsurancePolicyExpiry { get; set; }

    public DateTime CreatedAt { get; set; }
    public DateTime UpdatedAt { get; set; }
}
