using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using FluentValidation;
using SchoolManagement.API.Common;
using SchoolManagement.API.Extensions;
using SchoolManagement.Application.DTOs.Admissions;
using SchoolManagement.Application.DTOs.Students;
using SchoolManagement.Application.Interfaces;

namespace SchoolManagement.API.Controllers;

[ApiController]
[Route("api/admissions")]
public class AdmissionsController : ControllerBase
{
    private readonly IAdmissionService _admissionService;
    private readonly IStudentService _studentService;
    private readonly IValidator<CreateAdmissionRequest> _createValidator;
    private readonly IValidator<UpdateAdmissionRequest> _updateValidator;
    private readonly IValidator<ConfirmAdmissionRequest> _confirmValidator;
    private readonly IValidator<EnrollAdmissionRequest> _enrollValidator;
    private readonly IValidator<RejectAdmissionRequest> _rejectValidator;
    private readonly IValidator<CreateStudentFromAdmissionRequest> _createStudentValidator;

    public AdmissionsController(
        IAdmissionService admissionService,
        IStudentService studentService,
        IValidator<CreateAdmissionRequest> createValidator,
        IValidator<UpdateAdmissionRequest> updateValidator,
        IValidator<ConfirmAdmissionRequest> confirmValidator,
        IValidator<EnrollAdmissionRequest> enrollValidator,
        IValidator<RejectAdmissionRequest> rejectValidator,
        IValidator<CreateStudentFromAdmissionRequest> createStudentValidator)
    {
        _admissionService = admissionService;
        _studentService = studentService;
        _createValidator = createValidator;
        _updateValidator = updateValidator;
        _confirmValidator = confirmValidator;
        _enrollValidator = enrollValidator;
        _rejectValidator = rejectValidator;
        _createStudentValidator = createStudentValidator;
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
    public async Task<IActionResult> GetAll(CancellationToken cancellationToken) =>
        Ok(await _admissionService.GetAllAsync(cancellationToken));

    [HttpGet("{id:int}")]
    public async Task<IActionResult> GetById(int id, CancellationToken cancellationToken) =>
        Ok(await _admissionService.GetByIdAsync(id, cancellationToken));

    [HttpPost]
    [Authorize(Policy = "Admissions.Manage")]
    public async Task<IActionResult> Create(CreateAdmissionRequest request, CancellationToken cancellationToken)
    {
        var validationError = await ValidateAsync(_createValidator, request, cancellationToken);
        if (validationError is not null) return validationError;

        var result = await _admissionService.CreateAsync(request, cancellationToken);
        return Created($"api/admissions/{result.Id}", result);
    }

    [HttpPut("{id:int}")]
    [Authorize(Policy = "Admissions.Manage")]
    public async Task<IActionResult> Update(int id, UpdateAdmissionRequest request, CancellationToken cancellationToken)
    {
        var validationError = await ValidateAsync(_updateValidator, request, cancellationToken);
        if (validationError is not null) return validationError;

        return Ok(await _admissionService.UpdateAsync(id, request, cancellationToken));
    }

    [HttpPost("{id:int}/confirm-admission")]
    [Authorize(Policy = "Admissions.Manage")]
    public async Task<IActionResult> ConfirmAdmission(int id, ConfirmAdmissionRequest request, CancellationToken cancellationToken)
    {
        var validationError = await ValidateAsync(_confirmValidator, request, cancellationToken);
        if (validationError is not null) return validationError;

        return Ok(await _admissionService.ConfirmAdmissionAsync(id, request, cancellationToken));
    }

    [HttpPost("{id:int}/enroll")]
    [Authorize(Policy = "Admissions.Manage")]
    public async Task<IActionResult> Enroll(int id, EnrollAdmissionRequest request, CancellationToken cancellationToken)
    {
        var validationError = await ValidateAsync(_enrollValidator, request, cancellationToken);
        if (validationError is not null) return validationError;

        return Ok(await _admissionService.EnrollAsync(id, request, cancellationToken));
    }

    [HttpPost("{id:int}/reject")]
    [Authorize(Policy = "Admissions.Manage")]
    public async Task<IActionResult> Reject(int id, RejectAdmissionRequest request, CancellationToken cancellationToken)
    {
        var validationError = await ValidateAsync(_rejectValidator, request, cancellationToken);
        if (validationError is not null) return validationError;

        return Ok(await _admissionService.RejectAsync(id, request, cancellationToken));
    }

    [HttpPost("{id:int}/create-student")]
    [Authorize(Policy = "Students.Manage")]
    public async Task<IActionResult> CreateStudent(int id, CreateStudentFromAdmissionRequest request, CancellationToken cancellationToken)
    {
        var validationError = await ValidateAsync(_createStudentValidator, request, cancellationToken);
        if (validationError is not null) return validationError;

        var result = await _studentService.CreateFromAdmissionAsync(id, request, cancellationToken);
        return Created($"api/students/{result.Id}", result);
    }

    [HttpDelete("{id:int}")]
    [Authorize(Policy = "Admissions.Manage")]
    public async Task<IActionResult> Delete(int id, CancellationToken cancellationToken)
    {
        await _admissionService.DeleteAsync(id, cancellationToken);
        return NoContent();
    }
}