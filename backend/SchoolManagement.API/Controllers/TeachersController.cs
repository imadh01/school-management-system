using FluentValidation;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using SchoolManagement.API.Common;
using SchoolManagement.API.Extensions;
using SchoolManagement.Application.DTOs.Teachers;
using SchoolManagement.Application.Interfaces;
using SchoolManagement.Domain.Constants;

namespace SchoolManagement.API.Controllers;

[ApiController]
[Authorize]
[Route("api/teachers")]
public class TeachersController : ControllerBase
{
    private readonly ITeacherService _teacherService;
    private readonly IValidator<CreateTeacherRequest> _createValidator;
    private readonly IValidator<UpdateTeacherRequest> _updateValidator;
    private readonly IValidator<ChangeTeacherStatusRequest> _statusValidator;
    private readonly IValidator<AssignSubjectsRequest> _assignValidator;
    private readonly IValidator<SetClassTeacherRequest> _classTeacherValidator;

    public TeachersController(
        ITeacherService teacherService,
        IValidator<CreateTeacherRequest> createValidator,
        IValidator<UpdateTeacherRequest> updateValidator,
        IValidator<ChangeTeacherStatusRequest> statusValidator,
        IValidator<AssignSubjectsRequest> assignValidator,
        IValidator<SetClassTeacherRequest> classTeacherValidator)
    {
        _teacherService = teacherService;
        _createValidator = createValidator;
        _updateValidator = updateValidator;
        _statusValidator = statusValidator;
        _assignValidator = assignValidator;
        _classTeacherValidator = classTeacherValidator;
    }

    [HttpGet]
    [Authorize(Policy = Permissions.TeachersView)]
    [ProducesResponseType(typeof(List<TeacherResponse>), StatusCodes.Status200OK)]
    public async Task<IActionResult> GetAll(CancellationToken ct) =>
        Ok(await _teacherService.GetAllAsync(ct));

    [HttpGet("{id:int}")]
    [Authorize(Policy = Permissions.TeachersView)]
    [ProducesResponseType(typeof(TeacherResponse), StatusCodes.Status200OK)]
    [ProducesResponseType(typeof(ApiErrorResponse), StatusCodes.Status404NotFound)]
    public async Task<IActionResult> GetById(int id, CancellationToken ct) =>
        Ok(await _teacherService.GetByIdAsync(id, ct));

    [HttpPost]
    [Authorize(Policy = Permissions.TeachersCreate)]
    [ProducesResponseType(typeof(TeacherResponse), StatusCodes.Status201Created)]
    [ProducesResponseType(typeof(ApiErrorResponse), StatusCodes.Status400BadRequest)]
    [ProducesResponseType(typeof(ApiErrorResponse), StatusCodes.Status409Conflict)]
    public async Task<IActionResult> Create(CreateTeacherRequest request, CancellationToken ct)
    {
        var result = await _createValidator.ValidateAsync(request, ct);
        if (!result.IsValid) return ValidationFailed(result);

        var created = await _teacherService.CreateAsync(request, ct);
        return Created($"api/teachers/{created.Id}", created);
    }

    [HttpPut("{id:int}")]
    [Authorize(Policy = Permissions.TeachersEdit)]
    [ProducesResponseType(typeof(TeacherResponse), StatusCodes.Status200OK)]
    [ProducesResponseType(typeof(ApiErrorResponse), StatusCodes.Status400BadRequest)]
    [ProducesResponseType(typeof(ApiErrorResponse), StatusCodes.Status404NotFound)]
    [ProducesResponseType(typeof(ApiErrorResponse), StatusCodes.Status409Conflict)]
    public async Task<IActionResult> Update(int id, UpdateTeacherRequest request, CancellationToken ct)
    {
        var result = await _updateValidator.ValidateAsync(request, ct);
        if (!result.IsValid) return ValidationFailed(result);

        return Ok(await _teacherService.UpdateAsync(id, request, ct));
    }

    [HttpPatch("{id:int}/status")]
    [Authorize(Policy = Permissions.TeachersEdit)]
    [ProducesResponseType(typeof(TeacherResponse), StatusCodes.Status200OK)]
    [ProducesResponseType(typeof(ApiErrorResponse), StatusCodes.Status400BadRequest)]
    [ProducesResponseType(typeof(ApiErrorResponse), StatusCodes.Status404NotFound)]
    public async Task<IActionResult> ChangeStatus(int id, ChangeTeacherStatusRequest request, CancellationToken ct)
    {
        var result = await _statusValidator.ValidateAsync(request, ct);
        if (!result.IsValid) return ValidationFailed(result);

        return Ok(await _teacherService.ChangeStatusAsync(id, request.Status, ct));
    }

    [HttpDelete("{id:int}")]
    [Authorize(Policy = Permissions.TeachersDelete)]
    [ProducesResponseType(StatusCodes.Status204NoContent)]
    [ProducesResponseType(typeof(ApiErrorResponse), StatusCodes.Status404NotFound)]
    [ProducesResponseType(typeof(ApiErrorResponse), StatusCodes.Status409Conflict)]
    public async Task<IActionResult> Delete(int id, CancellationToken ct)
    {
        await _teacherService.DeleteAsync(id, ct);
        return NoContent();
    }

    // ---------- Assignments ----------

    [HttpGet("{id:int}/assignments")]
    [Authorize(Policy = Permissions.TeachersView)]
    [ProducesResponseType(typeof(List<TeacherAssignmentResponse>), StatusCodes.Status200OK)]
    [ProducesResponseType(typeof(ApiErrorResponse), StatusCodes.Status404NotFound)]
    public async Task<IActionResult> GetAssignments(int id, CancellationToken ct) =>
        Ok(await _teacherService.GetAssignmentsAsync(id, ct));

    [HttpPost("{id:int}/subjects")]
    [Authorize(Policy = Permissions.TeachersAssign)]
    [ProducesResponseType(typeof(List<TeacherAssignmentResponse>), StatusCodes.Status200OK)]
    [ProducesResponseType(typeof(ApiErrorResponse), StatusCodes.Status400BadRequest)]
    [ProducesResponseType(typeof(ApiErrorResponse), StatusCodes.Status404NotFound)]
    [ProducesResponseType(typeof(ApiErrorResponse), StatusCodes.Status409Conflict)]
    public async Task<IActionResult> AssignSubjects(int id, AssignSubjectsRequest request, CancellationToken ct)
    {
        var result = await _assignValidator.ValidateAsync(request, ct);
        if (!result.IsValid) return ValidationFailed(result);

        return Ok(await _teacherService.AssignSubjectsAsync(id, request, ct));
    }

    [HttpDelete("{id:int}/subjects/{subjectId:int}")]
    [Authorize(Policy = Permissions.TeachersAssign)]
    [ProducesResponseType(typeof(List<TeacherAssignmentResponse>), StatusCodes.Status200OK)]
    [ProducesResponseType(typeof(ApiErrorResponse), StatusCodes.Status404NotFound)]
    public async Task<IActionResult> UnassignSubject(int id, int subjectId, CancellationToken ct) =>
        Ok(await _teacherService.UnassignSubjectAsync(id, subjectId, ct));

    [HttpPut("{id:int}/class-teacher")]
    [Authorize(Policy = Permissions.TeachersAssign)]
    [ProducesResponseType(typeof(List<TeacherAssignmentResponse>), StatusCodes.Status200OK)]
    [ProducesResponseType(typeof(ApiErrorResponse), StatusCodes.Status400BadRequest)]
    [ProducesResponseType(typeof(ApiErrorResponse), StatusCodes.Status404NotFound)]
    [ProducesResponseType(typeof(ApiErrorResponse), StatusCodes.Status409Conflict)]
    public async Task<IActionResult> SetClassTeacher(int id, SetClassTeacherRequest request, CancellationToken ct)
    {
        var result = await _classTeacherValidator.ValidateAsync(request, ct);
        if (!result.IsValid) return ValidationFailed(result);

        return Ok(await _teacherService.SetClassTeacherAsync(id, request.ClassSectionId, ct));
    }

    [HttpDelete("{id:int}/class-teacher/{classSectionId:int}")]
    [Authorize(Policy = Permissions.TeachersAssign)]
    [ProducesResponseType(typeof(List<TeacherAssignmentResponse>), StatusCodes.Status200OK)]
    [ProducesResponseType(typeof(ApiErrorResponse), StatusCodes.Status404NotFound)]
    [ProducesResponseType(typeof(ApiErrorResponse), StatusCodes.Status409Conflict)]
    public async Task<IActionResult> ClearClassTeacher(int id, int classSectionId, CancellationToken ct) =>
        Ok(await _teacherService.ClearClassTeacherAsync(id, classSectionId, ct));

    private IActionResult ValidationFailed(FluentValidation.Results.ValidationResult result) =>
        BadRequest(new ApiErrorResponse
        {
            Message = "Validation failed.",
            ErrorCode = "VALIDATION_ERROR",
            Errors = result.ToErrorDictionary(),
            TraceId = HttpContext.GetCorrelationId(),
        });
}