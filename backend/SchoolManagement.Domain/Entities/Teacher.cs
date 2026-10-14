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

    /// <summary>E2c: the name and phone live on the staff profile (one copy). These read it, so the
    /// repositories that load a teacher for display must include User.StaffProfile. Read-only, so not mapped.</summary>
    public string Name => User?.StaffProfile?.FullName ?? User?.Username ?? string.Empty;
    public string? Phone => User?.StaffProfile?.Mobile;
    public string? Specialization { get; set; }

    public DateTime CreatedAt { get; set; }
    public DateTime UpdatedAt { get; set; }
    public int? CreatedBy { get; set; }
    public int? UpdatedBy { get; set; }

    public bool IsDeleted { get; set; }
    public DateTime? DeletedAt { get; set; }
    public int? DeletedBy { get; set; }
}
