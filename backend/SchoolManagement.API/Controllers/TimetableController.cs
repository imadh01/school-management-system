using FluentValidation;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using SchoolManagement.API.Common;
using SchoolManagement.API.Extensions;
using SchoolManagement.Application.DTOs.Timetable;
using SchoolManagement.Application.Interfaces;
using SchoolManagement.Domain.Constants;

namespace SchoolManagement.API.Controllers;

[ApiController]
[Route("api/timetable")]
[Authorize]
public class TimetableController : ControllerBase
{
    private readonly ITimetableService _timetableService;
    private readonly IDataScopeResolver _scopes;
    private readonly IValidator<CreateTimetablePeriodRequest> _createPeriodValidator;
    private readonly IValidator<UpdateTimetablePeriodRequest> _updatePeriodValidator;
    private readonly IValidator<SaveTimetableCellsRequest> _cellsValidator;

    public TimetableController(
        ITimetableService timetableService,
        IDataScopeResolver scopes,
        IValidator<CreateTimetablePeriodRequest> createPeriodValidator,
        IValidator<UpdateTimetablePeriodRequest> updatePeriodValidator,
        IValidator<SaveTimetableCellsRequest> cellsValidator)
    {
        _timetableService = timetableService;
        _scopes = scopes;
        _createPeriodValidator = createPeriodValidator;
        _updatePeriodValidator = updatePeriodValidator;
        _cellsValidator = cellsValidator;
    }

    // ------------------------------------------------------------------ periods

    [HttpGet("periods")]
    [Authorize(Policy = Permissions.TimetableView)]
    public async Task<IActionResult> GetPeriods([FromQuery] int? academicYearId, CancellationToken cancellationToken) =>
        Ok(await _timetableService.GetPeriodsAsync(academicYearId, cancellationToken));

    [HttpPost("periods")]
    [Authorize(Policy = Permissions.TimetableManagePeriods)]
    public async Task<IActionResult> CreatePeriod(CreateTimetablePeriodRequest request, CancellationToken cancellationToken)
    {
        var invalid = await ValidateAsync(_createPeriodValidator, request, cancellationToken);
        if (invalid is not null) return invalid;

        var result = await _timetableService.CreatePeriodAsync(request, cancellationToken);
        return Created($"api/timetable/periods/{result.Id}", result);
    }

    [HttpPut("periods/{id:int}")]
    [Authorize(Policy = Permissions.TimetableManagePeriods)]
    public async Task<IActionResult> UpdatePeriod(int id, UpdateTimetablePeriodRequest request, CancellationToken cancellationToken)
    {
        var invalid = await ValidateAsync(_updatePeriodValidator, request, cancellationToken);
        if (invalid is not null) return invalid;

        return Ok(await _timetableService.UpdatePeriodAsync(id, request, cancellationToken));
    }

    [HttpDelete("periods/{id:int}")]
    [Authorize(Policy = Permissions.TimetableManagePeriods)]
    public async Task<IActionResult> DeletePeriod(int id, CancellationToken cancellationToken)
    {
        await _timetableService.DeletePeriodAsync(id, cancellationToken);
        return NoContent();
    }

    // ------------------------------------------------------------------ class grid

    [HttpGet("classes/{classSectionId:int}")]
    [Authorize(Policy = Permissions.TimetableView)]
    public async Task<IActionResult> GetClassTimetable(int classSectionId, CancellationToken cancellationToken) =>
        Ok(await _timetableService.GetClassTimetableAsync(classSectionId, cancellationToken));

    [HttpPut("classes/{classSectionId:int}/cells")]
    [Authorize(Policy = Permissions.TimetableEdit)]
    public async Task<IActionResult> SaveCells(int classSectionId, SaveTimetableCellsRequest request, CancellationToken cancellationToken)
    {
        var invalid = await ValidateAsync(_cellsValidator, request, cancellationToken);
        if (invalid is not null) return invalid;

        return Ok(await _timetableService.SaveCellsAsync(classSectionId, request, cancellationToken));
    }

    [HttpDelete("classes/{classSectionId:int}/cells")]
    [Authorize(Policy = Permissions.TimetableEdit)]
    public async Task<IActionResult> ClearCell(
        int classSectionId, [FromQuery] int day, [FromQuery] int periodId, CancellationToken cancellationToken)
    {
        if (day is < 0 or > 6 || periodId <= 0)
        {
            return BadRequest(new ApiErrorResponse
            {
                Message = "Validation failed.",
                ErrorCode = "VALIDATION_ERROR",
                Errors = new Dictionary<string, string[]>
                {
                    ["day"] = new[] { "Day must be between 0 (Sunday) and 6 (Saturday)." },
                    ["periodId"] = new[] { "Period is required." },
                },
                TraceId = HttpContext.GetCorrelationId(),
            });
        }

        await _timetableService.ClearCellAsync(classSectionId, (DayOfWeek)day, periodId, cancellationToken);
        return NoContent();
    }

    [HttpPost("classes/{classSectionId:int}/copy-from/{sourceClassSectionId:int}")]
    [Authorize(Policy = Permissions.TimetableEdit)]
    public async Task<IActionResult> CopyFrom(int classSectionId, int sourceClassSectionId, CancellationToken cancellationToken) =>
        Ok(await _timetableService.CopyFromAsync(classSectionId, sourceClassSectionId, cancellationToken));

    // ------------------------------------------------------------------ teacher's week

    [HttpGet("teachers/{teacherId:int}")]
    [Authorize(Policy = CompositePolicies.TimetableRead)]
    public async Task<IActionResult> GetTeacherTimetable(
        int teacherId, [FromQuery] int? academicYearId, CancellationToken cancellationToken)
    {
        // Timetable.View: any teacher's week. Timetable.ViewScoped only: your own (the service answers 404 otherwise).
        var scope = await _scopes.ResolveAsync(Permissions.TimetableView, Permissions.TimetableViewScoped, cancellationToken);
        return Ok(await _timetableService.GetTeacherTimetableAsync(teacherId, academicYearId, scope, cancellationToken));
    }

    // ------------------------------------------------------------------ helpers

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
