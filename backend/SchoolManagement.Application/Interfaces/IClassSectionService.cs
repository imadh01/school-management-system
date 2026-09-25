using SchoolManagement.Application.DTOs.ClassSections;

namespace SchoolManagement.Application.Interfaces;

public interface IClassSectionService
{
    Task<List<ClassSectionResponse>> GetAllAsync(CancellationToken cancellationToken);
    Task<ClassSectionResponse> CreateAsync(CreateClassSectionRequest request, CancellationToken cancellationToken);
}
