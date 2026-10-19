namespace SchoolManagement.Domain.Entities;

/// <summary>
/// Master data: the admission fee of one class (all its sections and academic years). One row per
/// class name, e.g. "Class 5". Confirm Admission pre-fills the amount from here; staff may still
/// change it for one applicant. A class with no row simply has no suggested fee.
/// </summary>
public class AdmissionFee
{
    public int Id { get; set; }

    /// <summary>The class name as on ClassSection.Name ("LKG", "Class 5"). Unique.</summary>
    public string ClassName { get; set; } = string.Empty;

    public decimal Amount { get; set; }

    public DateTime CreatedAt { get; set; }
    public DateTime UpdatedAt { get; set; }
    public int? CreatedBy { get; set; }
    public int? UpdatedBy { get; set; }
}
