using SchoolManagement.Application.DTOs.Students;

namespace SchoolManagement.Application.Interfaces;

public interface IStudentService
{
    Task<List<StudentResponse>> GetAllAsync(CancellationToken cancellationToken);
    Task<StudentResponse> GetByIdAsync(int id, CancellationToken cancellationToken);
    Task<StudentResponse> CreateAsync(CreateStudentRequest request, CancellationToken cancellationToken);
    Task<StudentResponse> CreateFromAdmissionAsync(int admissionId, CreateStudentFromAdmissionRequest request, CancellationToken cancellationToken);
    Task<StudentResponse> UpdateAsync(int id, UpdateStudentRequest request, CancellationToken cancellationToken);
    Task DeleteAsync(int id, CancellationToken cancellationToken);
}