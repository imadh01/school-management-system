using SchoolManagement.Application.DTOs.Settings;

namespace SchoolManagement.Application.Interfaces;

public interface ISchoolSettingsService
{
    Task<SchoolSettingsResponse> GetAsync(CancellationToken cancellationToken);

    /// <summary>409 when stale; 400 for an unknown time zone or country.</summary>
    Task<SchoolSettingsResponse> UpdateAsync(UpdateSchoolSettingsRequest request, CancellationToken cancellationToken);

    /// <summary>The time zones the server knows, as IANA ids, sorted by offset then name.</summary>
    List<TimeZoneOption> GetTimeZones();

    /// <summary>Name and country for every signed-in user (top bar, address forms).</summary>
    Task<SchoolBasicsResponse> GetBasicsAsync(CancellationToken cancellationToken);
}
