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
    /// <summary>
    /// Makes this parent the student's one primary contact: the old primary is cleared and the new one set in
    /// ONE transaction. Returns false (changing nothing) when the student has no link to this parent.
    /// </summary>
    Task<bool> SetPrimaryAsync(int studentId, int parentId, CancellationToken cancellationToken);
}
