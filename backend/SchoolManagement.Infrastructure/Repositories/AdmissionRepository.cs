using Microsoft.EntityFrameworkCore;
using SchoolManagement.Application.Interfaces;
using SchoolManagement.Domain.Entities;
using SchoolManagement.Infrastructure.Persistence;

namespace SchoolManagement.Infrastructure.Repositories;

public class AdmissionRepository : IAdmissionRepository
{
    private readonly ApplicationDbContext _context;

    public AdmissionRepository(ApplicationDbContext context)
    {
        _context = context;
    }

    private IQueryable<Admission> WithIncludes() =>
        _context.Admissions
            .Include(a => a.AcademicYear)
            .Include(a => a.AppliedForClassSection)
            .Include(a => a.AllottedClassSection);

    public Task<List<Admission>> GetAllAsync(CancellationToken cancellationToken) =>
        WithIncludes().OrderByDescending(a => a.Id).ToListAsync(cancellationToken);

    public Task<Admission?> GetByIdAsync(int id, CancellationToken cancellationToken) =>
        WithIncludes().FirstOrDefaultAsync(a => a.Id == id, cancellationToken);

    public async Task AddAsync(Admission admission, CancellationToken cancellationToken)
    {
        await _context.Admissions.AddAsync(admission, cancellationToken);
        await _context.SaveChangesAsync(cancellationToken);
    }

    public Task SaveChangesAsync(CancellationToken cancellationToken) =>
        _context.SaveChangesAsync(cancellationToken);
}