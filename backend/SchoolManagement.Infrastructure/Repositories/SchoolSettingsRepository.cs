using Microsoft.EntityFrameworkCore;
using SchoolManagement.Application.Interfaces;
using SchoolManagement.Domain.Entities;
using SchoolManagement.Infrastructure.Persistence;

namespace SchoolManagement.Infrastructure.Repositories;

public class SchoolSettingsRepository : ISchoolSettingsRepository
{
    private readonly ApplicationDbContext _context;

    public SchoolSettingsRepository(ApplicationDbContext context)
    {
        _context = context;
    }

    public async Task<SchoolSettings> GetAsync(CancellationToken cancellationToken) =>
        await _context.SchoolSettings
            .Include(s => s.Country)
            .FirstOrDefaultAsync(s => s.Id == SchoolSettings.SingletonId, cancellationToken)
        ?? throw new InvalidOperationException("The SchoolSettings row is missing. Run the E2a migration.");

    public Task<bool> CountryExistsAsync(int countryId, CancellationToken cancellationToken) =>
        _context.Countries.AnyAsync(c => c.Id == countryId && c.IsActive, cancellationToken);

    public Task SaveChangesAsync(CancellationToken cancellationToken) =>
        _context.SaveChangesAsync(cancellationToken);
}
