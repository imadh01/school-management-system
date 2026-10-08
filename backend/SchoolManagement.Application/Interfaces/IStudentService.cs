using SchoolManagement.Application.DTOs.Students;

namespace SchoolManagement.Application.Interfaces;

public interface IStudentService
{
    Task<List<StudentSummaryResponse>> GetAllAsync(CancellationToken cancellationToken);
    Task<StudentResponse> GetByIdAsync(int id, bool includeSensitive, CancellationToken cancellationToken);
    Task<StudentResponse> CreateAsync(CreateStudentRequest request, bool includeSensitive, CancellationToken cancellationToken);
    Task<StudentResponse> UpdateAsync(int id, UpdateStudentRequest request, bool includeSensitive, CancellationToken cancellationToken);
    Task<StudentResponse> UpdateIdentityAsync(int id, UpdateStudentIdentityRequest request, CancellationToken cancellationToken);
    Task<List<StudentEnrollmentResponse>> GetEnrollmentsAsync(int studentId, CancellationToken cancellationToken);
    Task DeleteAsync(int id, CancellationToken cancellationToken);
}
