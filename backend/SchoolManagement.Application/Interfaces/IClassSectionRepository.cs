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

    /// <summary>
    /// Takes an exclusive lock on one class section's seats until the current transaction ends.
    /// Call it INSIDE a transaction, BEFORE counting seats: a second caller waits here until the
    /// first has committed, so it counts the seat the first one just took. Throws ConflictException
    /// if the lock cannot be obtained within a few seconds.
    /// </summary>
    Task LockSeatsAsync(int classSectionId, CancellationToken cancellationToken);
    Task<ClassSectionDependencies> GetDependenciesAsync(int classSectionId, CancellationToken cancellationToken);
    Task AddAsync(ClassSection classSection, CancellationToken cancellationToken);
    Task SaveChangesAsync(CancellationToken cancellationToken);
}