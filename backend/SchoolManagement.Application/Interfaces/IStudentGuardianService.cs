using SchoolManagement.Application.DTOs.Parents;

namespace SchoolManagement.Application.Interfaces;

public interface IStudentGuardianService
{
    Task<List<StudentGuardianResponse>> GetForStudentAsync(int studentId, CancellationToken cancellationToken);
    Task<StudentGuardianResponse> LinkAsync(int studentId, LinkGuardianRequest request, CancellationToken cancellationToken);
    Task UnlinkAsync(int studentId, int parentId, CancellationToken cancellationToken);

    Task<List<LinkedStudentResponse>> GetForParentAsync(int parentId, CancellationToken cancellationToken);
    Task<LinkedStudentResponse> LinkStudentAsync(int parentId, LinkStudentRequest request, CancellationToken cancellationToken);
    Task SetPrimaryAsync(int parentId, int studentId, CancellationToken cancellationToken);
}
