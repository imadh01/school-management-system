namespace SchoolManagement.Domain.Entities;

public class Parent
{
    public int Id { get; set; }

    public int? UserId { get; set; }
    public User? User { get; set; }

    public string Name { get; set; } = string.Empty;
    public string? Email { get; set; }
    public string Mobile { get; set; } = string.Empty;
    public string Status { get; set; } = "Active";

    public string? Occupation { get; set; }
    public string? Nationality { get; set; }
    public string? CountryOfResidence { get; set; }
    public string? Timezone { get; set; }
    public string? PreferredLanguage { get; set; }
    public string? PreferredContactMethod { get; set; }
    public string? Whatsapp { get; set; }
    public bool EmergencyOnly { get; set; }

    public bool NotifyAttendance { get; set; } = true;
    public bool NotifyExams { get; set; } = true;
    public bool NotifyFees { get; set; } = true;
    public bool NotifyNotices { get; set; } = true;
    public bool NotifyDiscipline { get; set; } = true;

    public string? Employer { get; set; }
    public string? JobTitle { get; set; }
    public string? WorkEmail { get; set; }
    public string? WorkPhone { get; set; }
    public bool BillingContact { get; set; }

    public string? AddressLine { get; set; }
    public string? City { get; set; }
    public string? State { get; set; }
    public string? Pincode { get; set; }

    public DateTime CreatedAt { get; set; }
    public DateTime UpdatedAt { get; set; }
    public int? CreatedBy { get; set; }
    public int? UpdatedBy { get; set; }
    public bool IsDeleted { get; set; }
    public DateTime? DeletedAt { get; set; }
    public int? DeletedBy { get; set; }

    public ICollection<StudentGuardian> StudentGuardians { get; set; } = new List<StudentGuardian>();
}
