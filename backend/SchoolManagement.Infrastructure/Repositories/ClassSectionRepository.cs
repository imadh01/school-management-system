using Microsoft.EntityFrameworkCore;
using SchoolManagement.Application.Interfaces;
using SchoolManagement.Domain.Entities;
using SchoolManagement.Infrastructure.Persistence;

namespace SchoolManagement.Infrastructure.Repositories;

public class ClassSectionRepository : IClassSectionRepository
{
    private readonly ApplicationDbContext _context;

    public ClassSectionRepository(ApplicationDbContext context)
    {
        _context = context;
    }

    public Task<List<ClassSection>> GetAllAsync(CancellationToken cancellationToken) =>
        _context.ClassSections
            .Include(c => c.AcademicYear)
            .OrderBy(c => c.Name).ThenBy(c => c.Section)
            .ToListAsync(cancellationToken);

    public Task<bool> ExistsAsync(int academicYearId, string name, string section, CancellationToken cancellationToken) =>
        _context.ClassSections.AnyAsync(
            c => c.AcademicYearId == academicYearId && c.Name == name && c.Section == section,
            cancellationToken);

    public async Task AddAsync(ClassSection classSection, CancellationToken cancellationToken)
    {
        await _context.ClassSections.AddAsync(classSection, cancellationToken);
        await _context.SaveChangesAsync(cancellationToken);
    }
}
