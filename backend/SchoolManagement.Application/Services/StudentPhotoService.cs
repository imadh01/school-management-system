using SchoolManagement.Application.DTOs.Documents;
using SchoolManagement.Application.Interfaces;
using SchoolManagement.Domain.Entities;
using SchoolManagement.Domain.Exceptions;

namespace SchoolManagement.Application.Services;

public class StudentPhotoService : IStudentPhotoService
{
    /// <summary>A picture does not need to be big; 2 MB is generous for a phone photo.</summary>
    public const long MaxBytes = 2 * 1024 * 1024;

    /// <summary>
    /// What responses carry in "PhotoUrl": the API route that serves the picture, or null when there is
    /// none. The stored file key stays inside the server, and a web address left over from the old
    /// free-text box counts as "no photo".
    /// </summary>
    public static string? RouteFor(int studentId, string? storedKey) =>
        string.IsNullOrEmpty(storedKey) || storedKey.Contains("://", StringComparison.Ordinal)
            ? null
            : $"/api/students/{studentId}/photo";

    private readonly IStudentRepository _students;
    private readonly IFileStorage _storage;

    public StudentPhotoService(IStudentRepository students, IFileStorage storage)
    {
        _students = students;
        _storage = storage;
    }

    public async Task SetAsync(int studentId, DocumentUpload file, CancellationToken cancellationToken)
    {
        var student = await _students.GetByIdAsync(studentId, cancellationToken)
            ?? throw new NotFoundException($"Student {studentId} does not exist.");

        if (file.Length <= 0)
            throw Invalid("The file is empty.");
        if (file.Length > MaxBytes)
            throw Invalid($"The photo is {FileSignatures.DescribeSize(file.Length)}. The largest photo allowed is {FileSignatures.DescribeSize(MaxBytes)}.");

        var content = file.Content;
        if (!content.CanSeek)
            throw new InvalidOperationException("The upload stream must be seekable (ASP.NET buffers uploads).");

        content.Position = 0;
        var header = new byte[FileSignatures.HeaderLength];
        var read = await content.ReadAtLeastAsync(header, header.Length, throwOnEndOfStream: false, cancellationToken);
        var contentType = FileSignatures.Detect(header.AsSpan(0, read));
        if (contentType is not (DocumentContentTypes.Jpeg or DocumentContentTypes.Png))
            throw Invalid("A photo must be a JPG or PNG picture. (The content is checked, not the file name.)");

        content.Position = 0;
        var newKey = await _storage.SaveAsync(content, DocumentContentTypes.Extensions[contentType], cancellationToken);
        var oldKey = student.PhotoUrl;
        student.PhotoUrl = newKey;

        try
        {
            await _students.SaveChangesAsync(cancellationToken);
        }
        catch
        {
            await _storage.DeleteAsync(newKey, CancellationToken.None);   // do not leave an orphan file
            throw;
        }

        await DeleteOldAsync(oldKey);
    }

    public async Task RemoveAsync(int studentId, CancellationToken cancellationToken)
    {
        var student = await _students.GetByIdAsync(studentId, cancellationToken)
            ?? throw new NotFoundException($"Student {studentId} does not exist.");

        var oldKey = student.PhotoUrl;
        if (string.IsNullOrEmpty(oldKey)) return;

        student.PhotoUrl = null;
        await _students.SaveChangesAsync(cancellationToken);
        await DeleteOldAsync(oldKey);
    }

    public async Task<StudentPhotoFile> GetAsync(int studentId, DataScope scope, CancellationToken cancellationToken)
    {
        if (await _students.GetAccessAsync(studentId, scope, cancellationToken) == StudentAccess.None)
            throw new NotFoundException($"Student {studentId} does not exist.");

        var student = await _students.GetByIdAsync(studentId, cancellationToken)
            ?? throw new NotFoundException($"Student {studentId} does not exist.");

        // Older rows may still hold a web address typed into the old "Photo URL" box: not a stored file.
        var key = student.PhotoUrl;
        if (string.IsNullOrEmpty(key) || key.Contains("://", StringComparison.Ordinal))
            throw new NotFoundException("This student has no photo.");

        var stored = await _storage.OpenReadAsync(key, cancellationToken)
            ?? throw new NotFoundException("This student has no photo.");

        var buffer = new MemoryStream();
        await using (stored)
            await stored.CopyToAsync(buffer, cancellationToken);
        buffer.Position = 0;

        var contentType = key.EndsWith(".png", StringComparison.OrdinalIgnoreCase)
            ? DocumentContentTypes.Png
            : DocumentContentTypes.Jpeg;
        return new StudentPhotoFile(buffer, contentType);
    }

    private async Task DeleteOldAsync(string? key)
    {
        if (string.IsNullOrEmpty(key) || key.Contains("://", StringComparison.Ordinal)) return;
        try { await _storage.DeleteAsync(key, CancellationToken.None); }
        catch { /* a leftover file is harmless; the periodic purge and backups cover it */ }
    }

    private static RequestValidationException Invalid(string message) =>
        new(new Dictionary<string, string[]> { ["File"] = new[] { message } });
}
