using FluentValidation;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using SchoolManagement.API.Common;
using SchoolManagement.API.Extensions;
using SchoolManagement.Application.DTOs.Settings;
using SchoolManagement.Application.Interfaces;
using SchoolManagement.Domain.Constants;

namespace SchoolManagement.API.Controllers;

/// <summary>E2: the school's own settings (one record).</summary>
[ApiController]
[Route("api/settings")]
[Authorize]
public class SettingsController : ControllerBase
{
    private readonly ISchoolSettingsService _settings;
    private readonly IValidator<UpdateSchoolSettingsRequest> _validator;

    public SettingsController(ISchoolSettingsService settings, IValidator<UpdateSchoolSettingsRequest> validator)
    {
        _settings = settings;
        _validator = validator;
    }

    [HttpGet("school")]
    [Authorize(Policy = Permissions.SettingsView)]
    [ProducesResponseType(typeof(SchoolSettingsResponse), StatusCodes.Status200OK)]
    public async Task<IActionResult> GetSchool(CancellationToken cancellationToken) =>
        Ok(await _settings.GetAsync(cancellationToken));

    [HttpPut("school")]
    [Authorize(Policy = Permissions.SettingsEdit)]
    [ProducesResponseType(typeof(SchoolSettingsResponse), StatusCodes.Status200OK)]
    [ProducesResponseType(typeof(ApiErrorResponse), StatusCodes.Status400BadRequest)]
    [ProducesResponseType(typeof(ApiErrorResponse), StatusCodes.Status409Conflict)]
    public async Task<IActionResult> UpdateSchool(UpdateSchoolSettingsRequest request, CancellationToken cancellationToken)
    {
        var result = await _validator.ValidateAsync(request, cancellationToken);
        if (!result.IsValid)
        {
            return BadRequest(new ApiErrorResponse
            {
                Message = "Validation failed.",
                ErrorCode = "VALIDATION_ERROR",
                Errors = result.ToErrorDictionary(),
                TraceId = HttpContext.GetCorrelationId(),
            });
        }

        return Ok(await _settings.UpdateAsync(request, cancellationToken));
    }

    /// <summary>The time-zone picker: IANA ids with today's UTC offset.</summary>
    [HttpGet("time-zones")]
    [Authorize(Policy = Permissions.SettingsView)]
    [ProducesResponseType(typeof(List<TimeZoneOption>), StatusCodes.Status200OK)]
    public IActionResult GetTimeZones() => Ok(_settings.GetTimeZones());

    /// <summary>School name and country for every signed-in user (top bar; address forms pre-select the country).</summary>
    [HttpGet("basics")]
    [ProducesResponseType(typeof(SchoolBasicsResponse), StatusCodes.Status200OK)]
    public async Task<IActionResult> GetBasics(CancellationToken cancellationToken) =>
        Ok(await _settings.GetBasicsAsync(cancellationToken));
}
