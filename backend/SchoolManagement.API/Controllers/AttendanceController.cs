using FluentValidation;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using SchoolManagement.API.Common;
using SchoolManagement.API.Extensions;
using SchoolManagement.Application.DTOs.Attendance;
using SchoolManagement.Application.Interfaces;
using SchoolManagement.Domain.Constants;
using SchoolManagement.Domain.Exceptions;

namespace SchoolManagement.API.Controllers;

[ApiController]
[Authorize]
[Route("api/attendance")]
public class AttendanceController : ControllerBase
{
    private readonly IAttendanceService _attendanceService;
    private readonly IValidator<SaveAttendanceRequest> _saveValidator;
    private readonly ICurrentUserPermissions _permissions;
    private readonly IDataScopeResolver _scopes;

    public AttendanceController(
        IAttendanceService attendanceService,
        IValidator<SaveAttendanceRequest> saveValidator,
        ICurrentUserPermissions permissions,
        IDataScopeResolver scopes)
    {
        _attendanceService = attendanceService;
        _saveValidator = saveValidator;
        _permissions = permissions;
        _scopes = scopes;
    }

    // F1: reads accept Attendance.View (everything) or Attendance.ViewScoped (own classes as class
    // teacher, own subjects, own children). Out of scope = 404, like a class/student that does not exist.
    private Task<DataScope> AttendanceScopeAsync(CancellationToken ct) =>
        _scopes.ResolveAsync(Permissions.AttendanceView, Permissions.AttendanceViewScoped, ct);

    /// <summary>
    /// The roster for a class on a date (optionally one subject): students, saved statuses,
    /// last-7 dots, and whether the caller may edit it.
    /// </summary>
    [HttpGet("roster")]
    [Authorize(Policy = CompositePolicies.AttendanceRead)]
    [ProducesResponseType(typeof(AttendanceRosterResponse), StatusCodes.Status200OK)]
    [ProducesResponseType(typeof(ApiErrorResponse), StatusCodes.Status404NotFound)]
    [ProducesResponseType(typeof(ApiErrorResponse), StatusCodes.Status422UnprocessableEntity)]
    public async Task<IActionResult> GetRoster(
        [FromQuery] int classSectionId,
        [FromQuery] DateOnly date,
        [FromQuery] int? subjectId,
        CancellationToken ct) =>
        Ok(await _attendanceService.GetRosterAsync(
            await GetActorAsync(ct), await AttendanceScopeAsync(ct), classSectionId, date, subjectId, ct));

    /// <summary>Creates or replaces the whole roster in one transaction.</summary>
    [HttpPut]
    [Authorize(Policy = CompositePolicies.AttendanceWrite)]
    [ProducesResponseType(typeof(AttendanceRosterResponse), StatusCodes.Status200OK)]
    [ProducesResponseType(typeof(ApiErrorResponse), StatusCodes.Status400BadRequest)]
    [ProducesResponseType(typeof(ApiErrorResponse), StatusCodes.Status403Forbidden)]
    [ProducesResponseType(typeof(ApiErrorResponse), StatusCodes.Status404NotFound)]
    [ProducesResponseType(typeof(ApiErrorResponse), StatusCodes.Status409Conflict)]
    [ProducesResponseType(typeof(ApiErrorResponse), StatusCodes.Status422UnprocessableEntity)]
    public async Task<IActionResult> Save(SaveAttendanceRequest request, CancellationToken ct)
    {
        var result = await _saveValidator.ValidateAsync(request, ct);
        if (!result.IsValid) return ValidationFailed(result);

        return Ok(await _attendanceService.SaveAsync(await GetActorAsync(ct), request, ct));
    }

    /// <summary>One student's attendance history and summary (defaults to the last 30 days).</summary>
    [HttpGet("students/{studentId:int}")]
    [Authorize(Policy = CompositePolicies.AttendanceRead)]
    [ProducesResponseType(typeof(StudentAttendanceResponse), StatusCodes.Status200OK)]
    [ProducesResponseType(typeof(ApiErrorResponse), StatusCodes.Status404NotFound)]
    [ProducesResponseType(typeof(ApiErrorResponse), StatusCodes.Status422UnprocessableEntity)]
    public async Task<IActionResult> GetStudentHistory(
        int studentId, [FromQuery] DateOnly? from, [FromQuery] DateOnly? to, CancellationToken ct) =>
        Ok(await _attendanceService.GetStudentHistoryAsync(await AttendanceScopeAsync(ct), studentId, from, to, ct));

    /// <summary>Per-student totals and percentages for a class over a date range (daily attendance only).</summary>
    [HttpGet("summary")]
    [Authorize(Policy = CompositePolicies.AttendanceRead)]
    [ProducesResponseType(typeof(ClassAttendanceSummaryResponse), StatusCodes.Status200OK)]
    [ProducesResponseType(typeof(ApiErrorResponse), StatusCodes.Status404NotFound)]
    [ProducesResponseType(typeof(ApiErrorResponse), StatusCodes.Status422UnprocessableEntity)]
    public async Task<IActionResult> GetSummary(
        [FromQuery] int classSectionId, [FromQuery] DateOnly from, [FromQuery] DateOnly to, CancellationToken ct) =>
        Ok(await _attendanceService.GetSummaryAsync(await AttendanceScopeAsync(ct), classSectionId, from, to, ct));

    // Builds the caller description the service needs; the service never touches HttpContext.
    // Permissions and roles come from the permission cache: the JWT carries neither (D1).
    private async Task<AttendanceActor> GetActorAsync(CancellationToken ct)
    {
        var idValue = User.FindFirst(System.Security.Claims.ClaimTypes.NameIdentifier)?.Value
                      ?? User.FindFirst("sub")?.Value;
        if (!int.TryParse(idValue, out var userId))
            throw new ForbiddenException("Your session is invalid. Please sign in again.");

        return new AttendanceActor(
            userId,
            CanManageAll: await _permissions.HasPermissionAsync(Permissions.AttendanceManage, ct),
            IsAdmin: await _permissions.IsInRoleAsync(RoleNames.Admin, ct));
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
