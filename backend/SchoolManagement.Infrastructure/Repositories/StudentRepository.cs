using Microsoft.EntityFrameworkCore;
using SchoolManagement.Application.Interfaces;
using SchoolManagement.Domain.Entities;
using SchoolManagement.Infrastructure.Persistence;

namespace SchoolManagement.Infrastructure.Repositories;

public class StudentRepository : IStudentRepository
{
    private readonly ApplicationDbContext _context;

    public StudentRepository(ApplicationDbContext context)
    {
        _context = context;
    }

    private IQueryable<Student> WithIncludes() =>
        _context.Students
            .Include(s => s.ClassSection)
        .ThenInclude(c => c.AcademicYear)
            .Include(s => s.Admission);

    public Task<List<Student>> GetAllAsync(CancellationToken cancellationToken) =>
        WithIncludes().OrderByDescending(s => s.Id).ToListAsync(cancellationToken);

    public Task<Student?> GetByIdAsync(int id, CancellationToken cancellationToken) =>
        WithIncludes().FirstOrDefaultAsync(s => s.Id == id, cancellationToken);

    public Task<bool> ExistsByAdmNoAsync(string admNo, CancellationToken cancellationToken) =>
        _context.Students.AnyAsync(s => s.AdmNo == admNo, cancellationToken);

    public Task<bool> ExistsByRollNumberInClassAsync(int classSectionId, string rollNumber, CancellationToken cancellationToken) =>
        _context.Students.AnyAsync(
            s => s.ClassSectionId == classSectionId && s.RollNumber == rollNumber,
            cancellationToken);

    public async Task AddAsync(Student student, CancellationToken cancellationToken)
    {
        await _context.Students.AddAsync(student, cancellationToken);
        await _context.SaveChangesAsync(cancellationToken);
    }

    public Task SaveChangesAsync(CancellationToken cancellationToken) =>
        _context.SaveChangesAsync(cancellationToken);
}