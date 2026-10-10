namespace SchoolManagement.Application.DTOs.Auth;

/// <summary>
/// What a successful login, refresh or password change produces.
/// <see cref="Result"/> is returned as the JSON body; <see cref="RefreshToken"/> goes ONLY into
/// the httpOnly cookie. Never return this type itself from a controller: that would put the
/// refresh token where JavaScript can read it.
/// </summary>
public sealed record AuthSession(AuthResult Result, IssuedRefreshToken RefreshToken);

/// <summary>A freshly issued raw refresh token and when it expires (UTC). Exists only in memory and in the cookie.</summary>
public sealed record IssuedRefreshToken(string Token, DateTime ExpiresAtUtc);

/// <summary>Who is asking, as far as HTTP can tell. Stored on the token row for investigating reuse alerts.</summary>
public sealed record ClientInfo(string? IpAddress, string? UserAgent)
{
    public static readonly ClientInfo Unknown = new(null, null);
}
