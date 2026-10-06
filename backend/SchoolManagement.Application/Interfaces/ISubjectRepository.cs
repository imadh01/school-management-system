using SchoolManagement.Domain.Entities;

namespace SchoolManagement.Application.Interfaces;

public interface ISubjectRepository
{
    Task<List<Subject>> GetAllAsync(CancellationToken cancellationToken);
    Task<Subject?> GetByIdAsync(int id, CancellationToken cancellationToken);
    Task<bool> ExistsWithCodeAsync(string code, int classSectionId, int? excludeId, CancellationToken cancellationToken);
    Task AddAsync(Subject subject, CancellationToken cancellationToken);
    Task SaveChangesAsync(CancellationToken cancellationToken);
}
