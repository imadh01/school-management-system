using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using FluentValidation;
using SchoolManagement.API.Common;
using SchoolManagement.API.Extensions;
using SchoolManagement.Application.DTOs.Parents;
using SchoolManagement.Application.Interfaces;
using SchoolManagement.Domain.Constants;

namespace SchoolManagement.API.Controllers;

[ApiController]
[Route("api/parents")]
[Authorize]
public class ParentsController : ControllerBase
{
    private readonly IParentService _parentService;
    private readonly IStudentGuardianService _guardianService;
    private readonly IValidator<CreateParentRequest> _createValidator;
    private readonly IValidator<UpdateParentRequest> _updateValidator;
    private readonly IValidator<LinkStudentRequest> _linkStudentValidator;
    private readonly IDataScopeResolver _scopes;

    public ParentsController(
        IParentService parentService,
        IStudentGuardianService guardianService,
        IValidator<CreateParentRequest> createValidator,
        IValidator<UpdateParentRequest> updateValidator,
        IValidator<LinkStudentRequest> linkStudentValidator,
        IDataScopeResolver scopes)
    {
        _parentService = parentService;
        _guardianService = guardianService;
        _createValidator = createValidator;
        _updateValidator = updateValidator;
        _linkStudentValidator = linkStudentValidator;
        _scopes = scopes;
    }

    // F1: reads accept Parents.View (every parent, full shape) or Parents.ViewScoped (parents of
    // students you teach and your own record; name and mobile only). Out of scope = 404, like missing.
    private Task<DataScope> ParentScopeAsync(CancellationToken cancellationToken) =>
        _scopes.ResolveAsync(Permissions.ParentsView, Permissions.ParentsViewScoped, cancellationToken);

    /// <summary>Parents.View: ParentResponse list. Parents.ViewScoped: ParentContactResponse list.</summary>
    [HttpGet]
    [Authorize(Policy = CompositePolicies.ParentsRead)]
    [ProducesResponseType(typeof(List<ParentResponse>), StatusCodes.Status200OK)]
    public async Task<IActionResult> GetAll(CancellationToken cancellationToken)
    {
        var scope = await ParentScopeAsync(cancellationToken);
        return scope.IsUnrestricted
            ? Ok(await _parentService.GetAllAsync(cancellationToken))
            : Ok(await _parentService.GetAllScopedAsync(scope, cancellationToken));
    }

    /// <summary>Parents.View: ParentResponse. Parents.ViewScoped: ParentContactResponse.</summary>
    [HttpGet("{id:int}")]
    [Authorize(Policy = CompositePolicies.ParentsRead)]
    [ProducesResponseType(typeof(ParentResponse), StatusCodes.Status200OK)]
    [ProducesResponseType(typeof(ApiErrorResponse), StatusCodes.Status404NotFound)]
    public async Task<IActionResult> GetById(int id, CancellationToken cancellationToken)
    {
        var scope = await ParentScopeAsync(cancellationToken);
        return scope.IsUnrestricted
            ? Ok(await _parentService.GetByIdAsync(id, cancellationToken))
            : Ok(await _parentService.GetByIdScopedAsync(id, scope, cancellationToken));
    }

    [HttpPost]
    [Authorize(Policy = Permissions.ParentsCreate)]
    public async Task<IActionResult> Create(CreateParentRequest request, CancellationToken cancellationToken)
    {
        var validationResult = await _createValidator.ValidateAsync(request, cancellationToken);
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

        var result = await _parentService.CreateAsync(request, cancellationToken);
        return Created($"api/parents/{result.Id}", result);
    }

    [HttpPut("{id:int}")]
    [Authorize(Policy = Permissions.ParentsEdit)]
    public async Task<IActionResult> Update(int id, UpdateParentRequest request, CancellationToken cancellationToken)
    {
        var validationResult = await _updateValidator.ValidateAsync(request, cancellationToken);
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

        return Ok(await _parentService.UpdateAsync(id, request, cancellationToken));
    }

    [HttpDelete("{id:int}")]
    [Authorize(Policy = Permissions.ParentsDelete)]
    public async Task<IActionResult> Delete(int id, CancellationToken cancellationToken)
    {
        await _parentService.DeleteAsync(id, cancellationToken);
        return NoContent();
    }

    /// <summary>The parent's children. Scoped callers see only the children inside their scope.</summary>
    [HttpGet("{id:int}/students")]
    [Authorize(Policy = CompositePolicies.ParentsRead)]
    [ProducesResponseType(typeof(List<LinkedStudentResponse>), StatusCodes.Status200OK)]
    [ProducesResponseType(typeof(ApiErrorResponse), StatusCodes.Status404NotFound)]
    public async Task<IActionResult> GetLinkedStudents(int id, CancellationToken cancellationToken) =>
        Ok(await _guardianService.GetForParentAsync(id, await ParentScopeAsync(cancellationToken), cancellationToken));

    [HttpPost("{id:int}/students")]
    [Authorize(Policy = Permissions.ParentsEdit)]
    public async Task<IActionResult> LinkStudent(int id, LinkStudentRequest request, CancellationToken cancellationToken)
    {
        var validationResult = await _linkStudentValidator.ValidateAsync(request, cancellationToken);
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

        var result = await _guardianService.LinkStudentAsync(id, request, cancellationToken);
        return Created($"api/parents/{id}/students/{result.StudentId}", result);
    }

    [HttpDelete("{id:int}/students/{studentId:int}")]
    [Authorize(Policy = Permissions.ParentsEdit)]
    public async Task<IActionResult> UnlinkStudent(int id, int studentId, CancellationToken cancellationToken)
    {
        await _guardianService.UnlinkAsync(studentId, id, cancellationToken);
        return NoContent();
    }

    [HttpPost("{id:int}/students/{studentId:int}/primary")]
    [Authorize(Policy = Permissions.ParentsEdit)]
    public async Task<IActionResult> SetPrimaryStudent(int id, int studentId, CancellationToken cancellationToken)
    {
        await _guardianService.SetPrimaryAsync(id, studentId, cancellationToken);
        return NoContent();
    }
}
