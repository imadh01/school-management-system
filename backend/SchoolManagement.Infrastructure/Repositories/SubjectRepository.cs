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
            .OrderByDescending(s => s.Id)
            .ToListAsync(cancellationToken);

    public Task<Subject?> GetByIdAsync(int id, CancellationToken cancellationToken) =>
        _context.Subjects
            .Include(s => s.ClassSection).ThenInclude(c => c.AcademicYear)
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
