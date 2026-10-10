using Microsoft.Extensions.Options;
using SchoolManagement.API.Configuration;
using SchoolManagement.Application.DTOs.Auth;

namespace SchoolManagement.API.Common;

/// <summary>
/// The only place that knows how the refresh token travels over HTTP. Services deal in plain
/// strings; the controller asks this class to read, write or clear the cookie, so the cookie's
/// name and flags are defined once.
/// </summary>
public sealed class RefreshTokenCookie
{
    private readonly RefreshTokenCookieOptions _options;

    public RefreshTokenCookie(IOptions<RefreshTokenCookieOptions> options)
    {
        _options = options.Value;
    }

    public string Name => _options.Name;

    public string? Read(HttpRequest request) =>
        request.Cookies.TryGetValue(_options.Name, out var value) && !string.IsNullOrWhiteSpace(value)
            ? value
            : null;

    /// <summary>
    /// Persistent cookie (survives closing the browser) that expires with the token's idle window.
    /// </summary>
    public void Write(HttpResponse response, IssuedRefreshToken token) =>
        response.Cookies.Append(_options.Name, token.Token, Build(
            new DateTimeOffset(DateTime.SpecifyKind(token.ExpiresAtUtc, DateTimeKind.Utc))));

    /// <summary>Deletion must repeat the same Path/Secure/SameSite, or the browser keeps the cookie.</summary>
    public void Clear(HttpResponse response) =>
        response.Cookies.Delete(_options.Name, Build(expires: null));

    private CookieOptions Build(DateTimeOffset? expires) => new()
    {
        HttpOnly = true,   // JavaScript (including injected XSS) cannot read it
        Secure = true,     // HTTPS only; browsers treat localhost as secure
        SameSite = _options.SameSite,
        Path = _options.Path,
        Expires = expires,
        IsEssential = true, // a login cookie, not tracking: never dropped by consent policies
    };
}
