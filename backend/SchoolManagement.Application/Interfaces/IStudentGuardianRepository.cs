using SchoolManagement.Domain.Entities;

namespace SchoolManagement.Application.Interfaces;

public interface IStudentGuardianRepository
{
    Task<List<StudentGuardian>> GetForStudentAsync(int studentId, CancellationToken cancellationToken);
    /// <summary>The parent's links, only to students visible under <paramref name="studentScope"/> (F1).</summary>
    Task<List<StudentGuardian>> GetForParentAsync(int parentId, DataScope studentScope, CancellationToken cancellationToken);
    Task<bool> LinkExistsAsync(int studentId, int parentId, CancellationToken cancellationToken);
    Task AddLinkAsync(StudentGuardian link, CancellationToken cancellationToken);
    Task<StudentGuardian?> GetLinkAsync(int studentId, int parentId, CancellationToken cancellationToken);
    Task RemoveLinkAsync(StudentGuardian link, CancellationToken cancellationToken);
    Task SetPrimaryAsync(int studentId, int parentId, CancellationToken cancellationToken);
}
