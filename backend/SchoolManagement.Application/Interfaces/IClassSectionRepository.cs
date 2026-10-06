using SchoolManagement.Domain.Entities;

namespace SchoolManagement.Application.Interfaces;

/// <summary>What still points at a class section — used to block deletion.</summary>
public record ClassSectionDependencies(int Students, int Admissions, int Subjects)
{
    public bool Any => Students > 0 || Admissions > 0 || Subjects > 0;
}

public interface IClassSectionRepository
{
    Task<List<ClassSection>> GetAllAsync(CancellationToken cancellationToken);
    Task<ClassSection?> GetByIdAsync(int id, CancellationToken cancellationToken);
    Task<bool> ExistsAsync(int academicYearId, string name, string section, int? excludeId, CancellationToken cancellationToken);
    Task<Dictionary<int, int>> GetEnrolledCountsAsync(CancellationToken cancellationToken);
    Task<int> CountActiveStudentsAsync(int classSectionId, CancellationToken cancellationToken);
    Task<ClassSectionDependencies> GetDependenciesAsync(int classSectionId, CancellationToken cancellationToken);
    Task AddAsync(ClassSection classSection, CancellationToken cancellationToken);
    Task SaveChangesAsync(CancellationToken cancellationToken);
}
