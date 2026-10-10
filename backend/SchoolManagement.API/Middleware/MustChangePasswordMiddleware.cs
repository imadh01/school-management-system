using System.IdentityModel.Tokens.Jwt;
using System.Security.Claims;
using System.Text.Json;
using SchoolManagement.API.Common;
using SchoolManagement.Application.Interfaces;

namespace SchoolManagement.API.Middleware;

/// <summary>
/// When a user's MustChangePassword flag is set, blocks ALL endpoints
/// except the ones they need to change their password or end the session:
///   - POST /api/auth/change-password
///   - GET  /api/auth/me
///   - POST /api/auth/refresh, /api/auth/logout, /api/auth/logout-all (D2)
///
/// Returns 403 with errorCode "PASSWORD_CHANGE_REQUIRED".
///
/// Must run AFTER authentication middleware so the user is already
/// identified by their JWT.
/// </summary>
public class MustChangePasswordMiddleware
{
    private readonly RequestDelegate _next;

    // Paths that remain accessible even when MustChangePassword is true.
    private static readonly HashSet<string> _allowedPaths = new(StringComparer.OrdinalIgnoreCase)
    {
        "/api/auth/change-password",
        "/api/auth/me",
        // D2: keeping the session alive (the new access token is still blocked
        // everywhere else) and signing out must work while a change is pending.
        "/api/auth/refresh",
        "/api/auth/logout",
        "/api/auth/logout-all",
    };

    public MustChangePasswordMiddleware(RequestDelegate next)
    {
        _next = next;
    }

    public async Task InvokeAsync(HttpContext context, IPermissionCacheService permissionCache)
    {
        // Skip for unauthenticated requests (login, etc.) — the auth
        // middleware or [AllowAnonymous] will handle those.
        if (context.User.Identity?.IsAuthenticated != true)
        {
            await _next(context);
            return;
        }

        // Skip allowed paths.
        var path = context.Request.Path.Value ?? string.Empty;
        if (_allowedPaths.Contains(path))
        {
            await _next(context);
            return;
        }

        // Parse user ID from the JWT.
        var userIdValue = context.User.FindFirst(JwtRegisteredClaimNames.Sub)?.Value
                          ?? context.User.FindFirst(ClaimTypes.NameIdentifier)?.Value;

        if (!int.TryParse(userIdValue, out var userId))
        {
            await _next(context);
            return;
        }

        var info = await permissionCache.GetUserInfoAsync(userId);
        if (info is not null && info.MustChangePassword)
        {
            context.Response.StatusCode = StatusCodes.Status403Forbidden;
            context.Response.ContentType = "application/json";

            var error = new ApiErrorResponse
            {
                Message = "You must change your password before continuing.",
                ErrorCode = "PASSWORD_CHANGE_REQUIRED",
                TraceId = context.TraceIdentifier
            };

            await context.Response.WriteAsync(
                JsonSerializer.Serialize(error, new JsonSerializerOptions
                {
                    PropertyNamingPolicy = JsonNamingPolicy.CamelCase
                }));
            return;
        }

        await _next(context);
    }
}
