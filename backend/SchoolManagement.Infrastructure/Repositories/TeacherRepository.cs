using Microsoft.EntityFrameworkCore;
using SchoolManagement.Application.Interfaces;
using SchoolManagement.Domain.Entities;
using SchoolManagement.Infrastructure.Persistence;

namespace SchoolManagement.Infrastructure.Repositories;

public class TeacherRepository : ITeacherRepository
{
    private readonly ApplicationDbContext _context;

    public TeacherRepository(ApplicationDbContext context)
    {
        _context = context;
    }

    public Task<List<Teacher>> GetAllAsync(CancellationToken cancellationToken) =>
        _context.Teachers.Include(t => t.User)
            .OrderBy(t => t.Name)
            .ToListAsync(cancellationToken);

    public Task<Teacher?> GetByIdAsync(int id, CancellationToken cancellationToken) =>
        _context.Teachers.Include(t => t.User)
            .FirstOrDefaultAsync(t => t.Id == id, cancellationToken);

    // Two grouped queries instead of loading every subject/class per teacher.
    public async Task<Dictionary<int, TeacherStats>> GetStatsAsync(CancellationToken cancellationToken)
    {
        var subjectRows = await _context.Subjects
            .Where(s => s.TeacherId != null)
            .GroupBy(s => s.TeacherId)
            .Select(g => new
            {
                g.Key,
                Subjects = g.Count(),
                Classes = g.Select(x => x.ClassSectionId).Distinct().Count(),
            })
            .ToListAsync(cancellationToken);

        var classTeacherRows = await _context.ClassSections
            .Where(c => c.ClassTeacherId != null)
            .GroupBy(c => c.ClassTeacherId)
            .Select(g => new { g.Key, Count = g.Count() })
            .ToListAsync(cancellationToken);

        var result = new Dictionary<int, TeacherStats>();
        foreach (var row in subjectRows)
            result[row.Key!.Value] = new TeacherStats(row.Classes, row.Subjects, 0);

        foreach (var row in classTeacherRows)
        {
            result.TryGetValue(row.Key!.Value, out var existing);
            result[row.Key!.Value] = (existing ?? new TeacherStats(0, 0, 0)) with { ClassTeacherOf = row.Count };
        }
        return result;
    }

    public Task<List<Subject>> GetAssignedSubjectsAsync(int teacherId, CancellationToken cancellationToken) =>
        _context.Subjects
            .Include(s => s.ClassSection).ThenInclude(c => c.AcademicYear)
            .Where(s => s.TeacherId == teacherId)
            .ToListAsync(cancellationToken);

    public Task<List<int>> GetClassTeacherSectionIdsAsync(int teacherId, CancellationToken cancellationToken) =>
        _context.ClassSections
            .Where(c => c.ClassTeacherId == teacherId)
            .Select(c => c.Id)
            .ToListAsync(cancellationToken);

    public Task<List<Subject>> GetSubjectsByIdsAsync(IReadOnlyCollection<int> ids, CancellationToken cancellationToken) =>
        _context.Subjects
            .Include(s => s.ClassSection)
            .Include(s => s.Teacher)
            .Where(s => ids.Contains(s.Id))
            .ToListAsync(cancellationToken);

    public Task<Subject?> GetSubjectAsync(int id, CancellationToken cancellationToken) =>
        _context.Subjects.FirstOrDefaultAsync(s => s.Id == id, cancellationToken);

    public Task<ClassSection?> GetClassSectionAsync(int id, CancellationToken cancellationToken) =>
        _context.ClassSections
            .Include(c => c.AcademicYear)
            .Include(c => c.ClassTeacher)
            .FirstOrDefaultAsync(c => c.Id == id, cancellationToken);

    public Task<int> CountSubjectsInClassAsync(int teacherId, int classSectionId, CancellationToken cancellationToken) =>
        _context.Subjects.CountAsync(
            s => s.TeacherId == teacherId && s.ClassSectionId == classSectionId, cancellationToken);

    public Task<bool> UsernameExistsAsync(string username, int? excludeUserId, CancellationToken cancellationToken) =>
        _context.Users.AnyAsync(u => u.Username == username && u.Id != (excludeUserId ?? 0), cancellationToken);

    public Task<bool> EmailExistsAsync(string email, int? excludeUserId, CancellationToken cancellationToken) =>
        _context.Users.AnyAsync(u => u.Email == email && u.Id != (excludeUserId ?? 0), cancellationToken);

    // Teacher + its new User + UserRole are one object graph → one SaveChanges,
    // which EF runs in a single transaction. No explicit transaction needed.
    public async Task AddAsync(Teacher teacher, CancellationToken cancellationToken)
    {
        await _context.Teachers.AddAsync(teacher, cancellationToken);
        await _context.SaveChangesAsync(cancellationToken);
    }

    public Task SaveChangesAsync(CancellationToken cancellationToken) =>
        _context.SaveChangesAsync(cancellationToken);
}
