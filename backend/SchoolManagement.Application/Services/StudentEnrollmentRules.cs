using SchoolManagement.Domain.Entities;

namespace SchoolManagement.Application.Services;

/// <summary>
/// The only place that creates or closes enrollment periods, so Admissions and Students
/// follow identical rules.
/// </summary>
public static class StudentEnrollmentRules
{
    // TODO(school time zone): "today" is UTC for now, same as the rest of the system.
    public static DateOnly Today() => DateOnly.FromDateTime(DateTime.UtcNow);

    /// <summary>A new Active period in the given section (its academic year is taken from the section).</summary>
    public static StudentEnrollment StartNew(ClassSection classSection, string rollNumber, DateOnly startDate) => new()
    {
        AcademicYearId = classSection.AcademicYearId,
        ClassSectionId = classSection.Id,
        RollNumber = rollNumber,
        StartDate = startDate,
        Status = EnrollmentStatuses.Active,
    };

    /// <summary>Closes an Active period with a historical outcome. The end date can never precede the start date.</summary>
    public static void Close(StudentEnrollment enrollment, string outcome, DateOnly endDate, string? remarks = null)
    {
        if (outcome == EnrollmentStatuses.Active || !EnrollmentStatuses.All.Contains(outcome))
            throw new ArgumentException($"'{outcome}' is not a valid closing status.", nameof(outcome));

        enrollment.Status = outcome;
        enrollment.EndDate = endDate < enrollment.StartDate ? enrollment.StartDate : endDate;
        enrollment.Remarks = remarks ?? enrollment.Remarks;
        enrollment.UpdatedAt = DateTime.UtcNow;
    }
}