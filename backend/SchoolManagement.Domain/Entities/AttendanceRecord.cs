namespace SchoolManagement.Domain.Entities;

/// <summary>One student's status within an <see cref="AttendanceSession"/>.</summary>
public class AttendanceRecord
{
    public int Id { get; set; }

    public int SessionId { get; set; }
    public AttendanceSession Session { get; set; } = null!;

    public int StudentId { get; set; }
    public Student Student { get; set; } = null!;

    /// <summary>Present | Absent | Late | Half Day | Leave.</summary>
    public string Status { get; set; } = "Present";

    public string? Remarks { get; set; }
}
