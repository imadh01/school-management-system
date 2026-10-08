namespace SchoolManagement.Domain.Entities;

/// <summary>
/// Outcome of one enrollment period. Only <see cref="Active"/> is a "current" state; every
/// other value is a historical outcome recorded when the period was closed.
/// </summary>
public static class EnrollmentStatuses
{
    /// <summary>The student's current period (at most one per student).</summary>
    public const string Active = "Active";
    /// <summary>Year finished; the student moved up to the next grade.</summary>
    public const string Promoted = "Promoted";
    /// <summary>Year finished; the student stays in the same grade next year.</summary>
    public const string Repeated = "Repeated";
    /// <summary>Closed because the student moved to another class/section mid-year.</summary>
    public const string Transferred = "Transferred";
    /// <summary>Closed because the student left the school.</summary>
    public const string Left = "Left";

    public static readonly string[] All = { Active, Promoted, Repeated, Transferred, Left };
}
