using SchoolManagement.Application.DTOs.Parents;

namespace SchoolManagement.Application.Interfaces;

public interface IStudentGuardianService
{
    Task<List<StudentGuardianResponse>> GetForStudentAsync(int studentId, CancellationToken cancellationToken);
    Task<StudentGuardianResponse> LinkAsync(int studentId, LinkGuardianRequest request, CancellationToken cancellationToken);
    Task UnlinkAsync(int studentId, int parentId, CancellationToken cancellationToken);
}
