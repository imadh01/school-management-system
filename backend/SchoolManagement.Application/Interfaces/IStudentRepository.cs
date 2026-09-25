using SchoolManagement.Domain.Entities;

namespace SchoolManagement.Application.Interfaces;

public interface IStudentRepository
{
    Task<List<Student>> GetAllAsync(CancellationToken cancellationToken);
    Task<Student?> GetByIdAsync(int id, CancellationToken cancellationToken);
    Task<bool> ExistsByAdmNoAsync(string admNo, CancellationToken cancellationToken);
    Task<bool> ExistsByRollNumberInClassAsync(int classSectionId, string rollNumber, CancellationToken cancellationToken);
    Task AddAsync(Student student, CancellationToken cancellationToken);
    Task SaveChangesAsync(CancellationToken cancellationToken);
}