using SchoolManagement.Domain.Entities;

namespace SchoolManagement.Application.Interfaces;

public interface ISchoolSettingsRepository
{
    /// <summary>The single row with its country, tracked. Throws when the E2a migration has not run.</summary>
    Task<SchoolSettings> GetAsync(CancellationToken cancellationToken);

    /// <summary>An active country with this id exists.</summary>
    Task<bool> CountryExistsAsync(int countryId, CancellationToken cancellationToken);

    Task SaveChangesAsync(CancellationToken cancellationToken);
}
