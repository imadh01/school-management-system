using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using FluentValidation;
using SchoolManagement.API.Common;
using SchoolManagement.API.Extensions;
using SchoolManagement.Application.DTOs.Admissions;
using SchoolManagement.Application.Interfaces;
using SchoolManagement.Domain.Constants;

namespace SchoolManagement.API.Controllers;

[ApiController]
[Route("api/admissions")]
[Authorize]
public class AdmissionsController : ControllerBase
{
    private readonly IAdmissionService _admissionService;
    private readonly IValidator<CreateAdmissionRequest> _createValidator;
    private readonly IValidator<UpdateAdmissionRequest> _updateValidator;
    private readonly IValidator<ConfirmAdmissionRequest> _confirmValidator;
    private readonly IValidator<EnrollAdmissionRequest> _enrollValidator;
    private readonly IValidator<RejectAdmissionRequest> _rejectValidator;

    public AdmissionsController(
        IAdmissionService admissionService,
        IValidator<CreateAdmissionRequest> createValidator,
        IValidator<UpdateAdmissionRequest> updateValidator,
        IValidator<ConfirmAdmissionRequest> confirmValidator,
        IValidator<EnrollAdmissionRequest> enrollValidator,
        IValidator<RejectAdmissionRequest> rejectValidator)
    {
        _admissionService = admissionService;
        _createValidator = createValidator;
        _updateValidator = updateValidator;
        _confirmValidator = confirmValidator;
        _enrollValidator = enrollValidator;
        _rejectValidator = rejectValidator;
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

    [HttpGet]
    [Authorize(Policy = Permissions.AdmissionsView)]
    public async Task<IActionResult> GetAll(CancellationToken cancellationToken) =>
        Ok(await _admissionService.GetAllAsync(cancellationToken));

    [HttpGet("{id:int}")]
    [Authorize(Policy = Permissions.AdmissionsView)]
    public async Task<IActionResult> GetById(int id, CancellationToken cancellationToken) =>
        Ok(await _admissionService.GetByIdAsync(id, cancellationToken));

    [HttpPost]
    [Authorize(Policy = Permissions.AdmissionsCreate)]
    public async Task<IActionResult> Create(CreateAdmissionRequest request, CancellationToken cancellationToken)
    {
        var validationError = await ValidateAsync(_createValidator, request, cancellationToken);
        if (validationError is not null) return validationError;

        var result = await _admissionService.CreateAsync(request, cancellationToken);
        return Created($"api/admissions/{result.Id}", result);
    }

    [HttpPut("{id:int}")]
    [Authorize(Policy = Permissions.AdmissionsEdit)]
    public async Task<IActionResult> Update(int id, UpdateAdmissionRequest request, CancellationToken cancellationToken)
    {
        var validationError = await ValidateAsync(_updateValidator, request, cancellationToken);
        if (validationError is not null) return validationError;

        return Ok(await _admissionService.UpdateAsync(id, request, cancellationToken));
    }

    [HttpPost("{id:int}/confirm-admission")]
    [Authorize(Policy = Permissions.AdmissionsEdit)]
    public async Task<IActionResult> ConfirmAdmission(int id, ConfirmAdmissionRequest request, CancellationToken cancellationToken)
    {
        var validationError = await ValidateAsync(_confirmValidator, request, cancellationToken);
        if (validationError is not null) return validationError;

        return Ok(await _admissionService.ConfirmAdmissionAsync(id, request, cancellationToken));
    }

    /// <summary>
    /// Existing parents that share each guardian's mobile number, with a suggested action.
    /// Read-only: nothing is linked until the enrol request is sent.
    /// </summary>
    [HttpGet("{id:int}/guardian-matches")]
    [Authorize(Policy = Permissions.AdmissionsEdit)]
    public async Task<IActionResult> GetGuardianMatches(int id, CancellationToken cancellationToken) =>
        Ok(await _admissionService.GetGuardianMatchesAsync(id, cancellationToken));

    [HttpPost("{id:int}/enroll")]
    [Authorize(Policy = Permissions.AdmissionsEnroll)]
    public async Task<IActionResult> Enroll(int id, EnrollAdmissionRequest request, CancellationToken cancellationToken)
    {
        var validationError = await ValidateAsync(_enrollValidator, request, cancellationToken);
        if (validationError is not null) return validationError;

        return Ok(await _admissionService.EnrollAsync(id, request, cancellationToken));
    }

    [HttpPost("{id:int}/reject")]
    [Authorize(Policy = Permissions.AdmissionsEdit)]
    public async Task<IActionResult> Reject(int id, RejectAdmissionRequest request, CancellationToken cancellationToken)
    {
        var validationError = await ValidateAsync(_rejectValidator, request, cancellationToken);
        if (validationError is not null) return validationError;

        return Ok(await _admissionService.RejectAsync(id, request, cancellationToken));
    }

    [HttpDelete("{id:int}")]
    [Authorize(Policy = Permissions.AdmissionsDelete)]
    public async Task<IActionResult> Delete(int id, CancellationToken cancellationToken)
    {
        await _admissionService.DeleteAsync(id, cancellationToken);
        return NoContent();
    }
}