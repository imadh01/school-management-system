using SchoolManagement.Domain.Entities;

namespace SchoolManagement.Application.Interfaces;

public interface IAcademicYearRepository
{
    Task<AcademicYear?> GetCurrentAsync(CancellationToken cancellationToken);
    Task<AcademicYear?> GetByIdAsync(int id, CancellationToken cancellationToken);
}
