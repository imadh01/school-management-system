using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using FluentValidation;
using SchoolManagement.API.Common;
using SchoolManagement.API.Extensions;
using SchoolManagement.Application.DTOs.Students;
using SchoolManagement.Application.DTOs.Parents;
using SchoolManagement.Application.Interfaces;
using SchoolManagement.Domain.Constants;

namespace SchoolManagement.API.Controllers;

[ApiController]
[Route("api/students")]
[Authorize]
public class StudentsController : ControllerBase
{
    private readonly IStudentService _studentService;
    private readonly IStudentGuardianService _guardianService;
    private readonly IValidator<CreateStudentRequest> _createValidator;
    private readonly IValidator<UpdateStudentRequest> _updateValidator;
    private readonly IValidator<UpdateStudentIdentityRequest> _identityValidator;
    private readonly IValidator<LinkGuardianRequest> _linkValidator;
    private readonly ICurrentUserPermissions _permissions;

    public StudentsController(
        IStudentService studentService,
        IStudentGuardianService guardianService,
        IValidator<CreateStudentRequest> createValidator,
        IValidator<UpdateStudentRequest> updateValidator,
        IValidator<UpdateStudentIdentityRequest> identityValidator,
        IValidator<LinkGuardianRequest> linkValidator,
        ICurrentUserPermissions permissions)
    {
        _studentService = studentService;
        _guardianService = guardianService;
        _createValidator = createValidator;
        _updateValidator = updateValidator;
        _identityValidator = identityValidator;
        _linkValidator = linkValidator;
        _permissions = permissions;
    }

    // Full Aadhaar / passport numbers are returned only to callers holding this permission;
    // everyone else gets masked values. The service never decides this itself.
    // Read from the permission cache: the JWT carries no permissions (D1).
    private Task<bool> CanViewSensitiveAsync(CancellationToken cancellationToken) =>
        _permissions.HasPermissionAsync(Permissions.StudentsViewSensitive, cancellationToken);

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

    [HttpGet]
    public async Task<IActionResult> GetAll(CancellationToken cancellationToken) =>
        Ok(await _studentService.GetAllAsync(cancellationToken));

    [HttpGet("{id:int}")]
    public async Task<IActionResult> GetById(int id, CancellationToken cancellationToken) =>
        Ok(await _studentService.GetByIdAsync(id, await CanViewSensitiveAsync(cancellationToken), cancellationToken));

    [HttpPost]
    [Authorize(Policy = "Students.Manage")]
    public async Task<IActionResult> Create(CreateStudentRequest request, CancellationToken cancellationToken)
    {
        var validationError = await ValidateAsync(_createValidator, request, cancellationToken);
        if (validationError is not null) return validationError;

        var result = await _studentService.CreateAsync(request, await CanViewSensitiveAsync(cancellationToken), cancellationToken);
        return Created($"api/students/{result.Id}", result);
    }

    [HttpPut("{id:int}")]
    [Authorize(Policy = "Students.Manage")]
    public async Task<IActionResult> Update(int id, UpdateStudentRequest request, CancellationToken cancellationToken)
    {
        var validationError = await ValidateAsync(_updateValidator, request, cancellationToken);
        if (validationError is not null) return validationError;

        return Ok(await _studentService.UpdateAsync(id, request, await CanViewSensitiveAsync(cancellationToken), cancellationToken));
    }

    /// <summary>Set Aadhaar / passport / visa details. Requires Students.ViewSensitive (and Students.Manage).</summary>
    [HttpPut("{id:int}/identity")]
    [Authorize(Policy = "Students.Manage")]
    [Authorize(Policy = "Students.ViewSensitive")]
    public async Task<IActionResult> UpdateIdentity(int id, UpdateStudentIdentityRequest request, CancellationToken cancellationToken)
    {
        var validationError = await ValidateAsync(_identityValidator, request, cancellationToken);
        if (validationError is not null) return validationError;

        return Ok(await _studentService.UpdateIdentityAsync(id, request, cancellationToken));
    }

    /// <summary>The student's academic history, newest period first. Read-only.</summary>
    [HttpGet("{id:int}/enrollments")]
    public async Task<IActionResult> GetEnrollments(int id, CancellationToken cancellationToken) =>
        Ok(await _studentService.GetEnrollmentsAsync(id, cancellationToken));

    [HttpGet("{id:int}/guardians")]
    public async Task<IActionResult> GetGuardians(int id, CancellationToken cancellationToken) =>
        Ok(await _guardianService.GetForStudentAsync(id, cancellationToken));

    [HttpPost("{id:int}/guardians")]
    [Authorize(Policy = "Parents.Manage")]
    public async Task<IActionResult> LinkGuardian(int id, LinkGuardianRequest request, CancellationToken cancellationToken)
    {
        var validationError = await ValidateAsync(_linkValidator, request, cancellationToken);
        if (validationError is not null) return validationError;

        var result = await _guardianService.LinkAsync(id, request, cancellationToken);
        return Created($"api/students/{id}/guardians/{result.ParentId}", result);
    }

    [HttpDelete("{id:int}/guardians/{parentId:int}")]
    [Authorize(Policy = "Parents.Manage")]
    public async Task<IActionResult> UnlinkGuardian(int id, int parentId, CancellationToken cancellationToken)
    {
        await _guardianService.UnlinkAsync(id, parentId, cancellationToken);
        return NoContent();
    }

    [HttpDelete("{id:int}")]
    [Authorize(Policy = "Students.Manage")]
    public async Task<IActionResult> Delete(int id, CancellationToken cancellationToken)
    {
        await _studentService.DeleteAsync(id, cancellationToken);
        return NoContent();
    }
}