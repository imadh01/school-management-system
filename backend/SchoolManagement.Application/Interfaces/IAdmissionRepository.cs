using SchoolManagement.Domain.Entities;

namespace SchoolManagement.Application.Interfaces;

public interface IAdmissionRepository
{
    Task<List<Admission>> GetAllAsync(CancellationToken cancellationToken);
    Task<Admission?> GetByIdAsync(int id, CancellationToken cancellationToken);
    Task AddAsync(Admission admission, CancellationToken cancellationToken);
    Task SaveChangesAsync(CancellationToken cancellationToken);
}