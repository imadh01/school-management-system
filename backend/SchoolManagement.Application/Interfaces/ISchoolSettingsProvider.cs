namespace SchoolManagement.Application.Interfaces;

/// <summary>The school-wide values other code needs (name, country, time zone, currency, working days).</summary>
public sealed record SchoolSettingsSnapshot(
    string SchoolName,
    string? ShortName,
    int? CountryId,
    string TimeZoneId,
    string CurrencyCode,
    byte WorkingDays);

/// <summary>
/// E2: the school settings, read once and kept in memory (they change a few times a year, but are needed by
/// many requests: the top bar, address forms, and from F2 every "today"). The settings service calls
/// <see cref="Invalidate"/> after it saves. Single-instance, like the permission cache.
/// </summary>
public interface ISchoolSettingsProvider
{
    Task<SchoolSettingsSnapshot> GetAsync(CancellationToken cancellationToken);

    /// <summary>Call AFTER the save committed; the next read loads the new values.</summary>
    void Invalidate();
}
