using SchoolManagement.Application.DTOs.Settings;

namespace SchoolManagement.Application.Services;

/// <summary>
/// E2: the school's time zone is stored as an IANA id ("Asia/Dubai"): the same text works on Windows and
/// Linux servers (.NET converts it), and browsers use the same ids. A Windows id ("Arabian Standard Time")
/// is refused so the stored value never depends on the server's operating system.
/// </summary>
public static class SchoolTimeZones
{
    public const string Message =
        "Time zone must be an IANA time-zone id the server knows, for example \"Asia/Dubai\" or \"Asia/Kolkata\".";

    public static bool IsValidIanaId(string? id) =>
        !string.IsNullOrWhiteSpace(id)
        && TimeZoneInfo.TryFindSystemTimeZoneById(id, out var zone)
        && zone.HasIanaId;

    /// <summary>Every zone the server knows, by IANA id, sorted by today's offset then id.</summary>
    public static List<TimeZoneOption> All(DateTime utcNow)
    {
        var options = new Dictionary<string, TimeZoneOption>(StringComparer.Ordinal);
        foreach (var zone in TimeZoneInfo.GetSystemTimeZones())
        {
            // Linux lists IANA ids already; Windows lists Windows ids, converted to their main IANA id.
            var id = zone.HasIanaId
                ? zone.Id
                : TimeZoneInfo.TryConvertWindowsIdToIanaId(zone.Id, out var iana) ? iana : null;
            if (id is null || options.ContainsKey(id)) continue;

            var offset = (int)zone.GetUtcOffset(utcNow).TotalMinutes;
            options[id] = new TimeZoneOption(id, $"(UTC{FormatOffset(offset)}) {id.Replace('_', ' ')}", offset);
        }

        return options.Values
            .OrderBy(o => o.UtcOffsetMinutes)
            .ThenBy(o => o.Id, StringComparer.Ordinal)
            .ToList();
    }

    private static string FormatOffset(int minutes)
    {
        var sign = minutes < 0 ? "-" : "+";
        var abs = Math.Abs(minutes);
        return $"{sign}{abs / 60:00}:{abs % 60:00}";
    }
}
