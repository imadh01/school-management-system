using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.DependencyInjection;
using SchoolManagement.Application.Interfaces;
using SchoolManagement.Domain.Entities;

namespace SchoolManagement.Infrastructure.Persistence;

/// <summary>
/// Singleton: one copy of the school settings in memory, loaded on first use and after
/// <see cref="Invalidate"/>. Reads through a new scope, like the permission cache (a singleton must not
/// hold a request's DbContext).
/// </summary>
public class SchoolSettingsProvider : ISchoolSettingsProvider
{
    private readonly IServiceScopeFactory _scopeFactory;
    private volatile SchoolSettingsSnapshot? _current;

    public SchoolSettingsProvider(IServiceScopeFactory scopeFactory)
    {
        _scopeFactory = scopeFactory;
    }

    public async Task<SchoolSettingsSnapshot> GetAsync(CancellationToken cancellationToken)
    {
        // Two requests loading at the same moment both read the same row: harmless, no lock needed.
        if (_current is { } cached) return cached;

        using var scope = _scopeFactory.CreateScope();
        var db = scope.ServiceProvider.GetRequiredService<ApplicationDbContext>();
        var loaded = await db.SchoolSettings
            .AsNoTracking()
            .Where(s => s.Id == SchoolSettings.SingletonId)
            .Select(s => new SchoolSettingsSnapshot(
                s.SchoolName, s.ShortName, s.CountryId, s.TimeZoneId, s.CurrencyCode, s.WorkingDays))
            .FirstOrDefaultAsync(cancellationToken)
            ?? throw new InvalidOperationException("The SchoolSettings row is missing. Run the E2a migration.");

        _current = loaded;
        return loaded;
    }

    public void Invalidate() => _current = null;
}
