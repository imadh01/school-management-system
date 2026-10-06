using SchoolManagement.Domain.Entities;
using SchoolManagement.Domain.Exceptions;

namespace SchoolManagement.Application.Services;

public static class ClassSectionRules
{
    /// <summary>Inactive sections can't take new admissions or students.</summary>
    public static void EnsureActive(ClassSection classSection)
    {
        if (classSection.Status != "Active")
            throw new ConflictException(
                $"'{classSection.DisplayName}' is inactive and cannot accept new admissions or students.");
    }

    /// <summary>A section at capacity can't take another student. No capacity set = no limit.</summary>
    public static void EnsureHasRoom(ClassSection classSection, int enrolled)
    {
        if (classSection.Capacity.HasValue && enrolled >= classSection.Capacity.Value)
            throw new ConflictException(
                $"'{classSection.DisplayName}' is full ({enrolled}/{classSection.Capacity}).");
    }
}
