using SchoolManagement.Application.DTOs.Documents;

namespace SchoolManagement.Application.Interfaces;

/// <summary>A stored photo, ready to stream to the browser.</summary>
public sealed record StudentPhotoFile(Stream Content, string ContentType);

/// <summary>
/// The student's profile picture. Not a "document": it is not reviewed or verified, it just replaces
/// the previous picture. The file lives in the same storage as documents and is only ever served
/// through the API (never as a public URL), so the usual permissions and data scope apply.
/// </summary>
public interface IStudentPhotoService
{
    /// <summary>Replaces the photo. JPG or PNG only (decided from the file's bytes), at most 2 MB.</summary>
    Task SetAsync(int studentId, DocumentUpload file, CancellationToken cancellationToken);

    Task RemoveAsync(int studentId, CancellationToken cancellationToken);

    /// <summary>The photo of a student the caller may see. 404 when the student is out of scope or has no photo.</summary>
    Task<StudentPhotoFile> GetAsync(int studentId, DataScope scope, CancellationToken cancellationToken);
}
