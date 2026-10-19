using FluentValidation;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using SchoolManagement.API.Common;
using SchoolManagement.API.Extensions;
using SchoolManagement.Application.DTOs.AdmissionFees;
using SchoolManagement.Application.Interfaces;
using SchoolManagement.Domain.Constants;

namespace SchoolManagement.API.Controllers;

/// <summary>
/// Master data: the admission fee of each class. Viewing needs Settings.View, changing needs Settings.Edit
/// (masters are school configuration, so they share the Settings permissions).
/// </summary>
[ApiController]
[Route("api/admission-fees")]
[Authorize]
public class AdmissionFeesController : ControllerBase
{
    private readonly IAdmissionFeeService _fees;
    private readonly IValidator<SetAdmissionFeeRequest> _validator;

    public AdmissionFeesController(IAdmissionFeeService fees, IValidator<SetAdmissionFeeRequest> validator)
    {
        _fees = fees;
        _validator = validator;
    }

    /// <summary>Every class with its fee (null when none is set).</summary>
    [HttpGet]
    [Authorize(Policy = Permissions.SettingsView)]
    [ProducesResponseType(typeof(List<AdmissionFeeResponse>), StatusCodes.Status200OK)]
    public async Task<IActionResult> GetAll(CancellationToken cancellationToken) =>
        Ok(await _fees.GetAllAsync(cancellationToken));

    /// <summary>Sets the fee of a class (creates or replaces).</summary>
    [HttpPut("{className}")]
    [Authorize(Policy = Permissions.SettingsEdit)]
    [ProducesResponseType(typeof(AdmissionFeeResponse), StatusCodes.Status200OK)]
    [ProducesResponseType(typeof(ApiErrorResponse), StatusCodes.Status400BadRequest)]
    [ProducesResponseType(typeof(ApiErrorResponse), StatusCodes.Status404NotFound)]
    public async Task<IActionResult> Set(string className, SetAdmissionFeeRequest request, CancellationToken cancellationToken)
    {
        var result = await _validator.ValidateAsync(request, cancellationToken);
        if (!result.IsValid)
            return BadRequest(new ApiErrorResponse
            {
                Message = "Validation failed.",
                ErrorCode = "VALIDATION_ERROR",
                Errors = result.ToErrorDictionary(),
                TraceId = HttpContext.GetCorrelationId(),
            });

        return Ok(await _fees.SetAsync(className, request, cancellationToken));
    }

    /// <summary>Removes the fee of a class: it then has no suggested fee.</summary>
    [HttpDelete("{className}")]
    [Authorize(Policy = Permissions.SettingsEdit)]
    [ProducesResponseType(StatusCodes.Status204NoContent)]
    [ProducesResponseType(typeof(ApiErrorResponse), StatusCodes.Status404NotFound)]
    public async Task<IActionResult> Clear(string className, CancellationToken cancellationToken)
    {
        await _fees.ClearAsync(className, cancellationToken);
        return NoContent();
    }
}
