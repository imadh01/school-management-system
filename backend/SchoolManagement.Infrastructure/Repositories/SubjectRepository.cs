using Microsoft.EntityFrameworkCore;
using SchoolManagement.Application.Interfaces;
using SchoolManagement.Domain.Entities;
using SchoolManagement.Infrastructure.Persistence;

namespace SchoolManagement.Infrastructure.Repositories;

public class SubjectRepository : ISubjectRepository
{
    private readonly ApplicationDbContext _context;

    public SubjectRepository(ApplicationDbContext context)
    {
        _context = context;
    }

    public Task<List<Subject>> GetAllAsync(CancellationToken cancellationToken) =>
           _context.Subjects
               .Include(s => s.ClassSection).ThenInclude(c => c.AcademicYear)
               .Include(s => s.Teacher)
               .OrderByDescending(s => s.ClassSection.AcademicYear.StartDate)
               .ThenBy(s => s.ClassSection.Grade ?? 0)
               .ThenBy(s => s.ClassSection.Name)
               .ThenBy(s => s.ClassSection.Section)
               .ThenBy(s => s.Name)
               .ToListAsync(cancellationToken);

    public Task<Subject?> GetByIdAsync(int id, CancellationToken cancellationToken) =>
        _context.Subjects
            .Include(s => s.ClassSection).ThenInclude(c => c.AcademicYear).Include(s => s.Teacher)
            .FirstOrDefaultAsync(s => s.Id == id, cancellationToken);

    public Task<bool> ExistsWithCodeAsync(string code, int classSectionId, int? excludeId, CancellationToken cancellationToken) =>
        _context.Subjects.AnyAsync(s =>
            s.Code == code && s.ClassSectionId == classSectionId && s.Id != (excludeId ?? 0),
            cancellationToken);

    public async Task AddAsync(Subject subject, CancellationToken cancellationToken)
    {
        await _context.Subjects.AddAsync(subject, cancellationToken);
        await _context.SaveChangesAsync(cancellationToken);
    }

    public Task SaveChangesAsync(CancellationToken cancellationToken) =>
        _context.SaveChangesAsync(cancellationToken);
}
