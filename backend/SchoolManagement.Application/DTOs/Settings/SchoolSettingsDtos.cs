namespace SchoolManagement.Application.DTOs.Settings;

/// <summary>E2: the school's own record (Settings page).</summary>
public record SchoolSettingsResponse(
    string SchoolName,
    string? ShortName,
    string? Email,
    string? Phone,
    string? Website,
    string? AddressText,
    int? CountryId,
    string? CountryName,
    string TimeZoneId,
    string CurrencyCode,
    // Monday first, English day names: "Monday", "Tuesday", ...
    List<string> WorkingDays,
    string? AboutTagline,
    string RowVersion);

public record UpdateSchoolSettingsRequest(
    string SchoolName,
    string? ShortName,
    string? Email,
    string? Phone,
    string? Website,
    string? AddressText,
    int? CountryId,
    string TimeZoneId,
    string CurrencyCode,
    // English day names, any order: "Monday", ..., "Sunday". At least one.
    List<string> WorkingDays,
    string? AboutTagline,
    string RowVersion);

/// <summary>One entry of the time-zone picker: IANA id, a readable name and today's offset from UTC.</summary>
public record TimeZoneOption(string Id, string DisplayName, int UtcOffsetMinutes);

/// <summary>What every page may know about the school (top bar, address forms): no permission needed.</summary>
public record SchoolBasicsResponse(string SchoolName, string? ShortName, int? CountryId);
