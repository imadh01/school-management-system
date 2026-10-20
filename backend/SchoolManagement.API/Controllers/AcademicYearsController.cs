using FluentValidation;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using SchoolManagement.API.Common;
using SchoolManagement.API.Extensions;
using SchoolManagement.Application.DTOs.AcademicYears;
using SchoolManagement.Application.Interfaces;
using SchoolManagement.Domain.Constants;

namespace SchoolManagement.API.Controllers;

/// <summary>
/// Academic years and their life: Upcoming, Current, Ended, Closed (see docs/design/academic-years.md).
/// The LIST is reference data for every dropdown, so (like Countries) any signed-in user may read it.
/// Everything else has its own AcademicYears.* permission, held only by Admin unless the Roles page grants it.
/// </summary>
[ApiController]
[Route("api/academic-years")]
[Authorize]
public class AcademicYearsController : ControllerBase
{
    private readonly IAcademicYearService _academicYearService;
    private readonly IValidator<CreateAcademicYearRequest> _createValidator;
    private readonly IValidator<UpdateAcademicYearRequest> _updateValidator;
    private readonly IValidator<SetCurrentAcademicYearRequest> _setCurrentValidator;
    private readonly IValidator<CloseAcademicYearRequest> _closeValidator;

    public AcademicYearsController(
        IAcademicYearService academicYearService,
        IValidator<CreateAcademicYearRequest> createValidator,
        IValidator<UpdateAcademicYearRequest> updateValidator,
        IValidator<SetCurrentAcademicYearRequest> setCurrentValidator,
        IValidator<CloseAcademicYearRequest> closeValidator)
    {
        _academicYearService = academicYearService;
        _createValidator = createValidator;
        _updateValidator = updateValidator;
        _setCurrentValidator = setCurrentValidator;
        _closeValidator = closeValidator;
    }

    /// <summary>Every year, newest first. Optional ?status=Upcoming|Current|Ended|Closed.</summary>
    [HttpGet]
    public async Task<IActionResult> GetAll([FromQuery] string? status, CancellationToken cancellationToken) =>
        Ok(await _academicYearService.GetAllAsync(status, cancellationToken));

    [HttpGet("{id:int}")]
    [Authorize(Policy = Permissions.AcademicYearsView)]
    public async Task<IActionResult> GetById(int id, CancellationToken cancellationToken) =>
        Ok(await _academicYearService.GetByIdAsync(id, cancellationToken));

    [HttpPost]
    [Authorize(Policy = Permissions.AcademicYearsCreate)]
    public async Task<IActionResult> Create(CreateAcademicYearRequest request, CancellationToken cancellationToken)
    {
        var invalid = await ValidateAsync(_createValidator, request, cancellationToken);
        if (invalid is not null) return invalid;

        var result = await _academicYearService.CreateAsync(request, cancellationToken);
        return Created($"api/academic-years/{result.Id}", result);
    }

    [HttpPut("{id:int}")]
    [Authorize(Policy = Permissions.AcademicYearsEdit)]
    public async Task<IActionResult> Update(int id, UpdateAcademicYearRequest request, CancellationToken cancellationToken)
    {
        var invalid = await ValidateAsync(_updateValidator, request, cancellationToken);
        if (invalid is not null) return invalid;

        return Ok(await _academicYearService.UpdateAsync(id, request, cancellationToken));
    }

    [HttpPost("{id:int}/set-current")]
    [Authorize(Policy = Permissions.AcademicYearsSetCurrent)]
    public async Task<IActionResult> SetCurrent(int id, SetCurrentAcademicYearRequest request, CancellationToken cancellationToken)
    {
        var invalid = await ValidateAsync(_setCurrentValidator, request, cancellationToken);
        if (invalid is not null) return invalid;

        return Ok(await _academicYearService.SetCurrentAsync(id, request, cancellationToken));
    }

    [HttpPost("{id:int}/close")]
    [Authorize(Policy = Permissions.AcademicYearsClose)]
    public async Task<IActionResult> Close(int id, CloseAcademicYearRequest request, CancellationToken cancellationToken)
    {
        var invalid = await ValidateAsync(_closeValidator, request, cancellationToken);
        if (invalid is not null) return invalid;

        return Ok(await _academicYearService.CloseAsync(id, request, cancellationToken));
    }

    /// <summary>Copies the periods of year <c>fromId</c> into year <c>id</c>, which must have none.</summary>
    [HttpPost("{id:int}/copy-periods")]
    [Authorize(Policy = Permissions.AcademicYearsCopyPeriods)]
    public async Task<IActionResult> CopyPeriods(int id, [FromQuery] int fromId, CancellationToken cancellationToken)
    {
        if (fromId <= 0)
        {
            return BadRequest(new ApiErrorResponse
            {
                Message = "Validation failed.",
                ErrorCode = "VALIDATION_ERROR",
                Errors = new Dictionary<string, string[]> { ["fromId"] = new[] { "Choose the year to copy the periods from." } },
                TraceId = HttpContext.GetCorrelationId(),
            });
        }

        return Ok(await _academicYearService.CopyPeriodsAsync(id, fromId, cancellationToken));
    }

    [HttpDelete("{id:int}")]
    [Authorize(Policy = Permissions.AcademicYearsDelete)]
    public async Task<IActionResult> Delete(int id, CancellationToken cancellationToken)
    {
        await _academicYearService.DeleteAsync(id, cancellationToken);
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
