using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using FluentValidation;
using SchoolManagement.API.Common;
using SchoolManagement.API.Extensions;
using SchoolManagement.API.Filters;
using SchoolManagement.Application.DTOs.Documents;
using SchoolManagement.Application.DTOs.Students;
using SchoolManagement.Application.DTOs.Parents;
using SchoolManagement.Application.Interfaces;
using SchoolManagement.Domain.Constants;

namespace SchoolManagement.API.Controllers;

[ApiController]
[Route("api/students")]
[Authorize]
public class StudentsController : ControllerBase
{
    private readonly IStudentService _studentService;
    private readonly IStudentGuardianService _guardianService;
    private readonly IStudentPhotoService _photoService;
    private readonly IValidator<CreateStudentRequest> _createValidator;
    private readonly IValidator<UpdateStudentRequest> _updateValidator;
    private readonly IValidator<UpdateStudentIdentityRequest> _identityValidator;
    private readonly IValidator<LinkGuardianRequest> _linkValidator;
    private readonly ICurrentUserPermissions _permissions;
    private readonly IDataScopeResolver _scopes;

    public StudentsController(
        IStudentService studentService,
        IStudentGuardianService guardianService,
        IStudentPhotoService photoService,
        IValidator<CreateStudentRequest> createValidator,
        IValidator<UpdateStudentRequest> updateValidator,
        IValidator<UpdateStudentIdentityRequest> identityValidator,
        IValidator<LinkGuardianRequest> linkValidator,
        ICurrentUserPermissions permissions,
        IDataScopeResolver scopes)
    {
        _studentService = studentService;
        _guardianService = guardianService;
        _photoService = photoService;
        _createValidator = createValidator;
        _updateValidator = updateValidator;
        _identityValidator = identityValidator;
        _linkValidator = linkValidator;
        _permissions = permissions;
        _scopes = scopes;
    }

    // F1: reads accept Students.View (everything, full shape) or Students.ViewScoped (related students
    // only, reduced shape). Out-of-scope ids answer 404 exactly like ids that do not exist.
    private Task<DataScope> StudentScopeAsync(CancellationToken cancellationToken) =>
        _scopes.ResolveAsync(Permissions.StudentsView, Permissions.StudentsViewScoped, cancellationToken);

    // Full Aadhaar / passport numbers are returned only to callers holding this permission;
    // everyone else gets masked values. The service never decides this itself.
    // Read from the permission cache: the JWT carries no permissions (D1).
    private Task<bool> CanViewSensitiveAsync(CancellationToken cancellationToken) =>
        _permissions.HasPermissionAsync(Permissions.StudentsViewSensitive, cancellationToken);

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

    /// <summary>Students.View: every student (StudentSummaryResponse). Students.ViewScoped: related students (StudentScopedSummaryResponse).</summary>
    [HttpGet]
    [Authorize(Policy = CompositePolicies.StudentsRead)]
    [ProducesResponseType(typeof(List<StudentSummaryResponse>), StatusCodes.Status200OK)]
    public async Task<IActionResult> GetAll(CancellationToken cancellationToken)
    {
        var scope = await StudentScopeAsync(cancellationToken);
        return scope.IsUnrestricted
            ? Ok(await _studentService.GetAllAsync(await CanViewSensitiveAsync(cancellationToken), cancellationToken))
            : Ok(await _studentService.GetAllScopedAsync(scope, cancellationToken));
    }

    /// <summary>Students.View: StudentResponse. Students.ViewScoped: StudentScopedResponse. Both leave a sensitive-read audit row.</summary>
    [HttpGet("{id:int}")]
    [Authorize(Policy = CompositePolicies.StudentsRead)]
    [ProducesResponseType(typeof(StudentResponse), StatusCodes.Status200OK)]
    [ProducesResponseType(typeof(ApiErrorResponse), StatusCodes.Status404NotFound)]
    public async Task<IActionResult> GetById(int id, CancellationToken cancellationToken)
    {
        var scope = await StudentScopeAsync(cancellationToken);
        return scope.IsUnrestricted
            ? Ok(await _studentService.GetByIdAsync(id, await CanViewSensitiveAsync(cancellationToken), cancellationToken))
            : Ok(await _studentService.GetByIdScopedAsync(id, scope, cancellationToken));
    }

    [HttpPost]
    [Authorize(Policy = Permissions.StudentsCreate)]
    public async Task<IActionResult> Create(CreateStudentRequest request, CancellationToken cancellationToken)
    {
        var validationError = await ValidateAsync(_createValidator, request, cancellationToken);
        if (validationError is not null) return validationError;

        var result = await _studentService.CreateAsync(request, await CanViewSensitiveAsync(cancellationToken), cancellationToken);
        return Created($"api/students/{result.Id}", result);
    }

    [HttpPut("{id:int}")]
    [Authorize(Policy = Permissions.StudentsEdit)]
    public async Task<IActionResult> Update(int id, UpdateStudentRequest request, CancellationToken cancellationToken)
    {
        var validationError = await ValidateAsync(_updateValidator, request, cancellationToken);
        if (validationError is not null) return validationError;

        return Ok(await _studentService.UpdateAsync(id, request, await CanViewSensitiveAsync(cancellationToken), cancellationToken));
    }

    /// <summary>Set Aadhaar / passport / visa details. Requires both Students.Edit and Students.ViewSensitive.</summary>
    [HttpPut("{id:int}/identity")]
    [Authorize(Policy = Permissions.StudentsEdit)]
    [Authorize(Policy = Permissions.StudentsViewSensitive)]
    public async Task<IActionResult> UpdateIdentity(int id, UpdateStudentIdentityRequest request, CancellationToken cancellationToken)
    {
        var validationError = await ValidateAsync(_identityValidator, request, cancellationToken);
        if (validationError is not null) return validationError;

        return Ok(await _studentService.UpdateIdentityAsync(id, request, cancellationToken));
    }

    /// <summary>The student's academic history, newest period first. Read-only.</summary>
    [HttpGet("{id:int}/enrollments")]
    [Authorize(Policy = CompositePolicies.StudentsRead)]
    [ProducesResponseType(typeof(List<StudentEnrollmentResponse>), StatusCodes.Status200OK)]
    [ProducesResponseType(typeof(ApiErrorResponse), StatusCodes.Status404NotFound)]
    public async Task<IActionResult> GetEnrollments(int id, CancellationToken cancellationToken) =>
        Ok(await _studentService.GetEnrollmentsAsync(id, await StudentScopeAsync(cancellationToken), cancellationToken));

    /// <summary>Students.View: StudentGuardianResponse. Students.ViewScoped: GuardianContactResponse (a parent sees only their own link).</summary>
    [HttpGet("{id:int}/guardians")]
    [Authorize(Policy = CompositePolicies.StudentsRead)]
    [ProducesResponseType(typeof(List<StudentGuardianResponse>), StatusCodes.Status200OK)]
    [ProducesResponseType(typeof(ApiErrorResponse), StatusCodes.Status404NotFound)]
    public async Task<IActionResult> GetGuardians(int id, CancellationToken cancellationToken)
    {
        var scope = await StudentScopeAsync(cancellationToken);
        return scope.IsUnrestricted
            ? Ok(await _guardianService.GetForStudentAsync(id, cancellationToken))
            : Ok(await _guardianService.GetContactsForStudentAsync(id, scope, cancellationToken));
    }

    [HttpPost("{id:int}/guardians")]
    [Authorize(Policy = Permissions.ParentsEdit)]
    public async Task<IActionResult> LinkGuardian(int id, LinkGuardianRequest request, CancellationToken cancellationToken)
    {
        var validationError = await ValidateAsync(_linkValidator, request, cancellationToken);
        if (validationError is not null) return validationError;

        var result = await _guardianService.LinkAsync(id, request, cancellationToken);
        return Created($"api/students/{id}/guardians/{result.ParentId}", result);
    }

    [HttpDelete("{id:int}/guardians/{parentId:int}")]
    [Authorize(Policy = Permissions.ParentsEdit)]
    public async Task<IActionResult> UnlinkGuardian(int id, int parentId, CancellationToken cancellationToken)
    {
        await _guardianService.UnlinkAsync(id, parentId, cancellationToken);
        return NoContent();
    }

    /// <summary>The form of a photo upload (multipart/form-data, one file).</summary>
    public sealed class StudentPhotoForm
    {
        public IFormFile? File { get; set; }
    }

    /// <summary>The picture, streamed through the API so the usual permissions and data scope apply.</summary>
    [HttpGet("{id:int}/photo")]
    [Authorize(Policy = CompositePolicies.StudentsRead)]
    [ProducesResponseType(StatusCodes.Status200OK)]
    [ProducesResponseType(typeof(ApiErrorResponse), StatusCodes.Status404NotFound)]
    public async Task<IActionResult> GetPhoto(int id, CancellationToken cancellationToken)
    {
        var photo = await _photoService.GetAsync(id, await StudentScopeAsync(cancellationToken), cancellationToken);
        Response.Headers["X-Content-Type-Options"] = "nosniff";
        Response.Headers.CacheControl = "private, no-cache";
        return File(photo.Content, photo.ContentType);
    }

    /// <summary>Uploads or replaces the photo (JPG or PNG, 2 MB). Needs Students.Edit.</summary>
    [HttpPut("{id:int}/photo")]
    [Authorize(Policy = Permissions.StudentsEdit)]
    [DocumentUploadLimit]
    public async Task<IActionResult> SetPhoto(int id, [FromForm] StudentPhotoForm form, CancellationToken cancellationToken)
    {
        if (form.File is null)
            return BadRequest(new ApiErrorResponse
            {
                Message = "Validation failed.",
                ErrorCode = "VALIDATION_ERROR",
                Errors = new Dictionary<string, string[]> { ["file"] = new[] { "Choose a photo to upload." } },
                TraceId = HttpContext.GetCorrelationId(),
            });

        await using var content = form.File.OpenReadStream();
        await _photoService.SetAsync(id, new DocumentUpload(form.File.FileName, form.File.Length, content), cancellationToken);
        return NoContent();
    }

    /// <summary>Removes the photo. Needs Students.Edit.</summary>
    [HttpDelete("{id:int}/photo")]
    [Authorize(Policy = Permissions.StudentsEdit)]
    public async Task<IActionResult> RemovePhoto(int id, CancellationToken cancellationToken)
    {
        await _photoService.RemoveAsync(id, cancellationToken);
        return NoContent();
    }

    /// <summary>Marks the student as Left: closes the class period and frees the seat. Needs Students.Edit.</summary>
    [HttpPost("{id:int}/leave")]
    [Authorize(Policy = Permissions.StudentsEdit)]
    public async Task<IActionResult> MarkLeft(int id, CancellationToken cancellationToken)
    {
        await _studentService.MarkLeftAsync(id, cancellationToken);
        return NoContent();
    }

    /// <summary>Deleted students, with who deleted each and when. Needs Students.Delete and the Admin role.</summary>
    [HttpGet("deleted")]
    [Authorize(Policy = Permissions.StudentsDelete)]
    public async Task<IActionResult> GetDeleted(CancellationToken cancellationToken) =>
        Ok(await _studentService.GetDeletedAsync(cancellationToken));

    /// <summary>Brings a deleted student back. Needs Students.Delete and the Admin role.</summary>
    [HttpPost("{id:int}/restore")]
    [Authorize(Policy = Permissions.StudentsDelete)]
    public async Task<IActionResult> Restore(int id, CancellationToken cancellationToken)
    {
        await _studentService.RestoreAsync(id, cancellationToken);
        return NoContent();
    }

    [HttpDelete("{id:int}")]
    [Authorize(Policy = Permissions.StudentsDelete)]
    public async Task<IActionResult> Delete(int id, CancellationToken cancellationToken)
    {
        await _studentService.DeleteAsync(id, cancellationToken);
        return NoContent();
    }
}