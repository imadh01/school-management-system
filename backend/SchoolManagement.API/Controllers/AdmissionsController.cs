using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using FluentValidation;
using SchoolManagement.API.Common;
using SchoolManagement.API.Extensions;
using SchoolManagement.Application.DTOs.Admissions;
using SchoolManagement.Application.Interfaces;
using SchoolManagement.Domain.Constants;
using SchoolManagement.Domain.Exceptions;

namespace SchoolManagement.API.Controllers;

[ApiController]
[Route("api/admissions")]
[Authorize]
public class AdmissionsController : ControllerBase
{
    private readonly IAdmissionService _admissionService;
    private readonly IAdmissionFeeService _admissionFees;
    private readonly ICurrentUserPermissions _permissions;
    private readonly IValidator<CreateAdmissionRequest> _createValidator;
    private readonly IValidator<UpdateAdmissionRequest> _updateValidator;
    private readonly IValidator<ConfirmAdmissionRequest> _confirmValidator;
    private readonly IValidator<EnrollAdmissionRequest> _enrollValidator;
    private readonly IValidator<RejectAdmissionRequest> _rejectValidator;

    public AdmissionsController(
        IAdmissionService admissionService,
        IAdmissionFeeService admissionFees,
        ICurrentUserPermissions permissions,
        IValidator<CreateAdmissionRequest> createValidator,
        IValidator<UpdateAdmissionRequest> updateValidator,
        IValidator<ConfirmAdmissionRequest> confirmValidator,
        IValidator<EnrollAdmissionRequest> enrollValidator,
        IValidator<RejectAdmissionRequest> rejectValidator)
    {
        _admissionService = admissionService;
        _admissionFees = admissionFees;
        _permissions = permissions;
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

    /// <summary>G2: naming an existing parent on an application needs Parents.View (the search shows their children).</summary>
    private Task<bool> CanLinkParentsAsync(CancellationToken cancellationToken) =>
        _permissions.HasPermissionAsync(Permissions.ParentsView, cancellationToken);

    /// <summary>
    /// G2: "Find an existing parent" on the registration form. Both permissions are needed: Parents.View (the answer
    /// includes the parents' mobile numbers and their children) and the right to register or edit applications.
    /// </summary>
    [HttpGet("parent-search")]
    [Authorize(Policy = Permissions.ParentsView)]
    public async Task<IActionResult> SearchParents(
        [FromQuery] string? q, [FromQuery] int take, CancellationToken cancellationToken)
    {
        if (!await _permissions.HasPermissionAsync(Permissions.AdmissionsCreate, cancellationToken) &&
            !await _permissions.HasPermissionAsync(Permissions.AdmissionsEdit, cancellationToken))
            throw new ForbiddenException("Searching parents needs permission to register or edit applications.");

        return Ok(await _admissionService.SearchParentsAsync(q, take == 0 ? 8 : take, cancellationToken));
    }

    [HttpGet]
    [Authorize(Policy = Permissions.AdmissionsView)]
    public async Task<IActionResult> GetAll(CancellationToken cancellationToken) =>
        Ok(await _admissionService.GetAllAsync(cancellationToken));

    /// <summary>The standard admission fee of this application's class (from Masters), to pre-fill Confirm. Needs Admissions.Edit.</summary>
    [HttpGet("{id:int}/suggested-fee")]
    [Authorize(Policy = Permissions.AdmissionsEdit)]
    public async Task<IActionResult> GetSuggestedFee(int id, CancellationToken cancellationToken) =>
        Ok(await _admissionFees.GetSuggestedForAdmissionAsync(id, cancellationToken));

    /// <summary>Deleted applications, with who deleted each and when. Needs Admissions.Delete.</summary>
    [HttpGet("deleted")]
    [Authorize(Policy = Permissions.AdmissionsDelete)]
    public async Task<IActionResult> GetDeleted(CancellationToken cancellationToken) =>
        Ok(await _admissionService.GetDeletedAsync(cancellationToken));

    /// <summary>Brings a deleted application back. Needs Admissions.Delete and the Admin role.</summary>
    [HttpPost("{id:int}/restore")]
    [Authorize(Policy = Permissions.AdmissionsDelete)]
    public async Task<IActionResult> Restore(int id, CancellationToken cancellationToken) =>
        Ok(await _admissionService.RestoreAsync(id, cancellationToken));

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

        var result = await _admissionService.CreateAsync(request, cancellationToken, await CanLinkParentsAsync(cancellationToken));
        return Created($"api/admissions/{result.Id}", result);
    }

    [HttpPut("{id:int}")]
    [Authorize(Policy = Permissions.AdmissionsEdit)]
    public async Task<IActionResult> Update(int id, UpdateAdmissionRequest request, CancellationToken cancellationToken)
    {
        var validationError = await ValidateAsync(_updateValidator, request, cancellationToken);
        if (validationError is not null) return validationError;

        return Ok(await _admissionService.UpdateAsync(id, request, cancellationToken, await CanLinkParentsAsync(cancellationToken)));
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
