namespace SchoolManagement.Application.DTOs.Documents;

/// <summary>A document type offered in the upload form.</summary>
public record DocumentTypeResponse(int Id, string Code, string Name, bool IsSensitive, bool HasExpiry);

/// <summary>
/// What the upload form needs: the types, and the size limit from the settings, so the browser can
/// refuse a too-big file with the exact numbers before sending it (the API checks again).
/// </summary>
public record DocumentOptionsResponse(List<DocumentTypeResponse> Types, long MaxFileSizeBytes, int ExpiringSoonDays);

/// <summary>
/// The form fields that come with an uploaded file (multipart/form-data). The file itself is not
/// part of this record: the controller hands it to the service as a <see cref="DocumentUpload"/>.
/// </summary>
public class UploadDocumentRequest
{
    public int DocumentTypeId { get; set; }
    public string? Title { get; set; }
    /// <summary>Required for types with an expiry (passport, visa, Emirates ID).</summary>
    public DateOnly? ExpiryDate { get; set; }
}

/// <summary>
/// The uploaded file as the service sees it: no ASP.NET types, so the Application layer stays
/// independent of the web framework. <see cref="Content"/> must be seekable (ASP.NET buffers uploads).
/// </summary>
public sealed record DocumentUpload(string FileName, long Length, Stream Content);

/// <summary>Verify and unverify carry the version the user saw, so two reviewers cannot overwrite each other.</summary>
public record ReviewDocumentRequest(string RowVersion);

public record RejectDocumentRequest(string Reason, string RowVersion);

/// <summary>Filters of the Documents page. All optional.</summary>
public record DocumentListQuery(string? Status, int? DocumentTypeId, string? Expiry);

/// <summary>Values of <see cref="DocumentResponse.ExpiryState"/> and of the "expiry" filter.</summary>
public static class DocumentExpiryStates
{
    public const string Expired = "Expired";
    public const string ExpiringSoon = "ExpiringSoon";

    public static readonly string[] All = { Expired, ExpiringSoon };
}

/// <summary>Who a document belongs to.</summary>
public static class DocumentOwnerTypes
{
    public const string Student = "Student";
    public const string Admission = "Admission";
}

public record DocumentResponse(
    int Id,
    // "Student" or "Admission".
    string OwnerType,
    int OwnerId,
    string OwnerName,
    // The student's admission number or the application's registration number.
    string OwnerNumber,
    int DocumentTypeId,
    string DocumentTypeName,
    bool IsSensitive,
    bool HasExpiry,
    string? Title,
    string OriginalFileName,
    string ContentType,
    int SizeBytes,
    DateOnly? ExpiryDate,
    // "Expired", "ExpiringSoon" or null.
    string? ExpiryState,
    string Status,
    string? UploadedBy,
    DateTime UploadedAt,
    string? ReviewedBy,
    DateTime? ReviewedAt,
    string? RejectionReason,
    // True when the caller uploaded it: the UI hides Verify/Reject (the API refuses them anyway).
    bool IsOwnUpload,
    // False for a sensitive file when the caller lacks Students.ViewSensitive.
    bool CanDownload,
    string RowVersion);

/// <summary>A stored file ready to send to the browser.</summary>
public sealed record DocumentFile(Stream Content, string ContentType, string DownloadName);
