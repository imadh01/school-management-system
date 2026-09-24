using SchoolManagement.Domain.Entities;

namespace SchoolManagement.Application.Interfaces;

public interface IClassSectionRepository
{
    Task<List<ClassSection>> GetAllAsync(CancellationToken cancellationToken);
    Task<bool> ExistsAsync(int academicYearId, string name, string section, CancellationToken cancellationToken);
    Task AddAsync(ClassSection classSection, CancellationToken cancellationToken);
}
