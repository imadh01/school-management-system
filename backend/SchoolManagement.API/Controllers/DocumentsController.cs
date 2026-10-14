using FluentValidation;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using SchoolManagement.API.Common;
using SchoolManagement.API.Extensions;
using SchoolManagement.API.Filters;
using SchoolManagement.Application.DTOs.Documents;
using SchoolManagement.Application.Interfaces;
using SchoolManagement.Domain.Constants;

namespace SchoolManagement.API.Controllers;

/// <summary>
/// G1b: documents of students and applicants. Uploads are multipart/form-data, one file per request.
/// Files are never served from a folder: every download goes through <see cref="Download"/>, which
/// checks the permissions, checks the file's integrity and writes a Read row to the audit log.
/// </summary>
[ApiController]
[Route("api")]
[Authorize]
public class DocumentsController : ControllerBase
{
    private readonly IDocumentService _documentService;
    private readonly IValidator<UploadDocumentRequest> _uploadValidator;
    private readonly IValidator<ReviewDocumentRequest> _reviewValidator;
    private readonly IValidator<RejectDocumentRequest> _rejectValidator;

    public DocumentsController(
        IDocumentService documentService,
        IValidator<UploadDocumentRequest> uploadValidator,
        IValidator<ReviewDocumentRequest> reviewValidator,
        IValidator<RejectDocumentRequest> rejectValidator)
    {
        _documentService = documentService;
        _uploadValidator = uploadValidator;
        _reviewValidator = reviewValidator;
        _rejectValidator = rejectValidator;
    }

    /// <summary>The form fields of an upload, as the browser sends them (multipart/form-data).</summary>
    public sealed class UploadDocumentForm
    {
        public IFormFile? File { get; set; }
        public int DocumentTypeId { get; set; }
        public string? Title { get; set; }
        public DateOnly? ExpiryDate { get; set; }
    }

    // ---------------------------------------------------------------- reads

    /// <summary>The document types and the size limit, for the upload form.</summary>
    [HttpGet("documents/options")]
    [Authorize(Policy = Permissions.DocumentsView)]
    public async Task<IActionResult> GetOptions(CancellationToken cancellationToken) =>
        Ok(await _documentService.GetOptionsAsync(cancellationToken));

    /// <summary>The Documents page. Filters: status (Pending/Verified/Rejected), documentTypeId, expiry (Expired/ExpiringSoon).</summary>
    [HttpGet("documents")]
    [Authorize(Policy = Permissions.DocumentsView)]
    public async Task<IActionResult> GetAll(
        [FromQuery] string? status, [FromQuery] int? documentTypeId, [FromQuery] string? expiry,
        CancellationToken cancellationToken) =>
        Ok(await _documentService.ListAsync(new DocumentListQuery(status, documentTypeId, expiry), cancellationToken));

    [HttpGet("students/{studentId:int}/documents")]
    [Authorize(Policy = Permissions.DocumentsView)]
    public async Task<IActionResult> GetForStudent(int studentId, CancellationToken cancellationToken) =>
        Ok(await _documentService.ListForStudentAsync(studentId, cancellationToken));

    [HttpGet("admissions/{admissionId:int}/documents")]
    [Authorize(Policy = Permissions.DocumentsView)]
    public async Task<IActionResult> GetForAdmission(int admissionId, CancellationToken cancellationToken) =>
        Ok(await _documentService.ListForAdmissionAsync(admissionId, cancellationToken));

    /// <summary>The file itself. Identity and medical documents also need Students.ViewSensitive.</summary>
    [HttpGet("documents/{id:int}/file")]
    [Authorize(Policy = Permissions.DocumentsView)]
    public async Task<IActionResult> Download(int id, CancellationToken cancellationToken)
    {
        var file = await _documentService.DownloadAsync(id, cancellationToken);

        // Never let the browser guess a different type, and never keep a copy in a shared cache.
        Response.Headers["X-Content-Type-Options"] = "nosniff";
        Response.Headers.CacheControl = "no-store";
        return File(file.Content, file.ContentType, file.DownloadName);
    }

    // ---------------------------------------------------------------- upload

    [HttpPost("students/{studentId:int}/documents")]
    [Authorize(Policy = Permissions.DocumentsUpload)]
    [DocumentUploadLimit]
    public async Task<IActionResult> UploadForStudent(
        int studentId, [FromForm] UploadDocumentForm form, CancellationToken cancellationToken)
    {
        var (request, upload, error) = await ReadUploadAsync(form, cancellationToken);
        if (error is not null) return error;

        await using var content = upload!.Content;
        var result = await _documentService.UploadForStudentAsync(studentId, request!, upload, cancellationToken);
        return Created($"api/documents/{result.Id}/file", result);
    }

    [HttpPost("admissions/{admissionId:int}/documents")]
    [Authorize(Policy = Permissions.DocumentsUpload)]
    [DocumentUploadLimit]
    public async Task<IActionResult> UploadForAdmission(
        int admissionId, [FromForm] UploadDocumentForm form, CancellationToken cancellationToken)
    {
        var (request, upload, error) = await ReadUploadAsync(form, cancellationToken);
        if (error is not null) return error;

        await using var content = upload!.Content;
        var result = await _documentService.UploadForAdmissionAsync(admissionId, request!, upload, cancellationToken);
        return Created($"api/documents/{result.Id}/file", result);
    }

    // ---------------------------------------------------------------- review and delete

    [HttpPost("documents/{id:int}/verify")]
    [Authorize(Policy = Permissions.DocumentsVerify)]
    public async Task<IActionResult> Verify(int id, ReviewDocumentRequest request, CancellationToken cancellationToken)
    {
        var validationError = await ValidateAsync(_reviewValidator, request, cancellationToken);
        if (validationError is not null) return validationError;

        return Ok(await _documentService.VerifyAsync(id, request, cancellationToken));
    }

    [HttpPost("documents/{id:int}/reject")]
    [Authorize(Policy = Permissions.DocumentsVerify)]
    public async Task<IActionResult> Reject(int id, RejectDocumentRequest request, CancellationToken cancellationToken)
    {
        var validationError = await ValidateAsync(_rejectValidator, request, cancellationToken);
        if (validationError is not null) return validationError;

        return Ok(await _documentService.RejectAsync(id, request, cancellationToken));
    }

    [HttpPost("documents/{id:int}/unverify")]
    [Authorize(Policy = Permissions.DocumentsVerify)]
    public async Task<IActionResult> Unverify(int id, ReviewDocumentRequest request, CancellationToken cancellationToken)
    {
        var validationError = await ValidateAsync(_reviewValidator, request, cancellationToken);
        if (validationError is not null) return validationError;

        return Ok(await _documentService.UnverifyAsync(id, request, cancellationToken));
    }

    [HttpDelete("documents/{id:int}")]
    [Authorize(Policy = Permissions.DocumentsDelete)]
    public async Task<IActionResult> Delete(int id, CancellationToken cancellationToken)
    {
        await _documentService.DeleteAsync(id, cancellationToken);
        return NoContent();
    }

    // ---------------------------------------------------------------- helpers

    private async Task<(UploadDocumentRequest? Request, DocumentUpload? Upload, IActionResult? Error)> ReadUploadAsync(
        UploadDocumentForm form, CancellationToken cancellationToken)
    {
        var request = new UploadDocumentRequest
        {
            DocumentTypeId = form.DocumentTypeId,
            Title = form.Title,
            ExpiryDate = form.ExpiryDate,
        };

        var result = await _uploadValidator.ValidateAsync(request, cancellationToken);
        var errors = result.ToErrorDictionary();
        if (form.File is null)
            errors["File"] = new[] { "Choose a file to upload." };

        if (errors.Count > 0)
            return (null, null, InvalidRequest(errors));

        // The browser's Content-Type and the file name are NOT trusted for the type: DocumentService
        // reads the content. The name is only kept as a label.
        var upload = new DocumentUpload(form.File!.FileName, form.File.Length, form.File.OpenReadStream());
        return (request, upload, null);
    }

    private async Task<IActionResult?> ValidateAsync<T>(IValidator<T> validator, T request, CancellationToken cancellationToken)
    {
        var result = await validator.ValidateAsync(request, cancellationToken);
        return result.IsValid ? null : InvalidRequest(result.ToErrorDictionary());
    }

    private IActionResult InvalidRequest(IDictionary<string, string[]> errors) =>
        BadRequest(new ApiErrorResponse
        {
            Message = "Validation failed.",
            ErrorCode = "VALIDATION_ERROR",
            Errors = errors,
            TraceId = HttpContext.GetCorrelationId(),
        });
}
