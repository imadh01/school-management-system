using SchoolManagement.Domain.Auditing;
namespace SchoolManagement.Domain.Entities;

/// <summary>
/// One attendance-taking event: a class on a date, optionally for one subject.
/// SubjectId null = daily attendance; set = subject-wise attendance.
/// </summary>
[AuditIgnore]
public class AttendanceSession
{
    public int Id { get; set; }

    public int ClassSectionId { get; set; }
    public ClassSection ClassSection { get; set; } = null!;

    public DateOnly Date { get; set; }

    public int? SubjectId { get; set; }
    public Subject? Subject { get; set; }

    /// <summary>User who first saved this session.</summary>
    public int TakenByUserId { get; set; }

    public DateTime CreatedAt { get; set; }
    public DateTime UpdatedAt { get; set; }
    public int? CreatedBy { get; set; }
    public int? UpdatedBy { get; set; }

    public ICollection<AttendanceRecord> Records { get; set; } = new List<AttendanceRecord>();
}