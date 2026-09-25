using SchoolManagement.Domain.Entities;

namespace SchoolManagement.Application.Interfaces;

public interface IStudentGuardianRepository
{
    Task<List<StudentGuardian>> GetForStudentAsync(int studentId, CancellationToken cancellationToken);
    Task<bool> LinkExistsAsync(int studentId, int parentId, CancellationToken cancellationToken);
    Task AddLinkAsync(StudentGuardian link, CancellationToken cancellationToken);
    Task<StudentGuardian?> GetLinkAsync(int studentId, int parentId, CancellationToken cancellationToken);
    Task RemoveLinkAsync(StudentGuardian link, CancellationToken cancellationToken);
}
