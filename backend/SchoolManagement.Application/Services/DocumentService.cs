using System.Globalization;
using System.Security.Cryptography;
using Microsoft.Extensions.Logging;
using SchoolManagement.Application.DTOs.Documents;
using SchoolManagement.Application.Interfaces;
using SchoolManagement.Application.Settings;
using SchoolManagement.Domain.Constants;
using SchoolManagement.Domain.Entities;
using SchoolManagement.Domain.Exceptions;

namespace SchoolManagement.Application.Services;

/// <summary>
/// G1b: upload, review, download and delete of student and applicant documents.
///
/// Upload checks, in order: the owner exists (and an application is not already enrolled), the type
/// is offered, the expiry date is there when the type needs one, the size is within
/// Documents:MaxFileSizeBytes, the CONTENT is a PDF, JPG or PNG, and the same file is not already
/// attached to the same owner. Only then is the file stored and the row inserted.
///
/// Review: Pending -> Verified or Rejected (with a reason); Verified -> Pending (unverify).
/// Nobody reviews their own upload. A verified document must be unverified before it is deleted.
/// The database enforces the same rules (DocumentConfiguration); the checks here give clear messages.
/// </summary>
public class DocumentService : IDocumentService
{
    private const int PurgeBatchSize = 100;

    private readonly IDocumentRepository _documents;
    private readonly IFileStorage _storage;
    private readonly ICurrentUser _currentUser;
    private readonly ICurrentUserPermissions _permissions;
    private readonly ISensitiveReadLog _readLog;
    private readonly IConcurrencyGuard _concurrency;
    private readonly DocumentSettings _settings;
    private readonly TimeProvider _time;
    private readonly ILogger<DocumentService> _logger;

    public DocumentService(
        IDocumentRepository documents,
        IFileStorage storage,
        ICurrentUser currentUser,
        ICurrentUserPermissions permissions,
        ISensitiveReadLog readLog,
        IConcurrencyGuard concurrency,
        DocumentSettings settings,
        TimeProvider time,
        ILogger<DocumentService> logger)
    {
        _documents = documents;
        _storage = storage;
        _currentUser = currentUser;
        _permissions = permissions;
        _readLog = readLog;
        _concurrency = concurrency;
        _settings = settings;
        _time = time;
        _logger = logger;
    }

    private DateTime UtcNow => _time.GetUtcNow().UtcDateTime;

    // Same convention as the rest of the API: "today" is the UTC date.
    private DateOnly Today => DateOnly.FromDateTime(UtcNow);

    // ---------------------------------------------------------------- reads

    public async Task<DocumentOptionsResponse> GetOptionsAsync(CancellationToken cancellationToken)
    {
        var types = await _documents.GetTypesAsync(cancellationToken);
        return new DocumentOptionsResponse(
            types
                .Where(t => t.IsActive && t.ForStudents)
                .OrderBy(t => t.SortOrder).ThenBy(t => t.Name)
                .Select(t => new DocumentTypeResponse(t.Id, t.Code, t.Name, t.IsSensitive, t.HasExpiry))
                .ToList(),
            _settings.MaxFileSizeBytes,
            _settings.ExpiringSoonDays);
    }

    public async Task<List<DocumentResponse>> ListForStudentAsync(int studentId, CancellationToken cancellationToken)
    {
        if (!await _documents.StudentExistsAsync(studentId, cancellationToken))
            throw new NotFoundException($"Student {studentId} was not found.");

        return await ToResponsesAsync(
            await _documents.ListAsync(new DocumentFilter(StudentId: studentId), cancellationToken), cancellationToken);
    }

    public async Task<List<DocumentResponse>> ListForAdmissionAsync(int admissionId, CancellationToken cancellationToken)
    {
        if (await _documents.GetAdmissionStatusAsync(admissionId, cancellationToken) is null)
            throw new NotFoundException($"Application {admissionId} was not found.");

        return await ToResponsesAsync(
            await _documents.ListAsync(new DocumentFilter(AdmissionId: admissionId), cancellationToken), cancellationToken);
    }

    public async Task<List<DocumentResponse>> ListAsync(DocumentListQuery query, CancellationToken cancellationToken)
    {
        var errors = new Dictionary<string, string[]>();
        if (query.Status is not null && !DocumentStatuses.IsValid(query.Status))
            errors["Status"] = new[] { $"Status must be one of: {string.Join(", ", DocumentStatuses.All)}." };
        if (query.Expiry is not null && !DocumentExpiryStates.All.Contains(query.Expiry))
            errors["Expiry"] = new[] { $"Expiry must be one of: {string.Join(", ", DocumentExpiryStates.All)}." };
        if (errors.Count > 0)
            throw new RequestValidationException(errors);

        var today = Today;
        var filter = query.Expiry switch
        {
            DocumentExpiryStates.Expired => new DocumentFilter(ExpiresOnOrBefore: today.AddDays(-1)),
            DocumentExpiryStates.ExpiringSoon => new DocumentFilter(
                ExpiresOnOrAfter: today, ExpiresOnOrBefore: today.AddDays(_settings.ExpiringSoonDays)),
            _ => new DocumentFilter(),
        };
        filter = filter with { Status = query.Status, DocumentTypeId = query.DocumentTypeId };

        return await ToResponsesAsync(await _documents.ListAsync(filter, cancellationToken), cancellationToken);
    }

    // ---------------------------------------------------------------- upload

    public async Task<DocumentResponse> UploadForStudentAsync(
        int studentId, UploadDocumentRequest request, DocumentUpload file, CancellationToken cancellationToken)
    {
        if (!await _documents.StudentExistsAsync(studentId, cancellationToken))
            throw new NotFoundException($"Student {studentId} was not found.");

        return await UploadAsync(new Owner(studentId, null, "student"), request, file, cancellationToken);
    }

    public async Task<DocumentResponse> UploadForAdmissionAsync(
        int admissionId, UploadDocumentRequest request, DocumentUpload file, CancellationToken cancellationToken)
    {
        var status = await _documents.GetAdmissionStatusAsync(admissionId, cancellationToken)
            ?? throw new NotFoundException($"Application {admissionId} was not found.");

        // Enrolment moved the application's documents to the student; new ones belong there too.
        if (status == AdmissionStatuses.Enrolled)
            throw new ConflictException(
                "This application is enrolled: its documents have moved to the student. Upload on the student's page.");

        return await UploadAsync(new Owner(null, admissionId, "application"), request, file, cancellationToken);
    }

    private sealed record Owner(int? StudentId, int? AdmissionId, string Noun);

    private async Task<DocumentResponse> UploadAsync(
        Owner owner, UploadDocumentRequest request, DocumentUpload file, CancellationToken cancellationToken)
    {
        var errors = new Dictionary<string, string[]>();

        var type = await _documents.GetTypeAsync(request.DocumentTypeId, cancellationToken);
        if (type is null || !type.IsActive || !type.ForStudents)
            errors["DocumentTypeId"] = new[] { "Choose a document type from the list." };
        else if (type.HasExpiry && request.ExpiryDate is null)
            errors["ExpiryDate"] = new[] { $"{type.Name} needs an expiry date." };

        // The size comes first and needs no reading: an oversized file is refused before its content is touched.
        if (file.Length <= 0)
            errors["File"] = new[] { "The file is empty." };
        else if (file.Length > _settings.MaxFileSizeBytes)
            errors["File"] = new[] { FileSignatures.TooLargeMessage(file.Length, _settings.MaxFileSizeBytes) };

        if (errors.Count > 0)
            throw new RequestValidationException(errors);

        var content = file.Content;
        if (!content.CanSeek)
            throw new InvalidOperationException("The upload stream must be seekable (ASP.NET buffers uploads).");

        // What the file really is, from its first bytes.
        content.Position = 0;
        var header = new byte[FileSignatures.HeaderLength];
        var read = await content.ReadAtLeastAsync(header, header.Length, throwOnEndOfStream: false, cancellationToken);
        var contentType = FileSignatures.Detect(header.AsSpan(0, read));
        if (contentType is null)
            throw new RequestValidationException(new Dictionary<string, string[]>
            {
                ["File"] = new[]
                {
                    "Only PDF, JPG and PNG files can be uploaded, and this file's content is none of them. " +
                    "(The content is checked, not the name: renaming a file does not change what it is.)",
                },
            });

        content.Position = 0;
        var sha256 = await SHA256.HashDataAsync(content, cancellationToken);

        var duplicate = await _documents.FindDuplicateAsync(owner.StudentId, owner.AdmissionId, sha256, cancellationToken);
        if (duplicate is not null)
            throw new ConflictException(DuplicateMessage(owner.Noun, duplicate.DocumentTypeName, duplicate.Status, duplicate.UploadedAt));

        content.Position = 0;
        var storageKey = await _storage.SaveAsync(content, DocumentContentTypes.Extensions[contentType], cancellationToken);

        var document = new Document
        {
            StudentId = owner.StudentId,
            AdmissionId = owner.AdmissionId,
            DocumentTypeId = type!.Id,
            Title = string.IsNullOrWhiteSpace(request.Title) ? null : request.Title.Trim(),
            OriginalFileName = CleanFileName(file.FileName),
            ContentType = contentType,
            SizeBytes = checked((int)file.Length),
            Sha256 = sha256,
            StorageKey = storageKey,
            // A date sent for a type without expiry is ignored rather than stored.
            ExpiryDate = type.HasExpiry ? request.ExpiryDate : null,
            Status = DocumentStatuses.Pending,
        };

        try
        {
            await _documents.AddAsync(document, cancellationToken);
        }
        catch
        {
            // No row points at the stored file: remove it, so a failed upload leaves nothing behind.
            await DeleteStoredFileQuietlyAsync(storageKey);
            throw;
        }

        _logger.LogInformation(
            "Document {DocumentId} uploaded ({ContentType}, {SizeBytes} bytes) for {OwnerNoun} {OwnerId}",
            document.Id, contentType, document.SizeBytes, owner.Noun, owner.StudentId ?? owner.AdmissionId);

        return await GetResponseAsync(document.Id, cancellationToken);
    }

    /// <summary>The 409 message for a file that is already attached to the same owner.</summary>
    public static string DuplicateMessage(string ownerNoun, string typeName, string status, DateTime uploadedAt) =>
        $"This file is already attached to this {ownerNoun} as \"{typeName}\" " +
        $"({status.ToLowerInvariant()}, uploaded {uploadedAt.ToString("d MMM yyyy", CultureInfo.InvariantCulture)}). The same file is not stored twice.";

    // ---------------------------------------------------------------- download

    public async Task<DocumentFile> DownloadAsync(int id, CancellationToken cancellationToken)
    {
        var row = await _documents.GetRowAsync(id, cancellationToken)
            ?? throw new NotFoundException($"Document {id} was not found.");

        if (row.IsSensitive && !await _permissions.HasPermissionAsync(Permissions.StudentsViewSensitive, cancellationToken))
            throw new ForbiddenException(
                "This is an identity or medical document. Downloading it also needs the Students.ViewSensitive permission.");

        var document = await _documents.GetByIdAsync(id, cancellationToken)
            ?? throw new NotFoundException($"Document {id} was not found.");

        await using var stored = await _storage.OpenReadAsync(document.StorageKey, cancellationToken);
        if (stored is null)
        {
            _logger.LogError("Document {DocumentId}: file {StorageKey} is missing from storage", id, document.StorageKey);
            throw new BusinessRuleException(
                "The file of this document is missing from storage. Ask an administrator to restore it from the backup.");
        }

        // Files are small (a few MB), so they are read whole: the hash is checked BEFORE anything is sent.
        var buffer = new MemoryStream();
        await stored.CopyToAsync(buffer, cancellationToken);
        if (!SHA256.HashData(buffer.GetBuffer().AsSpan(0, (int)buffer.Length)).AsSpan().SequenceEqual(document.Sha256))
        {
            _logger.LogError("Document {DocumentId}: file {StorageKey} does not match its SHA-256", id, document.StorageKey);
            throw new BusinessRuleException(
                "The stored file of this document is damaged. Ask an administrator to restore it from the backup.");
        }

        // Every download leaves a Read row in the audit log (who opened which document, never the content).
        await _readLog.RecordAsync("Documents", id, new[] { row.IsSensitive ? "File (sensitive)" : "File" }, cancellationToken);

        buffer.Position = 0;
        return new DocumentFile(buffer, document.ContentType, FileSignatures.DownloadName(document.OriginalFileName, document.ContentType));
    }

    // ---------------------------------------------------------------- review

    public async Task<DocumentResponse> VerifyAsync(int id, ReviewDocumentRequest request, CancellationToken cancellationToken)
    {
        var document = await GetForChangeAsync(id, request.RowVersion, cancellationToken);
        RequireStatus(document, DocumentStatuses.Pending, "verified");
        RefuseSelfReview(document);

        document.Status = DocumentStatuses.Verified;
        document.VerifiedBy = _currentUser.UserId;
        document.VerifiedAt = UtcNow;
        await _documents.SaveChangesAsync(cancellationToken);

        return await GetResponseAsync(id, cancellationToken);
    }

    public async Task<DocumentResponse> RejectAsync(int id, RejectDocumentRequest request, CancellationToken cancellationToken)
    {
        var document = await GetForChangeAsync(id, request.RowVersion, cancellationToken);
        RequireStatus(document, DocumentStatuses.Pending, "rejected");
        RefuseSelfReview(document);

        document.Status = DocumentStatuses.Rejected;
        document.VerifiedBy = _currentUser.UserId;
        document.VerifiedAt = UtcNow;
        document.RejectionReason = request.Reason.Trim();
        await _documents.SaveChangesAsync(cancellationToken);

        return await GetResponseAsync(id, cancellationToken);
    }

    /// <summary>
    /// Verified -> Pending. Its own audit row (an Update with Status Verified -> Pending and the reviewer
    /// cleared) records who took the verification back and when. A rejected document is not reopened:
    /// it is replaced by a new upload (decision 5).
    /// </summary>
    public async Task<DocumentResponse> UnverifyAsync(int id, ReviewDocumentRequest request, CancellationToken cancellationToken)
    {
        var document = await GetForChangeAsync(id, request.RowVersion, cancellationToken);
        RequireStatus(document, DocumentStatuses.Verified, "unverified");

        document.Status = DocumentStatuses.Pending;
        document.VerifiedBy = null;
        document.VerifiedAt = null;
        await _documents.SaveChangesAsync(cancellationToken);

        return await GetResponseAsync(id, cancellationToken);
    }

    public async Task DeleteAsync(int id, CancellationToken cancellationToken)
    {
        var document = await _documents.GetByIdAsync(id, cancellationToken)
            ?? throw new NotFoundException($"Document {id} was not found.");

        // Decision 6: a verified document is evidence someone checked; taking that back is a deliberate step.
        if (document.Status == DocumentStatuses.Verified)
            throw new ConflictException("This document is verified. Unverify it first, then delete it.");

        document.IsDeleted = true;
        document.DeletedAt = UtcNow;
        await _documents.SaveChangesAsync(cancellationToken);
    }

    // ---------------------------------------------------------------- purge

    public async Task<int> PurgeDeletedFilesAsync(CancellationToken cancellationToken)
    {
        var deletedBefore = UtcNow.AddDays(-_settings.DeletedFileRetentionDays);
        var purged = 0;

        while (true)
        {
            var batch = await _documents.GetPurgeBatchAsync(deletedBefore, PurgeBatchSize, cancellationToken);
            if (batch.Count == 0) break;

            foreach (var document in batch)
            {
                await _storage.DeleteAsync(document.StorageKey, cancellationToken);
                document.FilePurgedAt = UtcNow;
            }

            await _documents.SaveChangesAsync(cancellationToken);
            purged += batch.Count;
            if (batch.Count < PurgeBatchSize) break;
        }

        return purged;
    }

    // ---------------------------------------------------------------- helpers

    private async Task<Document> GetForChangeAsync(int id, string rowVersion, CancellationToken cancellationToken)
    {
        var document = await _documents.GetByIdAsync(id, cancellationToken)
            ?? throw new NotFoundException($"Document {id} was not found.");
        _concurrency.Expect(document, rowVersion); // 409 if someone reviewed it after this user loaded it
        return document;
    }

    private static void RequireStatus(Document document, string required, string verb)
    {
        if (document.Status != required)
            throw new ConflictException(
                $"Only a {required.ToLowerInvariant()} document can be {verb}; this one is {document.Status.ToLowerInvariant()}.");
    }

    private void RefuseSelfReview(Document document)
    {
        if (document.CreatedBy is not null && document.CreatedBy == _currentUser.UserId)
            throw new BusinessRuleException("You uploaded this document, so someone else must verify or reject it.");
    }

    private async Task<DocumentResponse> GetResponseAsync(int id, CancellationToken cancellationToken)
    {
        var row = await _documents.GetRowAsync(id, cancellationToken)
            ?? throw new NotFoundException($"Document {id} was not found.");
        return (await ToResponsesAsync(new List<DocumentRow> { row }, cancellationToken))[0];
    }

    private async Task<List<DocumentResponse>> ToResponsesAsync(List<DocumentRow> rows, CancellationToken cancellationToken)
    {
        var canSeeSensitive = await _permissions.HasPermissionAsync(Permissions.StudentsViewSensitive, cancellationToken);
        var userId = _currentUser.UserId;
        var today = Today;

        return rows.Select(r => new DocumentResponse(
            Id: r.Id,
            OwnerType: r.StudentId is not null ? DocumentOwnerTypes.Student : DocumentOwnerTypes.Admission,
            OwnerId: r.StudentId ?? r.AdmissionId ?? 0,
            OwnerName: r.OwnerName,
            OwnerNumber: r.OwnerNumber,
            DocumentTypeId: r.DocumentTypeId,
            DocumentTypeName: r.DocumentTypeName,
            IsSensitive: r.IsSensitive,
            HasExpiry: r.HasExpiry,
            Title: r.Title,
            OriginalFileName: r.OriginalFileName,
            ContentType: r.ContentType,
            SizeBytes: r.SizeBytes,
            ExpiryDate: r.ExpiryDate,
            ExpiryState: ExpiryStateOf(r.ExpiryDate, today, _settings.ExpiringSoonDays),
            Status: r.Status,
            UploadedBy: r.UploadedBy,
            UploadedAt: r.CreatedAt,
            ReviewedBy: r.ReviewedBy,
            ReviewedAt: r.VerifiedAt,
            RejectionReason: r.RejectionReason,
            IsOwnUpload: userId is not null && r.CreatedBy == userId,
            CanDownload: !r.IsSensitive || canSeeSensitive,
            RowVersion: Convert.ToBase64String(r.RowVersion))).ToList();
    }

    /// <summary>"Expired" before today, "ExpiringSoon" from today up to <paramref name="soonDays"/> ahead, else null.</summary>
    public static string? ExpiryStateOf(DateOnly? expiry, DateOnly today, int soonDays) =>
        expiry is null ? null
        : expiry < today ? DocumentExpiryStates.Expired
        : expiry <= today.AddDays(soonDays) ? DocumentExpiryStates.ExpiringSoon
        : null;

    /// <summary>
    /// The name to show: no folder part (some browsers send "C:\fakepath\x.pdf"), no control characters,
    /// at most 255 characters with the extension kept.
    /// </summary>
    public static string CleanFileName(string? fileName)
    {
        var name = (fileName ?? string.Empty).Replace('\\', '/');
        name = name[(name.LastIndexOf('/') + 1)..];
        name = new string(name.Where(c => !char.IsControl(c)).ToArray()).Trim();
        if (name.Length == 0) return "document";
        if (name.Length <= 255) return name;

        var extension = Path.GetExtension(name);
        if (extension.Length > 10) extension = string.Empty;
        return name[..(255 - extension.Length)] + extension;
    }

    private async Task DeleteStoredFileQuietlyAsync(string storageKey)
    {
        try
        {
            await _storage.DeleteAsync(storageKey, CancellationToken.None);
        }
        catch (Exception ex)
        {
            _logger.LogWarning(ex, "Could not remove stored file {StorageKey} after a failed upload", storageKey);
        }
    }
}
