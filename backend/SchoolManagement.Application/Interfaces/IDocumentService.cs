using SchoolManagement.Application.DTOs.Documents;

namespace SchoolManagement.Application.Interfaces;

public interface IDocumentService
{
    /// <summary>The active types that can be attached to students and applicants, and the size limit.</summary>
    Task<DocumentOptionsResponse> GetOptionsAsync(CancellationToken cancellationToken);

    Task<List<DocumentResponse>> ListForStudentAsync(int studentId, CancellationToken cancellationToken);
    Task<List<DocumentResponse>> ListForAdmissionAsync(int admissionId, CancellationToken cancellationToken);

    /// <summary>The Documents page: every student's and applicant's documents, filtered.</summary>
    Task<List<DocumentResponse>> ListAsync(DocumentListQuery query, CancellationToken cancellationToken);

    Task<DocumentResponse> UploadForStudentAsync(
        int studentId, UploadDocumentRequest request, DocumentUpload file, CancellationToken cancellationToken);
    Task<DocumentResponse> UploadForAdmissionAsync(
        int admissionId, UploadDocumentRequest request, DocumentUpload file, CancellationToken cancellationToken);

    /// <summary>The file, after the permission check and the integrity check. Writes a Read audit row.</summary>
    Task<DocumentFile> DownloadAsync(int id, CancellationToken cancellationToken);

    Task<DocumentResponse> VerifyAsync(int id, ReviewDocumentRequest request, CancellationToken cancellationToken);
    Task<DocumentResponse> RejectAsync(int id, RejectDocumentRequest request, CancellationToken cancellationToken);
    Task<DocumentResponse> UnverifyAsync(int id, ReviewDocumentRequest request, CancellationToken cancellationToken);

    /// <summary>Soft delete. A verified document must be unverified first. The file is kept until the purge.</summary>
    Task DeleteAsync(int id, CancellationToken cancellationToken);

    /// <summary>
    /// Removes the files of documents deleted more than Documents:DeletedFileRetentionDays ago.
    /// Returns how many files were removed. Called once a day by the purge job.
    /// </summary>
    Task<int> PurgeDeletedFilesAsync(CancellationToken cancellationToken);
}
