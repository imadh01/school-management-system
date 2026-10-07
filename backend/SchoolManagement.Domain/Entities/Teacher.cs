namespace SchoolManagement.Domain.Entities;

/// <summary>
/// Teacher profile. Login data (username, email, password hash, status) lives
/// on the linked User — nothing is duplicated here.
/// </summary>
public class Teacher
{
    public int Id { get; set; }

    public int UserId { get; set; }
    public User User { get; set; } = null!;

    public string Name { get; set; } = string.Empty;
    public string? Phone { get; set; }
    public string? Specialization { get; set; }

    public DateTime CreatedAt { get; set; }
    public DateTime UpdatedAt { get; set; }
    public int? CreatedBy { get; set; }
    public int? UpdatedBy { get; set; }

    public bool IsDeleted { get; set; }
    public DateTime? DeletedAt { get; set; }
    public int? DeletedBy { get; set; }
}
