namespace SchoolManagement.Application.Settings;

/// <summary>
/// Bound from the "RefreshToken" section of appsettings.json. The cookie itself (name, path,
/// SameSite) is an HTTP concern and is configured separately in the API layer
/// (RefreshTokenCookieOptions).
/// </summary>
public class RefreshTokenSettings
{
    public const string SectionName = "RefreshToken";

    /// <summary>A session that is not refreshed for this long dies (sliding window).</summary>
    public int IdleLifetimeDays { get; set; } = 7;

    /// <summary>Hard ceiling from login: even an active user must sign in again after this.</summary>
    public int AbsoluteLifetimeDays { get; set; } = 30;

    /// <summary>
    /// How long a dead family's rows are kept after it expires, so a reuse alert can still be
    /// investigated. The daily cleanup job deletes anything older.
    /// </summary>
    public int RetentionDays { get; set; } = 7;

    /// <summary>Turns the daily cleanup job off (the integration tests do).</summary>
    public bool CleanupEnabled { get; set; } = true;
}
