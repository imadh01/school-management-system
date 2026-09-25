using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using FluentValidation;
using SchoolManagement.API.Common;
using SchoolManagement.API.Extensions;
using SchoolManagement.Application.DTOs.Users;
using SchoolManagement.Application.Interfaces;

namespace SchoolManagement.API.Controllers;

[ApiController]
[Route("api/users")]
public class UsersController : ControllerBase
{
    private readonly IUserService _userService;
    private readonly IValidator<CreateUserRequest> _validator;

    public UsersController(IUserService userService, IValidator<CreateUserRequest> validator)
    {
        _userService = userService;
        _validator = validator;
    }

    [HttpPost]
    [Authorize(Policy = "Users.Create")]
    [ProducesResponseType(typeof(UserResponse), StatusCodes.Status201Created)]
    [ProducesResponseType(typeof(ApiErrorResponse), StatusCodes.Status400BadRequest)]
    [ProducesResponseType(typeof(ApiErrorResponse), StatusCodes.Status403Forbidden)]
    [ProducesResponseType(typeof(ApiErrorResponse), StatusCodes.Status409Conflict)]
    public async Task<IActionResult> Create(CreateUserRequest request, CancellationToken cancellationToken)
    {
        var validationResult = await _validator.ValidateAsync(request, cancellationToken);
        if (!validationResult.IsValid)
        {
            return BadRequest(new ApiErrorResponse
            {
                Message = "Validation failed.",
                ErrorCode = "VALIDATION_ERROR",
                Errors = validationResult.ToErrorDictionary(),
                TraceId = HttpContext.GetCorrelationId(),
            });
        }

        var user = await _userService.CreateUserAsync(request, cancellationToken);
        return Created($"api/users/{user.Id}", user);
    }
}