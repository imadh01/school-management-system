using SchoolManagement.Application.DTOs.Subjects;

namespace SchoolManagement.Application.Interfaces;

public interface ISubjectService
{
    Task<List<SubjectResponse>> GetAllAsync(CancellationToken cancellationToken);
    Task<SubjectResponse> GetByIdAsync(int id, CancellationToken cancellationToken);
    Task<SubjectResponse> CreateAsync(CreateSubjectRequest request, CancellationToken cancellationToken);
    Task<SubjectResponse> UpdateAsync(int id, UpdateSubjectRequest request, CancellationToken cancellationToken);
    Task DeleteAsync(int id, CancellationToken cancellationToken);
}
