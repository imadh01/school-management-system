using SchoolManagement.Domain.Entities;
using SchoolManagement.Domain.Exceptions;

namespace SchoolManagement.Application.Services;

/// <summary>Small rules about academic years that other services share.</summary>
public static class AcademicYearRules
{
    /// <summary>A Closed year is read-only history: nothing new may be added to it (409).</summary>
    public static void EnsureNotClosed(AcademicYear year)
    {
        if (year.Status == AcademicYearStatuses.Closed)
            throw new ConflictException($"{year.Name} is closed and read-only.");
    }
}
