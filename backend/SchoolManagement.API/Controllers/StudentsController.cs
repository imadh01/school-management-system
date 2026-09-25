using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using FluentValidation;
using SchoolManagement.API.Common;
using SchoolManagement.API.Extensions;
using SchoolManagement.Application.DTOs.Students;
using SchoolManagement.Application.DTOs.Parents;
using SchoolManagement.Application.Interfaces;

namespace SchoolManagement.API.Controllers;

[ApiController]
[Route("api/students")]
public class StudentsController : ControllerBase
{
    private readonly IStudentService _studentService;
    private readonly IStudentGuardianService _guardianService;
    private readonly IValidator<CreateStudentRequest> _createValidator;
    private readonly IValidator<UpdateStudentRequest> _updateValidator;
    private readonly IValidator<LinkGuardianRequest> _linkValidator;

    public StudentsController(
        IStudentService studentService,
        IStudentGuardianService guardianService,
        IValidator<CreateStudentRequest> createValidator,
        IValidator<UpdateStudentRequest> updateValidator,
        IValidator<LinkGuardianRequest> linkValidator)
    {
        _studentService = studentService;
        _guardianService = guardianService;
        _createValidator = createValidator;
        _updateValidator = updateValidator;
        _linkValidator = linkValidator;
    }

    [HttpGet]
    public async Task<IActionResult> GetAll(CancellationToken cancellationToken) =>
        Ok(await _studentService.GetAllAsync(cancellationToken));

    [HttpGet("{id:int}")]
    public async Task<IActionResult> GetById(int id, CancellationToken cancellationToken) =>
        Ok(await _studentService.GetByIdAsync(id, cancellationToken));

    [HttpPost]
    [Authorize(Policy = "Students.Manage")]
    public async Task<IActionResult> Create(CreateStudentRequest request, CancellationToken cancellationToken)
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

        var result = await _studentService.CreateAsync(request, cancellationToken);
        return Created($"api/students/{result.Id}", result);
    }

    [HttpPut("{id:int}")]
    [Authorize(Policy = "Students.Manage")]
    public async Task<IActionResult> Update(int id, UpdateStudentRequest request, CancellationToken cancellationToken)
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

        return Ok(await _studentService.UpdateAsync(id, request, cancellationToken));
    }

    [HttpGet("{id:int}/guardians")]
    public async Task<IActionResult> GetGuardians(int id, CancellationToken cancellationToken) =>
        Ok(await _guardianService.GetForStudentAsync(id, cancellationToken));

    [HttpPost("{id:int}/guardians")]
    [Authorize(Policy = "Parents.Manage")]
    public async Task<IActionResult> LinkGuardian(int id, LinkGuardianRequest request, CancellationToken cancellationToken)
    {
        var validationResult = await _linkValidator.ValidateAsync(request, cancellationToken);
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