using System.IdentityModel.Tokens.Jwt;
using System.Security.Claims;
using FluentValidation;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using SchoolManagement.API.Common;
using SchoolManagement.API.Extensions;
using SchoolManagement.API.Filters;
using SchoolManagement.Application.DTOs.Auth;
using SchoolManagement.Application.Interfaces;

namespace SchoolManagement.API.Controllers;

/// <summary>
/// Two tokens (D2):
///   - ACCESS token (JWT, 15 min) in the JSON body. The frontend keeps it in memory only
///     and sends it as "Authorization: Bearer ...".
///   - REFRESH token in an httpOnly cookie, scoped to /api/auth. Never in a response body.
/// </summary>
[ApiController]
[Route("api/auth")]
[Authorize]
public class AuthController : ControllerBase
{
    private readonly IAuthService _authService;
    private readonly IValidator<LoginRequest> _loginValidator;
    private readonly IValidator<ChangePasswordRequest> _changePasswordValidator;
    private readonly RefreshTokenCookie _refreshCookie;

    public AuthController(
        IAuthService authService,
        IValidator<LoginRequest> loginValidator,
        IValidator<ChangePasswordRequest> changePasswordValidator,
        RefreshTokenCookie refreshCookie)
    {
        _authService = authService;
        _loginValidator = loginValidator;
        _changePasswordValidator = changePasswordValidator;
        _refreshCookie = refreshCookie;
    }

    /// <summary>
    /// Authenticate with username/email and password.
    /// Returns a JWT (sub + security_stamp + jti only) and user metadata,
    /// and sets the refresh-token cookie (a new session for this device).
    /// </summary>
    [HttpPost("login")]
    [AllowAnonymous]
    public async Task<IActionResult> Login(LoginRequest request, CancellationToken cancellationToken)
    {
        var validationResult = await _loginValidator.ValidateAsync(request, cancellationToken);
        if (!validationResult.IsValid)
        {
            return BadRequest(new ApiErrorResponse
            {
                Message = "Validation failed.",
                ErrorCode = "VALIDATION_ERROR",
                Errors = validationResult.ToErrorDictionary(),
                TraceId = HttpContext.GetCorrelationId()
            });
        }

        var session = await _authService.LoginAsync(request, GetClientInfo(), cancellationToken);

        if (session is null)
        {
            return Unauthorized(new ApiErrorResponse
            {
                Message = "Invalid username/email or password.",
                ErrorCode = "INVALID_CREDENTIALS",
                TraceId = HttpContext.GetCorrelationId()
            });
        }

        _refreshCookie.Write(Response, session.RefreshToken);
        return Ok(session.Result);
    }

    /// <summary>
    /// Exchanges the refresh-token cookie for a new access token and a rotated cookie.
    /// Anonymous because the access token has, by definition, expired: the cookie IS the credential.
    /// Every failure is the same 401 and clears the cookie, so nothing reveals which check failed.
    /// </summary>
    [HttpPost("refresh")]
    [AllowAnonymous]
    [RequireCsrfHeader]
    public async Task<IActionResult> Refresh(CancellationToken cancellationToken)
    {
        var session = await _authService.RefreshAsync(
            _refreshCookie.Read(Request), GetClientInfo(), cancellationToken);

        if (session is null)
        {
            _refreshCookie.Clear(Response);
            return Unauthorized(new ApiErrorResponse
            {
                Message = "Your session has ended. Please sign in again.",
                ErrorCode = "REFRESH_TOKEN_INVALID",
                TraceId = HttpContext.GetCorrelationId()
            });
        }

        _refreshCookie.Write(Response, session.RefreshToken);
        return Ok(session.Result);
    }

    /// <summary>
    /// Ends this device's session and clears the cookie. Idempotent: always 204, even with no
    /// cookie or an already-ended session. Anonymous so logout still works after the access token expired.
    /// </summary>
    [HttpPost("logout")]
    [AllowAnonymous]
    [RequireCsrfHeader]
    public async Task<IActionResult> Logout(CancellationToken cancellationToken)
    {
        await _authService.LogoutAsync(_refreshCookie.Read(Request), cancellationToken);
        _refreshCookie.Clear(Response);
        return NoContent();
    }

    /// <summary>
    /// "Sign out everywhere": ends every session of the current user on every device, including
    /// this one. Access tokens stop working immediately (the security stamp is rotated).
    /// </summary>
    [HttpPost("logout-all")]
    public async Task<IActionResult> LogoutAll(CancellationToken cancellationToken)
    {
        await _authService.LogoutAllAsync(GetCurrentUserId(), cancellationToken);
        _refreshCookie.Clear(Response);
        return NoContent();
    }

    /// <summary>
    /// Returns the current user's profile, roles, and permissions.
    /// This is one of only two endpoints accessible when MustChangePassword is true.
    /// </summary>
    [HttpGet("me")]
    public async Task<IActionResult> GetCurrentUser(CancellationToken cancellationToken)
    {
        var userId = GetCurrentUserId();
        var response = await _authService.GetCurrentUserAsync(userId, cancellationToken);
        return Ok(response);
    }

    /// <summary>
    /// Change the current user's password.  Also clears MustChangePassword,
    /// rotates the security stamp, signs out every other device, and returns
    /// a new JWT plus a new refresh-token cookie for this device.
    /// This is one of only two endpoints accessible when MustChangePassword is true.
    /// </summary>
    [HttpPost("change-password")]
    public async Task<IActionResult> ChangePassword(
        ChangePasswordRequest request, CancellationToken cancellationToken)
    {
        var validationResult = await _changePasswordValidator.ValidateAsync(request, cancellationToken);
        if (!validationResult.IsValid)
        {
            return BadRequest(new ApiErrorResponse
            {
                Message = "Validation failed.",
                ErrorCode = "VALIDATION_ERROR",
                Errors = validationResult.ToErrorDictionary(),
                TraceId = HttpContext.GetCorrelationId()
            });
        }

        var userId = GetCurrentUserId();
        var session = await _authService.ChangePasswordAsync(userId, request, GetClientInfo(), cancellationToken);

        _refreshCookie.Write(Response, session.RefreshToken);
        return Ok(session.Result);
    }

    /// <summary>
    /// Admin-only: unlock a user whose account is locked from failed login attempts.
    /// Clears LockoutEnd and AccessFailedCount.
    /// </summary>
    [HttpPost("unlock/{userId:int}")]
    [Authorize(Policy = "Users.Create")] // Re-uses existing user-management permission
    public async Task<IActionResult> UnlockUser(int userId, CancellationToken cancellationToken)
    {
        await _authService.UnlockUserAsync(userId, cancellationToken);
        return NoContent();
    }

    // ─── Private helpers ─────────────────────────────────────────────

    /// <summary>
    /// Extracts the current user's ID from the JWT "sub" claim.
    /// Falls back to ClaimTypes.NameIdentifier for compatibility.
    /// </summary>
    private int GetCurrentUserId()
    {
        var value = User.FindFirst(JwtRegisteredClaimNames.Sub)?.Value
                    ?? User.FindFirst(ClaimTypes.NameIdentifier)?.Value;

        // If we reach an [Authorize]-protected endpoint the JWT middleware
        // has already validated the token, so "sub" must be present.
        return int.Parse(value!);
    }

    /// <summary>
    /// IP and browser of the caller, stored on the refresh-token row for investigating reuse.
    /// Behind a reverse proxy this IP is the proxy's until UseForwardedHeaders is configured
    /// (a deployment step).
    /// </summary>
    private ClientInfo GetClientInfo() => new(
        HttpContext.Connection.RemoteIpAddress?.ToString(),
        Request.Headers.UserAgent.ToString());
}
