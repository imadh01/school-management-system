using SchoolManagement.Domain.Entities;

namespace SchoolManagement.Application.Interfaces;

public interface IParentRepository
{
    /// <summary>Parents visible under <paramref name="scope"/>, newest first (F1).</summary>
    Task<List<Parent>> GetAllAsync(DataScope scope, CancellationToken cancellationToken);
    /// <summary>The parent if it exists AND is visible under <paramref name="scope"/>; otherwise null (F1).</summary>
    Task<Parent?> GetVisibleByIdAsync(int id, DataScope scope, CancellationToken cancellationToken);
    Task<Parent?> GetByIdAsync(int id, CancellationToken cancellationToken);
    Task AddAsync(Parent parent, CancellationToken cancellationToken);
    Task SaveChangesAsync(CancellationToken cancellationToken);
}
