using SchoolManagement.Application.Interfaces;

namespace SchoolManagement.Application.Services;

/// <summary>
/// F2: today's date in the school's time zone. The zone comes from the school settings (kept in memory by
/// <see cref="ISchoolSettingsProvider"/>, so no query per call) and the instant from <see cref="TimeProvider"/>
/// (so tests can set the time). An unknown or empty zone falls back to UTC, the behaviour before F2.
/// </summary>
public sealed class SchoolClock : ISchoolClock
{
    private readonly ISchoolSettingsProvider _settings;
    private readonly TimeProvider _time;

    public SchoolClock(ISchoolSettingsProvider settings, TimeProvider time)
    {
        _settings = settings;
        _time = time;
    }

    public async Task<DateOnly> TodayAsync(CancellationToken cancellationToken) =>
        TodayIn((await _settings.GetAsync(cancellationToken)).TimeZoneId, _time.GetUtcNow());

    /// <summary>The calendar date at <paramref name="utcNow"/> in the given IANA zone (UTC when unknown).</summary>
    public static DateOnly TodayIn(string? timeZoneId, DateTimeOffset utcNow)
    {
        var zone = !string.IsNullOrWhiteSpace(timeZoneId) && TimeZoneInfo.TryFindSystemTimeZoneById(timeZoneId.Trim(), out var found)
            ? found
            : TimeZoneInfo.Utc;
        return DateOnly.FromDateTime(TimeZoneInfo.ConvertTime(utcNow, zone).DateTime);
    }
}
