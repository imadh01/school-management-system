using Microsoft.EntityFrameworkCore;
using SchoolManagement.Application.Interfaces;
using SchoolManagement.Domain.Entities;
using SchoolManagement.Infrastructure.Persistence;

namespace SchoolManagement.Infrastructure.Repositories;

public class AdmissionFeeRepository : IAdmissionFeeRepository
{
    private readonly ApplicationDbContext _context;

    public AdmissionFeeRepository(ApplicationDbContext context)
    {
        _context = context;
    }

    public async Task<List<string>> GetClassNamesAsync(CancellationToken cancellationToken)
    {
        var rows = await _context.ClassSections.AsNoTracking()
            .Select(c => new { c.Name, c.Grade })
            .ToListAsync(cancellationToken);

        // Teaching order: LKG and UKG (no grade) first, then Class 1, 2, 3 ...
        return rows
            .GroupBy(r => r.Name)
            .OrderBy(g => g.Min(r => r.Grade ?? 0))
            .ThenBy(g => g.Key, StringComparer.OrdinalIgnoreCase)
            .Select(g => g.Key)
            .ToList();
    }

    public Task<List<AdmissionFee>> GetAllAsync(CancellationToken cancellationToken) =>
        _context.AdmissionFees.AsNoTracking().ToListAsync(cancellationToken);

    public Task<AdmissionFee?> GetByClassNameAsync(string className, CancellationToken cancellationToken) =>
        _context.AdmissionFees.FirstOrDefaultAsync(f => f.ClassName == className, cancellationToken);

    public async Task AddAsync(AdmissionFee fee, CancellationToken cancellationToken)
    {
        await _context.AdmissionFees.AddAsync(fee, cancellationToken);
        await _context.SaveChangesAsync(cancellationToken);
    }

    public async Task RemoveAsync(AdmissionFee fee, CancellationToken cancellationToken)
    {
        _context.AdmissionFees.Remove(fee);
        await _context.SaveChangesAsync(cancellationToken);
    }

    public Task SaveChangesAsync(CancellationToken cancellationToken) => _context.SaveChangesAsync(cancellationToken);
}
