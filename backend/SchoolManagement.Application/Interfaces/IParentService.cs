using SchoolManagement.Application.DTOs.Parents;

namespace SchoolManagement.Application.Interfaces;

public interface IParentService
{
    Task<List<ParentResponse>> GetAllAsync(CancellationToken cancellationToken);
    Task<ParentResponse> GetByIdAsync(int id, CancellationToken cancellationToken);
    Task<ParentResponse> CreateAsync(CreateParentRequest request, CancellationToken cancellationToken);
    Task<ParentResponse> UpdateAsync(int id, UpdateParentRequest request, CancellationToken cancellationToken);
    Task DeleteAsync(int id, CancellationToken cancellationToken);
}
