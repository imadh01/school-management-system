using SchoolManagement.Domain.Entities;

namespace SchoolManagement.Application.Interfaces;

public interface IParentRepository
{
    Task<List<Parent>> GetAllAsync(CancellationToken cancellationToken);
    Task<Parent?> GetByIdAsync(int id, CancellationToken cancellationToken);
    Task AddAsync(Parent parent, CancellationToken cancellationToken);
    Task SaveChangesAsync(CancellationToken cancellationToken);
}
