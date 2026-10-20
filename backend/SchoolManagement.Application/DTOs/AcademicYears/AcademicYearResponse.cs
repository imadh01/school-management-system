namespace SchoolManagement.Application.DTOs.AcademicYears;

/// <summary>
/// One academic year. Dates are yyyy-MM-dd; ClosedAt is an ISO 8601 UTC timestamp (null until closed).
/// The counts are live records only: class sections, Active student enrollments, timetable periods.
/// </summary>
public record AcademicYearResponse(
    int Id,
    string Name,
    string StartDate,
    string EndDate,
    bool IsCurrent,
    string Status,
    string RowVersion,
    string? ClosedAt,
    int ClassCount,
    int ActiveEnrollmentCount,
    int PeriodCount);
