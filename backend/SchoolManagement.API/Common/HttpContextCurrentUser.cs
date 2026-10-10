// File: backend/SchoolManagement.API/Common/HttpContextCurrentUser.cs
using System.IdentityModel.Tokens.Jwt;
using System.Security.Claims;
using SchoolManagement.Application.Interfaces;

namespace SchoolManagement.API.Common;

/// <summary>
/// Extracts the current user's identity from JWT claims.
///
/// D1 change: the JWT no longer carries unique_name or email.
/// UserName is resolved from the permission cache instead.
/// UserId still comes from the "sub" claim.
/// </summary>
public class HttpContextCurrentUser : ICurrentUser
{
    private readonly IHttpContextAccessor _httpContextAccessor;
    private readonly IPermissionCacheService _permissionCache;

    public HttpContextCurrentUser(
        IHttpContextAccessor httpContextAccessor,
        IPermissionCacheService permissionCache)
    {
        _httpContextAccessor = httpContextAccessor;
        _permissionCache = permissionCache;
    }

    public int? UserId
    {
        get
        {
            var value = _httpContextAccessor.HttpContext?.User
                .FindFirst(JwtRegisteredClaimNames.Sub)?.Value
                ?? _httpContextAccessor.HttpContext?.User
                .FindFirst(ClaimTypes.NameIdentifier)?.Value;

            return int.TryParse(value, out var id) ? id : null;
        }
    }

    public string? UserName
    {
        get
        {
            // Since JWT no longer carries the username, resolve it
            // from the permission cache.  The cache is almost certainly
            // warm by now (the authorization handler hit it first).
            if (UserId is null) return null;

            var info = _permissionCache
                .GetUserInfoAsync(UserId.Value)
                .ConfigureAwait(false)
                .GetAwaiter()
                .GetResult();

            return info?.Username;
        }
    }

    public string? TraceId =>
        _httpContextAccessor.HttpContext?.TraceIdentifier;
}
