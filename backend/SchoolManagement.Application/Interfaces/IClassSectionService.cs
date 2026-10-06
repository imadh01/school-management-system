using SchoolManagement.Application.DTOs.ClassSections;

namespace SchoolManagement.Application.Interfaces;

public interface IClassSectionService
{
    Task<List<ClassSectionResponse>> GetAllAsync(CancellationToken cancellationToken);
    Task<ClassSectionResponse> GetByIdAsync(int id, CancellationToken cancellationToken);
    Task<ClassSectionResponse> CreateAsync(CreateClassSectionRequest request, CancellationToken cancellationToken);
    Task<ClassSectionResponse> UpdateAsync(int id, UpdateClassSectionRequest request, CancellationToken cancellationToken);
    Task<ClassSectionResponse> ChangeStatusAsync(int id, bool isActive, CancellationToken cancellationToken);
    Task DeleteAsync(int id, CancellationToken cancellationToken);
}
