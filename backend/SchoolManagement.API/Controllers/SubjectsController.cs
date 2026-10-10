using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using FluentValidation;
using SchoolManagement.API.Common;
using SchoolManagement.API.Extensions;
using SchoolManagement.Application.DTOs.Subjects;
using SchoolManagement.Application.Interfaces;

namespace SchoolManagement.API.Controllers;

[ApiController]
[Route("api/subjects")]
[Authorize]
public class SubjectsController : ControllerBase
{
    private readonly ISubjectService _subjectService;
    private readonly IValidator<CreateSubjectRequest> _createValidator;
    private readonly IValidator<UpdateSubjectRequest> _updateValidator;

    public SubjectsController(
        ISubjectService subjectService,
        IValidator<CreateSubjectRequest> createValidator,
        IValidator<UpdateSubjectRequest> updateValidator)
    {
        _subjectService = subjectService;
        _createValidator = createValidator;
        _updateValidator = updateValidator;
    }

    [HttpGet]
    public async Task<IActionResult> GetAll(CancellationToken cancellationToken) =>
        Ok(await _subjectService.GetAllAsync(cancellationToken));

    [HttpGet("{id:int}")]
    public async Task<IActionResult> GetById(int id, CancellationToken cancellationToken) =>
        Ok(await _subjectService.GetByIdAsync(id, cancellationToken));

    [HttpPost]
    [Authorize(Policy = "Subjects.Manage")]
    public async Task<IActionResult> Create(CreateSubjectRequest request, CancellationToken cancellationToken)
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

        var result = await _subjectService.CreateAsync(request, cancellationToken);
        return Created($"api/subjects/{result.Id}", result);
    }

    [HttpPut("{id:int}")]
    [Authorize(Policy = "Subjects.Manage")]
    public async Task<IActionResult> Update(int id, UpdateSubjectRequest request, CancellationToken cancellationToken)
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

        return Ok(await _subjectService.UpdateAsync(id, request, cancellationToken));
    }

    [HttpDelete("{id:int}")]
    [Authorize(Policy = "Subjects.Manage")]
    public async Task<IActionResult> Delete(int id, CancellationToken cancellationToken)
    {
        await _subjectService.DeleteAsync(id, cancellationToken);
        return NoContent();
    }
}