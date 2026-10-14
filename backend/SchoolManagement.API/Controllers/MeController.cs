using FluentValidation;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using SchoolManagement.API.Common;
using SchoolManagement.API.Extensions;
using SchoolManagement.Application.DTOs.Users;
using SchoolManagement.Application.Interfaces;

namespace SchoolManagement.API.Controllers;

/// <summary>
/// E2: My Account. Every signed-in user may read and edit their OWN profile, so there is no permission:
/// the user is always the caller (the id comes from the token, never from the URL).
/// Password change stays at POST /api/auth/change-password.
/// </summary>
[ApiController]
[Route("api/me")]
[Authorize]
public class MeController : ControllerBase
{
    private readonly IUserService _userService;
    private readonly IValidator<UpdateMyProfileRequest> _validator;

    public MeController(IUserService userService, IValidator<UpdateMyProfileRequest> validator)
    {
        _userService = userService;
        _validator = validator;
    }

    [HttpGet("profile")]
    [ProducesResponseType(typeof(UserResponse), StatusCodes.Status200OK)]
    [ProducesResponseType(typeof(ApiErrorResponse), StatusCodes.Status404NotFound)]
    public async Task<IActionResult> GetProfile(CancellationToken cancellationToken) =>
        Ok(await _userService.GetMyProfileAsync(cancellationToken));

    [HttpPut("profile")]
    [ProducesResponseType(typeof(UserResponse), StatusCodes.Status200OK)]
    [ProducesResponseType(typeof(ApiErrorResponse), StatusCodes.Status400BadRequest)]
    [ProducesResponseType(typeof(ApiErrorResponse), StatusCodes.Status404NotFound)]
    [ProducesResponseType(typeof(ApiErrorResponse), StatusCodes.Status409Conflict)]
    public async Task<IActionResult> UpdateProfile(UpdateMyProfileRequest request, CancellationToken cancellationToken)
    {
        var result = await _validator.ValidateAsync(request, cancellationToken);
        if (!result.IsValid)
        {
            return BadRequest(new ApiErrorResponse
            {
                Message = "Validation failed.",
                ErrorCode = "VALIDATION_ERROR",
                Errors = result.ToErrorDictionary(),
                TraceId = HttpContext.GetCorrelationId(),
            });
        }

        return Ok(await _userService.UpdateMyProfileAsync(request, cancellationToken));
    }
}
