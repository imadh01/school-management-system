using Microsoft.EntityFrameworkCore;
using SchoolManagement.Application.Interfaces;
using SchoolManagement.Domain.Entities;
using SchoolManagement.Infrastructure.Persistence;

namespace SchoolManagement.Infrastructure.Repositories;

public class AcademicYearRepository : IAcademicYearRepository
{
    private readonly ApplicationDbContext _context;

    public AcademicYearRepository(ApplicationDbContext context)
    {
        _context = context;
    }

    public Task<AcademicYear?> GetCurrentAsync(CancellationToken cancellationToken) =>
        _context.AcademicYears.FirstOrDefaultAsync(a => a.IsCurrent, cancellationToken);

    public Task<AcademicYear?> GetByIdAsync(int id, CancellationToken cancellationToken) =>
        _context.AcademicYears.FirstOrDefaultAsync(a => a.Id == id, cancellationToken);
}
