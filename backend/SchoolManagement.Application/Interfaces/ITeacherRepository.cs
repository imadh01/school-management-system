using SchoolManagement.Domain.Entities;

namespace SchoolManagement.Application.Interfaces;

public record TeacherStats(int Classes, int Subjects, int ClassTeacherOf);

public interface ITeacherRepository
{
    Task<List<Teacher>> GetAllAsync(CancellationToken cancellationToken);
    Task<Teacher?> GetByIdAsync(int id, CancellationToken cancellationToken);
    Task<Dictionary<int, TeacherStats>> GetStatsAsync(CancellationToken cancellationToken);
    Task<List<Subject>> GetAssignedSubjectsAsync(int teacherId, CancellationToken cancellationToken);
    Task<List<int>> GetClassTeacherSectionIdsAsync(int teacherId, CancellationToken cancellationToken);
    Task<List<Subject>> GetSubjectsByIdsAsync(IReadOnlyCollection<int> ids, CancellationToken cancellationToken);
    Task<Subject?> GetSubjectAsync(int id, CancellationToken cancellationToken);
    Task<ClassSection?> GetClassSectionAsync(int id, CancellationToken cancellationToken);
    Task<int> CountSubjectsInClassAsync(int teacherId, int classSectionId, CancellationToken cancellationToken);
    Task<bool> UsernameExistsAsync(string username, int? excludeUserId, CancellationToken cancellationToken);
    Task<bool> EmailExistsAsync(string email, int? excludeUserId, CancellationToken cancellationToken);
    Task AddAsync(Teacher teacher, CancellationToken cancellationToken);
    Task SaveChangesAsync(CancellationToken cancellationToken);
}
