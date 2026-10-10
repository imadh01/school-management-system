using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using FluentValidation;
using SchoolManagement.API.Common;
using SchoolManagement.API.Extensions;
using SchoolManagement.Application.DTOs.ClassSections;
using SchoolManagement.Application.Interfaces;

namespace SchoolManagement.API.Controllers;

[ApiController]
[Route("api/class-sections")]
[Authorize]
public class ClassSectionsController : ControllerBase
{
    private readonly IClassSectionService _classSectionService;
    private readonly IValidator<CreateClassSectionRequest> _createValidator;
    private readonly IValidator<UpdateClassSectionRequest> _updateValidator;

    public ClassSectionsController(
        IClassSectionService classSectionService,
        IValidator<CreateClassSectionRequest> createValidator,
        IValidator<UpdateClassSectionRequest> updateValidator)
    {
        _classSectionService = classSectionService;
        _createValidator = createValidator;
        _updateValidator = updateValidator;
    }

    [HttpGet]
    [ProducesResponseType(typeof(List<ClassSectionResponse>), StatusCodes.Status200OK)]
    public async Task<IActionResult> GetAll(CancellationToken cancellationToken) =>
        Ok(await _classSectionService.GetAllAsync(cancellationToken));

    [HttpGet("{id:int}")]
    [ProducesResponseType(typeof(ClassSectionResponse), StatusCodes.Status200OK)]
    [ProducesResponseType(typeof(ApiErrorResponse), StatusCodes.Status404NotFound)]
    public async Task<IActionResult> GetById(int id, CancellationToken cancellationToken) =>
        Ok(await _classSectionService.GetByIdAsync(id, cancellationToken));

    [HttpPost]
    [Authorize(Policy = "ClassSections.Manage")]
    [ProducesResponseType(typeof(ClassSectionResponse), StatusCodes.Status201Created)]
    [ProducesResponseType(typeof(ApiErrorResponse), StatusCodes.Status400BadRequest)]
    [ProducesResponseType(typeof(ApiErrorResponse), StatusCodes.Status404NotFound)]
    [ProducesResponseType(typeof(ApiErrorResponse), StatusCodes.Status409Conflict)]
    public async Task<IActionResult> Create(CreateClassSectionRequest request, CancellationToken cancellationToken)
    {
        var validationResult = await _createValidator.ValidateAsync(request, cancellationToken);
        if (!validationResult.IsValid)
            return ValidationFailed(validationResult);

        var result = await _classSectionService.CreateAsync(request, cancellationToken);
        return Created($"api/class-sections/{result.Id}", result);
    }

    [HttpPut("{id:int}")]
    [Authorize(Policy = "ClassSections.Manage")]
    [ProducesResponseType(typeof(ClassSectionResponse), StatusCodes.Status200OK)]
    [ProducesResponseType(typeof(ApiErrorResponse), StatusCodes.Status400BadRequest)]
    [ProducesResponseType(typeof(ApiErrorResponse), StatusCodes.Status404NotFound)]
    [ProducesResponseType(typeof(ApiErrorResponse), StatusCodes.Status409Conflict)]
    public async Task<IActionResult> Update(int id, UpdateClassSectionRequest request, CancellationToken cancellationToken)
    {
        var validationResult = await _updateValidator.ValidateAsync(request, cancellationToken);
        if (!validationResult.IsValid)
            return ValidationFailed(validationResult);

        return Ok(await _classSectionService.UpdateAsync(id, request, cancellationToken));
    }

    [HttpPatch("{id:int}/status")]
    [Authorize(Policy = "ClassSections.Manage")]
    [ProducesResponseType(typeof(ClassSectionResponse), StatusCodes.Status200OK)]
    [ProducesResponseType(typeof(ApiErrorResponse), StatusCodes.Status404NotFound)]
    public async Task<IActionResult> ChangeStatus(int id, ChangeClassSectionStatusRequest request, CancellationToken cancellationToken) =>
        Ok(await _classSectionService.ChangeStatusAsync(id, request.IsActive, cancellationToken));

    [HttpDelete("{id:int}")]
    [Authorize(Policy = "ClassSections.Manage")]
    [ProducesResponseType(StatusCodes.Status204NoContent)]
    [ProducesResponseType(typeof(ApiErrorResponse), StatusCodes.Status404NotFound)]
    [ProducesResponseType(typeof(ApiErrorResponse), StatusCodes.Status409Conflict)]
    public async Task<IActionResult> Delete(int id, CancellationToken cancellationToken)
    {
        await _classSectionService.DeleteAsync(id, cancellationToken);
        return NoContent();
    }

    private IActionResult ValidationFailed(FluentValidation.Results.ValidationResult result) =>
        BadRequest(new ApiErrorResponse
        {
            Message = "Validation failed.",
            ErrorCode = "VALIDATION_ERROR",
            Errors = result.ToErrorDictionary(),
            TraceId = HttpContext.GetCorrelationId(),
        });
}