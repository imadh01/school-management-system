using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using FluentValidation;
using SchoolManagement.API.Common;
using SchoolManagement.API.Extensions;
using SchoolManagement.Application.DTOs.Parents;
using SchoolManagement.Application.Interfaces;

namespace SchoolManagement.API.Controllers;

[ApiController]
[Route("api/parents")]
public class ParentsController : ControllerBase
{
    private readonly IParentService _parentService;
    private readonly IStudentGuardianService _guardianService;
    private readonly IValidator<CreateParentRequest> _createValidator;
    private readonly IValidator<UpdateParentRequest> _updateValidator;
    private readonly IValidator<LinkStudentRequest> _linkStudentValidator;

    public ParentsController(
        IParentService parentService,
        IStudentGuardianService guardianService,
        IValidator<CreateParentRequest> createValidator,
        IValidator<UpdateParentRequest> updateValidator,
        IValidator<LinkStudentRequest> linkStudentValidator)
    {
        _parentService = parentService;
        _guardianService = guardianService;
        _createValidator = createValidator;
        _updateValidator = updateValidator;
        _linkStudentValidator = linkStudentValidator;
    }

    [HttpGet]
    public async Task<IActionResult> GetAll(CancellationToken cancellationToken) =>
        Ok(await _parentService.GetAllAsync(cancellationToken));

    [HttpGet("{id:int}")]
    public async Task<IActionResult> GetById(int id, CancellationToken cancellationToken) =>
        Ok(await _parentService.GetByIdAsync(id, cancellationToken));

    [HttpPost]
    [Authorize(Policy = "Parents.Manage")]
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
    [Authorize(Policy = "Parents.Manage")]
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
    [Authorize(Policy = "Parents.Manage")]
    public async Task<IActionResult> Delete(int id, CancellationToken cancellationToken)
    {
        await _parentService.DeleteAsync(id, cancellationToken);
        return NoContent();
    }

    [HttpGet("{id:int}/students")]
    public async Task<IActionResult> GetLinkedStudents(int id, CancellationToken cancellationToken) =>
        Ok(await _guardianService.GetForParentAsync(id, cancellationToken));

    [HttpPost("{id:int}/students")]
    [Authorize(Policy = "Parents.Manage")]
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
    [Authorize(Policy = "Parents.Manage")]
    public async Task<IActionResult> UnlinkStudent(int id, int studentId, CancellationToken cancellationToken)
    {
        await _guardianService.UnlinkAsync(studentId, id, cancellationToken);
        return NoContent();
    }

    [HttpPost("{id:int}/students/{studentId:int}/primary")]
    [Authorize(Policy = "Parents.Manage")]
    public async Task<IActionResult> SetPrimaryStudent(int id, int studentId, CancellationToken cancellationToken)
    {
        await _guardianService.SetPrimaryAsync(id, studentId, cancellationToken);
        return NoContent();
    }
}