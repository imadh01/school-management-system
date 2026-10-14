using SchoolManagement.Application.DTOs.Settings;
using SchoolManagement.Application.Interfaces;
using SchoolManagement.Domain.Entities;
using SchoolManagement.Domain.Exceptions;

namespace SchoolManagement.Application.Services;

/// <summary>E2: the Settings page (school profile, time zone, currency, working days).</summary>
public class SchoolSettingsService : ISchoolSettingsService
{
    private readonly ISchoolSettingsRepository _settings;
    private readonly ISchoolSettingsProvider _provider;
    private readonly IConcurrencyGuard _concurrency;
    private readonly TimeProvider _clock;

    public SchoolSettingsService(
        ISchoolSettingsRepository settings,
        ISchoolSettingsProvider provider,
        IConcurrencyGuard concurrency,
        TimeProvider clock)
    {
        _settings = settings;
        _provider = provider;
        _concurrency = concurrency;
        _clock = clock;
    }

    public async Task<SchoolSettingsResponse> GetAsync(CancellationToken cancellationToken) =>
        ToResponse(await _settings.GetAsync(cancellationToken));

    public async Task<SchoolSettingsResponse> UpdateAsync(UpdateSchoolSettingsRequest request, CancellationToken cancellationToken)
    {
        var settings = await _settings.GetAsync(cancellationToken);
        _concurrency.Expect(settings, request.RowVersion); // 409 if another admin saved in between

        if (request.CountryId is { } countryId && !await _settings.CountryExistsAsync(countryId, cancellationToken))
            throw new RequestValidationException(new Dictionary<string, string[]>
            {
                ["CountryId"] = new[] { $"Country {countryId} does not exist or is inactive." },
            });

        settings.SchoolName = request.SchoolName.Trim();
        settings.ShortName = Clean(request.ShortName);
        settings.Email = Clean(request.Email);
        settings.Phone = Clean(request.Phone);
        settings.Website = Clean(request.Website);
        settings.AddressText = Clean(request.AddressText);
        settings.CountryId = request.CountryId;
        settings.TimeZoneId = request.TimeZoneId.Trim();
        settings.CurrencyCode = request.CurrencyCode.Trim();
        // The validator has checked the names; Enum.Parse cannot fail here.
        settings.WorkingDays = WorkingDays.FromDays(request.WorkingDays.Select(d => Enum.Parse<DayOfWeek>(d)));
        settings.AboutTagline = Clean(request.AboutTagline);

        await _settings.SaveChangesAsync(cancellationToken);
        _provider.Invalidate(); // after the commit

        return ToResponse(await _settings.GetAsync(cancellationToken)); // reloads the country name
    }

    public List<TimeZoneOption> GetTimeZones() => SchoolTimeZones.All(_clock.GetUtcNow().UtcDateTime);

    public async Task<SchoolBasicsResponse> GetBasicsAsync(CancellationToken cancellationToken)
    {
        var s = await _provider.GetAsync(cancellationToken);
        return new SchoolBasicsResponse(s.SchoolName, s.ShortName, s.CountryId,
            SchoolClock.TodayIn(s.TimeZoneId, _clock.GetUtcNow()));
    }

    private static SchoolSettingsResponse ToResponse(SchoolSettings s) => new(
        s.SchoolName, s.ShortName, s.Email, s.Phone, s.Website, s.AddressText,
        s.CountryId, s.Country?.Name,
        s.TimeZoneId, s.CurrencyCode,
        WorkingDays.ToDays(s.WorkingDays).Select(d => d.ToString()).ToList(),
        s.AboutTagline,
        Convert.ToBase64String(s.RowVersion));

    private static string? Clean(string? value) =>
        string.IsNullOrWhiteSpace(value) ? null : value.Trim();
}
