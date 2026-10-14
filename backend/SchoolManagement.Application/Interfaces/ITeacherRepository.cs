using SchoolManagement.Domain.Entities;

namespace SchoolManagement.Application.Interfaces;

public record TeacherStats(int Classes, int Subjects, int ClassTeacherOf);

public interface ITeacherRepository
{
    Task<List<Teacher>> GetAllAsync(CancellationToken cancellationToken);
    Task<Teacher?> GetByIdAsync(int id, CancellationToken cancellationToken);

    /// <summary>E2: the (non-deleted) teacher record of a login, tracked; null when the user is not a teacher.</summary>
    Task<Teacher?> GetByUserIdAsync(int userId, CancellationToken cancellationToken);
    Task<Dictionary<int, TeacherStats>> GetStatsAsync(CancellationToken cancellationToken);
    Task<List<Subject>> GetAssignedSubjectsAsync(int teacherId, CancellationToken cancellationToken);
    Task<List<int>> GetClassTeacherSectionIdsAsync(int teacherId, CancellationToken cancellationToken);
    Task<List<Subject>> GetSubjectsByIdsAsync(IReadOnlyCollection<int> ids, CancellationToken cancellationToken);
    Task<Subject?> GetSubjectAsync(int id, CancellationToken cancellationToken);
    Task<ClassSection?> GetClassSectionAsync(int id, CancellationToken cancellationToken);
    Task<int> CountSubjectsInClassAsync(int teacherId, int classSectionId, CancellationToken cancellationToken);
    /// <summary>E2: tracks a new teacher record without saving (it is saved with the account change around it).</summary>
    void Add(Teacher teacher);
    Task SaveChangesAsync(CancellationToken cancellationToken);
}
