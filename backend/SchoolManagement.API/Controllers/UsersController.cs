using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using FluentValidation;
using SchoolManagement.API.Common;
using SchoolManagement.API.Extensions;
using SchoolManagement.Application.DTOs.Users;
using SchoolManagement.Application.Interfaces;
using SchoolManagement.Domain.Constants;

namespace SchoolManagement.API.Controllers;

/// <summary>
/// E2: staff accounts and their profiles. The guards (not your own account, never the last active Admin,
/// no Student/Parent role yet, only roles whose permissions you hold, teacher with assignments) are in
/// UserService / UserAccountService. Status, password, role and delete end the user's sessions at once.
/// </summary>
[ApiController]
[Route("api/users")]
[Authorize]
public class UsersController : ControllerBase
{
    private readonly IUserService _userService;
    private readonly IAuthService _authService;
    private readonly IValidator<CreateUserRequest> _createValidator;
    private readonly IValidator<UpdateUserRequest> _updateValidator;
    private readonly IValidator<ChangeUserStatusRequest> _statusValidator;
    private readonly IValidator<ResetUserPasswordRequest> _passwordValidator;

    public UsersController(
        IUserService userService,
        IAuthService authService,
        IValidator<CreateUserRequest> createValidator,
        IValidator<UpdateUserRequest> updateValidator,
        IValidator<ChangeUserStatusRequest> statusValidator,
        IValidator<ResetUserPasswordRequest> passwordValidator)
    {
        _userService = userService;
        _authService = authService;
        _createValidator = createValidator;
        _updateValidator = updateValidator;
        _statusValidator = statusValidator;
        _passwordValidator = passwordValidator;
    }

    [HttpGet]
    [Authorize(Policy = Permissions.UsersView)]
    [ProducesResponseType(typeof(List<UserListItemResponse>), StatusCodes.Status200OK)]
    public async Task<IActionResult> GetAll(CancellationToken cancellationToken) =>
        Ok(await _userService.GetAllAsync(cancellationToken));

    [HttpGet("{id:int}")]
    [Authorize(Policy = Permissions.UsersView)]
    [ProducesResponseType(typeof(UserResponse), StatusCodes.Status200OK)]
    [ProducesResponseType(typeof(ApiErrorResponse), StatusCodes.Status404NotFound)]
    public async Task<IActionResult> GetById(int id, CancellationToken cancellationToken) =>
        Ok(await _userService.GetByIdAsync(id, cancellationToken));

    [HttpPost]
    [Authorize(Policy = Permissions.UsersCreate)]
    [ProducesResponseType(typeof(UserResponse), StatusCodes.Status201Created)]
    [ProducesResponseType(typeof(ApiErrorResponse), StatusCodes.Status400BadRequest)]
    [ProducesResponseType(typeof(ApiErrorResponse), StatusCodes.Status403Forbidden)]
    [ProducesResponseType(typeof(ApiErrorResponse), StatusCodes.Status409Conflict)]
    [ProducesResponseType(typeof(ApiErrorResponse), StatusCodes.Status422UnprocessableEntity)]
    public async Task<IActionResult> Create(CreateUserRequest request, CancellationToken cancellationToken)
    {
        if (await ValidateAsync(_createValidator, request, cancellationToken) is { } invalid) return invalid;

        var user = await _userService.CreateUserAsync(request, cancellationToken);
        return Created($"api/users/{user.Id}", user);
    }

    /// <summary>Account, profile, addresses and role in one save. A different role ends the user's sessions.</summary>
    [HttpPut("{id:int}")]
    [Authorize(Policy = Permissions.UsersEdit)]
    [ProducesResponseType(typeof(UserResponse), StatusCodes.Status200OK)]
    [ProducesResponseType(typeof(ApiErrorResponse), StatusCodes.Status400BadRequest)]
    [ProducesResponseType(typeof(ApiErrorResponse), StatusCodes.Status403Forbidden)]
    [ProducesResponseType(typeof(ApiErrorResponse), StatusCodes.Status404NotFound)]
    [ProducesResponseType(typeof(ApiErrorResponse), StatusCodes.Status409Conflict)]
    [ProducesResponseType(typeof(ApiErrorResponse), StatusCodes.Status422UnprocessableEntity)]
    public async Task<IActionResult> Update(int id, UpdateUserRequest request, CancellationToken cancellationToken)
    {
        if (await ValidateAsync(_updateValidator, request, cancellationToken) is { } invalid) return invalid;

        return Ok(await _userService.UpdateAsync(id, request, cancellationToken));
    }

    /// <summary>Active / Inactive / Suspended. Anything but Active ends the user's sessions.</summary>
    [HttpPatch("{id:int}/status")]
    [Authorize(Policy = Permissions.UsersChangeStatus)]
    [ProducesResponseType(typeof(UserResponse), StatusCodes.Status200OK)]
    [ProducesResponseType(typeof(ApiErrorResponse), StatusCodes.Status400BadRequest)]
    [ProducesResponseType(typeof(ApiErrorResponse), StatusCodes.Status404NotFound)]
    [ProducesResponseType(typeof(ApiErrorResponse), StatusCodes.Status409Conflict)]
    [ProducesResponseType(typeof(ApiErrorResponse), StatusCodes.Status422UnprocessableEntity)]
    public async Task<IActionResult> ChangeStatus(int id, ChangeUserStatusRequest request, CancellationToken cancellationToken)
    {
        if (await ValidateAsync(_statusValidator, request, cancellationToken) is { } invalid) return invalid;

        return Ok(await _userService.ChangeStatusAsync(id, request, cancellationToken));
    }

    /// <summary>A temporary password: the user must change it at the next sign-in. Ends the user's sessions.</summary>
    [HttpPost("{id:int}/reset-password")]
    [Authorize(Policy = Permissions.UsersResetPassword)]
    [ProducesResponseType(StatusCodes.Status204NoContent)]
    [ProducesResponseType(typeof(ApiErrorResponse), StatusCodes.Status400BadRequest)]
    [ProducesResponseType(typeof(ApiErrorResponse), StatusCodes.Status404NotFound)]
    [ProducesResponseType(typeof(ApiErrorResponse), StatusCodes.Status422UnprocessableEntity)]
    public async Task<IActionResult> ResetPassword(int id, ResetUserPasswordRequest request, CancellationToken cancellationToken)
    {
        if (await ValidateAsync(_passwordValidator, request, cancellationToken) is { } invalid) return invalid;

        await _userService.ResetPasswordAsync(id, request, cancellationToken);
        return NoContent();
    }

    /// <summary>Ends a lockout after too many failed sign-ins (replaces the old POST /api/auth/unlock/{id}).</summary>
    [HttpPost("{id:int}/unlock")]
    [Authorize(Policy = Permissions.UsersChangeStatus)]
    [ProducesResponseType(StatusCodes.Status204NoContent)]
    [ProducesResponseType(typeof(ApiErrorResponse), StatusCodes.Status404NotFound)]
    public async Task<IActionResult> Unlock(int id, CancellationToken cancellationToken)
    {
        await _authService.UnlockUserAsync(id, cancellationToken);
        return NoContent();
    }

    /// <summary>Soft-deletes the login, its profile and its teacher record. Ends the user's sessions.</summary>
    [HttpDelete("{id:int}")]
    [Authorize(Policy = Permissions.UsersDelete)]
    [ProducesResponseType(StatusCodes.Status204NoContent)]
    [ProducesResponseType(typeof(ApiErrorResponse), StatusCodes.Status404NotFound)]
    [ProducesResponseType(typeof(ApiErrorResponse), StatusCodes.Status422UnprocessableEntity)]
    public async Task<IActionResult> Delete(int id, CancellationToken cancellationToken)
    {
        await _userService.DeleteAsync(id, cancellationToken);
        return NoContent();
    }

    private async Task<IActionResult?> ValidateAsync<T>(IValidator<T> validator, T request, CancellationToken cancellationToken)
    {
        var result = await validator.ValidateAsync(request, cancellationToken);
        if (result.IsValid) return null;

        return BadRequest(new ApiErrorResponse
        {
            Message = "Validation failed.",
            ErrorCode = "VALIDATION_ERROR",
            Errors = result.ToErrorDictionary(),
            TraceId = HttpContext.GetCorrelationId(),
        });
    }
}