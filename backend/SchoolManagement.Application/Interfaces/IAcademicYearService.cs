using SchoolManagement.Application.DTOs.AcademicYears;

namespace SchoolManagement.Application.Interfaces;

public interface IAcademicYearService
{
    Task<List<AcademicYearResponse>> GetAllAsync(string? status, CancellationToken cancellationToken);
    Task<AcademicYearResponse> GetByIdAsync(int id, CancellationToken cancellationToken);
    Task<AcademicYearResponse> CreateAsync(CreateAcademicYearRequest request, CancellationToken cancellationToken);
    Task<AcademicYearResponse> UpdateAsync(int id, UpdateAcademicYearRequest request, CancellationToken cancellationToken);

    /// <summary>Makes an Upcoming year current; the previous current year becomes Ended.</summary>
    Task<AcademicYearResponse> SetCurrentAsync(int id, SetCurrentAcademicYearRequest request, CancellationToken cancellationToken);

    /// <summary>Closes an Ended year for good (read-only history).</summary>
    Task<AcademicYearResponse> CloseAsync(int id, CloseAcademicYearRequest request, CancellationToken cancellationToken);

    Task DeleteAsync(int id, CancellationToken cancellationToken);

    /// <summary>Copies the periods of another year into this one (which must have none).</summary>
    Task<CopyPeriodsResult> CopyPeriodsAsync(int id, int fromYearId, CancellationToken cancellationToken);
}
