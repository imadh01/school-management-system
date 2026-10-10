using FluentValidation;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using SchoolManagement.API.Common;
using SchoolManagement.API.Extensions;
using SchoolManagement.Application.DTOs.Roles;
using SchoolManagement.Application.Interfaces;
using SchoolManagement.Domain.Constants;

namespace SchoolManagement.API.Controllers;

/// <summary>
/// Roles and their permissions (Batch E1). Roles are never deleted, only deactivated.
/// All guards (own role, escalation, Admin/system roles, dependencies, concurrency) are in RoleService.
/// </summary>
[ApiController]
[Route("api/roles")]
[Authorize]
public class RolesController : ControllerBase
{
    private readonly IRoleService _roleService;
    private readonly IValidator<CreateRoleRequest> _createValidator;
    private readonly IValidator<UpdateRoleRequest> _updateValidator;
    private readonly IValidator<SetRolePermissionsRequest> _permissionsValidator;
    private readonly IValidator<ChangeRoleStatusRequest> _statusValidator;

    public RolesController(
        IRoleService roleService,
        IValidator<CreateRoleRequest> createValidator,
        IValidator<UpdateRoleRequest> updateValidator,
        IValidator<SetRolePermissionsRequest> permissionsValidator,
        IValidator<ChangeRoleStatusRequest> statusValidator)
    {
        _roleService = roleService;
        _createValidator = createValidator;
        _updateValidator = updateValidator;
        _permissionsValidator = permissionsValidator;
        _statusValidator = statusValidator;
    }

    [HttpGet]
    [Authorize(Policy = Permissions.RolesView)]
    [ProducesResponseType(typeof(IReadOnlyList<RoleSummaryResponse>), StatusCodes.Status200OK)]
    public async Task<IActionResult> GetAll(CancellationToken cancellationToken) =>
        Ok(await _roleService.GetAllAsync(cancellationToken));

    [HttpGet("{id:int}")]
    [Authorize(Policy = Permissions.RolesView)]
    [ProducesResponseType(typeof(RoleResponse), StatusCodes.Status200OK)]
    [ProducesResponseType(typeof(ApiErrorResponse), StatusCodes.Status404NotFound)]
    public async Task<IActionResult> GetById(int id, CancellationToken cancellationToken) =>
        Ok(await _roleService.GetByIdAsync(id, cancellationToken));

    [HttpPost]
    [Authorize(Policy = Permissions.RolesCreate)]
    [ProducesResponseType(typeof(RoleResponse), StatusCodes.Status201Created)]
    [ProducesResponseType(typeof(ApiErrorResponse), StatusCodes.Status400BadRequest)]
    [ProducesResponseType(typeof(ApiErrorResponse), StatusCodes.Status403Forbidden)]
    [ProducesResponseType(typeof(ApiErrorResponse), StatusCodes.Status409Conflict)]
    [ProducesResponseType(typeof(ApiErrorResponse), StatusCodes.Status422UnprocessableEntity)]
    public async Task<IActionResult> Create(CreateRoleRequest request, CancellationToken cancellationToken)
    {
        if (await ValidateAsync(_createValidator, request, cancellationToken) is { } invalid) return invalid;

        var result = await _roleService.CreateAsync(request, cancellationToken);
        return Created($"api/roles/{result.Id}", result);
    }

    [HttpPut("{id:int}")]
    [Authorize(Policy = Permissions.RolesEdit)]
    [ProducesResponseType(typeof(RoleResponse), StatusCodes.Status200OK)]
    [ProducesResponseType(typeof(ApiErrorResponse), StatusCodes.Status400BadRequest)]
    [ProducesResponseType(typeof(ApiErrorResponse), StatusCodes.Status403Forbidden)]
    [ProducesResponseType(typeof(ApiErrorResponse), StatusCodes.Status404NotFound)]
    [ProducesResponseType(typeof(ApiErrorResponse), StatusCodes.Status409Conflict)]
    [ProducesResponseType(typeof(ApiErrorResponse), StatusCodes.Status422UnprocessableEntity)]
    public async Task<IActionResult> Update(int id, UpdateRoleRequest request, CancellationToken cancellationToken)
    {
        if (await ValidateAsync(_updateValidator, request, cancellationToken) is { } invalid) return invalid;

        return Ok(await _roleService.UpdateAsync(id, request, cancellationToken));
    }

    /// <summary>Replaces the role's whole permission set.</summary>
    [HttpPut("{id:int}/permissions")]
    [Authorize(Policy = Permissions.RolesEdit)]
    [ProducesResponseType(typeof(RoleResponse), StatusCodes.Status200OK)]
    [ProducesResponseType(typeof(ApiErrorResponse), StatusCodes.Status400BadRequest)]
    [ProducesResponseType(typeof(ApiErrorResponse), StatusCodes.Status403Forbidden)]
    [ProducesResponseType(typeof(ApiErrorResponse), StatusCodes.Status404NotFound)]
    [ProducesResponseType(typeof(ApiErrorResponse), StatusCodes.Status409Conflict)]
    [ProducesResponseType(typeof(ApiErrorResponse), StatusCodes.Status422UnprocessableEntity)]
    public async Task<IActionResult> SetPermissions(
        int id, SetRolePermissionsRequest request, CancellationToken cancellationToken)
    {
        if (await ValidateAsync(_permissionsValidator, request, cancellationToken) is { } invalid) return invalid;

        return Ok(await _roleService.SetPermissionsAsync(id, request, cancellationToken));
    }

    [HttpPatch("{id:int}/status")]
    [Authorize(Policy = Permissions.RolesEdit)]
    [ProducesResponseType(typeof(RoleResponse), StatusCodes.Status200OK)]
    [ProducesResponseType(typeof(ApiErrorResponse), StatusCodes.Status400BadRequest)]
    [ProducesResponseType(typeof(ApiErrorResponse), StatusCodes.Status403Forbidden)]
    [ProducesResponseType(typeof(ApiErrorResponse), StatusCodes.Status404NotFound)]
    [ProducesResponseType(typeof(ApiErrorResponse), StatusCodes.Status409Conflict)]
    [ProducesResponseType(typeof(ApiErrorResponse), StatusCodes.Status422UnprocessableEntity)]
    public async Task<IActionResult> ChangeStatus(
        int id, ChangeRoleStatusRequest request, CancellationToken cancellationToken)
    {
        if (await ValidateAsync(_statusValidator, request, cancellationToken) is { } invalid) return invalid;

        return Ok(await _roleService.ChangeStatusAsync(id, request, cancellationToken));
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
