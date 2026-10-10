namespace SchoolManagement.API.Configuration;

/// <summary>
/// Bound from the "RefreshTokenCookie" section of appsettings. HttpOnly and Secure are not
/// configurable on purpose: there is no environment where the refresh token should be readable
/// by JavaScript or sent over plain HTTP.
/// </summary>
public class RefreshTokenCookieOptions
{
    public const string SectionName = "RefreshTokenCookie";

    /// <summary>
    /// The "__Secure-" prefix makes the browser reject the cookie unless it is Secure. Development
    /// uses a plain name, because the Vite dev server runs on http://localhost.
    /// </summary>
    public string Name { get; set; } = "__Secure-eschool_rt";

    /// <summary>Sent only to the auth endpoints, not with every API call.</summary>
    public string Path { get; set; } = "/api/auth";

    /// <summary>
    /// Strict while the UI and API share one site (decision: same-origin deployment). Only if
    /// they ever move to different sites does this need "None", and then the CSRF header check
    /// on /refresh and /logout becomes the main protection.
    /// </summary>
    public SameSiteMode SameSite { get; set; } = SameSiteMode.Strict;
}
