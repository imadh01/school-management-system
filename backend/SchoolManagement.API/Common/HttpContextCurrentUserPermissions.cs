using System.IdentityModel.Tokens.Jwt;
using System.Security.Claims;
using SchoolManagement.Application.Interfaces;

namespace SchoolManagement.API.Common;

/// <summary>
/// Answers permission and role questions for the signed-in user from the permission cache,
/// never from JWT claims. The cache already expands Admin to every permission in the catalog,
/// so "Admin can do everything" is decided in exactly one place.
/// </summary>
public sealed class HttpContextCurrentUserPermissions : ICurrentUserPermissions
{
    private readonly IHttpContextAccessor _httpContextAccessor;
    private readonly IPermissionCacheService _permissionCache;

    public HttpContextCurrentUserPermissions(
        IHttpContextAccessor httpContextAccessor,
        IPermissionCacheService permissionCache)
    {
        _httpContextAccessor = httpContextAccessor;
        _permissionCache = permissionCache;
    }

    public async Task<bool> HasPermissionAsync(string permission, CancellationToken cancellationToken = default)
    {
        var info = await GetInfoAsync(cancellationToken);
        return info is not null && info.Permissions.Contains(permission, StringComparer.Ordinal);
    }

    public async Task<bool> IsInRoleAsync(string roleName, CancellationToken cancellationToken = default)
    {
        var info = await GetInfoAsync(cancellationToken);
        return info is not null && info.Roles.Contains(roleName, StringComparer.Ordinal);
    }

    private Task<CachedUserInfo?> GetInfoAsync(CancellationToken cancellationToken)
    {
        var user = _httpContextAccessor.HttpContext?.User;
        var value = user?.FindFirst(JwtRegisteredClaimNames.Sub)?.Value
                    ?? user?.FindFirst(ClaimTypes.NameIdentifier)?.Value;

        return int.TryParse(value, out var userId)
            ? _permissionCache.GetUserInfoAsync(userId, cancellationToken)
            : Task.FromResult<CachedUserInfo?>(null);
    }
}
