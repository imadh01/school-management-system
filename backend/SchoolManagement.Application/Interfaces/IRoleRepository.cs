using SchoolManagement.Domain.Entities;

namespace SchoolManagement.Application.Interfaces;

public interface IRoleRepository
{
    /// <summary>Used when assigning a default role (e.g. "Student") at registration.</summary>
    Task<Role?> GetByNameAsync(string name, CancellationToken cancellationToken);
}