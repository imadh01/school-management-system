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
public class ClassSectionsController : ControllerBase
{
    private readonly IClassSectionService _classSectionService;
    private readonly IValidator<CreateClassSectionRequest> _validator;

    public ClassSectionsController(
        IClassSectionService classSectionService,
        IValidator<CreateClassSectionRequest> validator)
    {
        _classSectionService = classSectionService;
        _validator = validator;
    }

    [HttpGet]
    [ProducesResponseType(typeof(List<ClassSectionResponse>), StatusCodes.Status200OK)]
    public async Task<IActionResult> GetAll(CancellationToken cancellationToken)
    {
        var sections = await _classSectionService.GetAllAsync(cancellationToken);
        return Ok(sections);
    }

    [HttpPost]
    [Authorize(Policy = "ClassSections.Create")]
    [ProducesResponseType(typeof(ClassSectionResponse), StatusCodes.Status201Created)]
    [ProducesResponseType(typeof(ApiErrorResponse), StatusCodes.Status400BadRequest)]
    [ProducesResponseType(typeof(ApiErrorResponse), StatusCodes.Status404NotFound)]
    [ProducesResponseType(typeof(ApiErrorResponse), StatusCodes.Status409Conflict)]
    public async Task<IActionResult> Create(CreateClassSectionRequest request, CancellationToken cancellationToken)
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

        var result = await _classSectionService.CreateAsync(request, cancellationToken);
        return Created($"api/class-sections/{result.Id}", result);
    }
}
