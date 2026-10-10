
using System.IdentityModel.Tokens.Jwt;
using System.Security.Claims;
using Microsoft.AspNetCore.Authorization;
using SchoolManagement.Application.Interfaces;

namespace SchoolManagement.Infrastructure.Security;

/// <summary>
/// Handles both <see cref="PermissionRequirement"/> (single permission)
/// and <see cref="AnyPermissionRequirement"/> (any-of-N permissions).
///
/// Permissions are resolved from the in-memory permission cache, NOT from
/// JWT claims. The JWT carries only sub + security_stamp + jti.
/// </summary>
public class PermissionAuthorizationHandler : IAuthorizationHandler
{
    private readonly IPermissionCacheService _cache;

    public PermissionAuthorizationHandler(IPermissionCacheService cache)
    {
        _cache = cache;
    }

    public async Task HandleAsync(AuthorizationHandlerContext context)
    {
        // Parse user ID from the JWT's "sub" claim.
        var userIdValue = context.User.FindFirst(JwtRegisteredClaimNames.Sub)?.Value
                          ?? context.User.FindFirst(ClaimTypes.NameIdentifier)?.Value;

        if (!int.TryParse(userIdValue, out var userId))
            return; // unauthenticated — don't succeed any requirements

        var info = await _cache.GetUserInfoAsync(userId);
        if (info is null)
            return;

        foreach (var requirement in context.PendingRequirements.ToList())
        {
            switch (requirement)
            {
                case PermissionRequirement pr
                    when info.Permissions.Contains(pr.PermissionName):
                    context.Succeed(requirement);
                    break;

                case AnyPermissionRequirement apr
                    when apr.PermissionNames.Any(p => info.Permissions.Contains(p)):
                    context.Succeed(requirement);
                    break;
            }
        }
    }
}
