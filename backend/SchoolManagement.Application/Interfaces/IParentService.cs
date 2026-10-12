using SchoolManagement.Application.DTOs.Parents;

namespace SchoolManagement.Application.Interfaces;

public interface IParentService
{
    // Full shape for Parents.View, contact shape (name + mobile) for Parents.ViewScoped (F1).
    Task<List<ParentResponse>> GetAllAsync(CancellationToken cancellationToken);
    Task<List<ParentContactResponse>> GetAllScopedAsync(DataScope scope, CancellationToken cancellationToken);
    Task<ParentResponse> GetByIdAsync(int id, CancellationToken cancellationToken);
    /// <summary>404 (same body as a missing id) when the parent is outside <paramref name="scope"/>.</summary>
    Task<ParentContactResponse> GetByIdScopedAsync(int id, DataScope scope, CancellationToken cancellationToken);
    Task<ParentResponse> CreateAsync(CreateParentRequest request, CancellationToken cancellationToken);
    Task<ParentResponse> UpdateAsync(int id, UpdateParentRequest request, CancellationToken cancellationToken);
    Task DeleteAsync(int id, CancellationToken cancellationToken);
}
