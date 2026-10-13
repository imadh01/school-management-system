using SchoolManagement.Domain.Entities;

namespace SchoolManagement.Application.Interfaces;

/// <summary>
/// One document as lists show it: the row plus the names they need (type, owner, uploader,
/// reviewer), read in one query. Never the file.
/// </summary>
public sealed record DocumentRow(
    int Id,
    int? StudentId,
    int? AdmissionId,
    string OwnerName,
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
    string Status,
    int? CreatedBy,
    string? UploadedBy,
    DateTime CreatedAt,
    string? ReviewedBy,
    DateTime? VerifiedAt,
    string? RejectionReason,
    byte[] RowVersion);

/// <summary>Filters for <see cref="IDocumentRepository.ListAsync"/>. Null = no filter.</summary>
public sealed record DocumentFilter(
    int? StudentId = null,
    int? AdmissionId = null,
    string? Status = null,
    int? DocumentTypeId = null,
    // Only documents whose expiry date is on or before this day.
    DateOnly? ExpiresOnOrBefore = null,
    // Only documents whose expiry date is on or after this day.
    DateOnly? ExpiresOnOrAfter = null);

/// <summary>An existing document with the same content, for the "already uploaded" message.</summary>
public sealed record DuplicateDocument(int Id, string DocumentTypeName, string Status, DateTime UploadedAt);

public interface IDocumentRepository
{
    Task<List<DocumentType>> GetTypesAsync(CancellationToken cancellationToken);
    Task<DocumentType?> GetTypeAsync(int id, CancellationToken cancellationToken);

    /// <summary>A not-deleted document with its type, tracked (for review and delete).</summary>
    Task<Document?> GetByIdAsync(int id, CancellationToken cancellationToken);

    Task<DocumentRow?> GetRowAsync(int id, CancellationToken cancellationToken);
    Task<List<DocumentRow>> ListAsync(DocumentFilter filter, CancellationToken cancellationToken);

    Task<bool> StudentExistsAsync(int studentId, CancellationToken cancellationToken);
    /// <summary>The application's status, or null when it does not exist (or is deleted).</summary>
    Task<string?> GetAdmissionStatusAsync(int admissionId, CancellationToken cancellationToken);

    /// <summary>A not-deleted document of the same owner with the same SHA-256, if any.</summary>
    Task<DuplicateDocument?> FindDuplicateAsync(int? studentId, int? admissionId, byte[] sha256, CancellationToken cancellationToken);

    /// <summary>
    /// Inserts the document. When the database refuses it as a duplicate (another request saved the
    /// same file a moment earlier) this throws ConflictException.
    /// </summary>
    Task AddAsync(Document document, CancellationToken cancellationToken);

    /// <summary>The application's not-deleted documents, tracked (enrolment moves them to the student).</summary>
    Task<List<Document>> GetForAdmissionAsync(int admissionId, CancellationToken cancellationToken);

    /// <summary>
    /// Deleted documents whose file is still stored and that were deleted before <paramref name="deletedBefore"/>,
    /// oldest first, tracked. Used by the purge job.
    /// </summary>
    Task<List<Document>> GetPurgeBatchAsync(DateTime deletedBefore, int take, CancellationToken cancellationToken);

    Task SaveChangesAsync(CancellationToken cancellationToken);
}
