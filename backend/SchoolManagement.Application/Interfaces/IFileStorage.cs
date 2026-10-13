namespace SchoolManagement.Application.Interfaces;

/// <summary>
/// G1b: where the bytes of uploaded documents live. The database keeps only the metadata and the
/// storage key. Development and a single server use a local folder (LocalFileStorage); moving to
/// Azure Blob / S3 later means another implementation of this interface, with no schema change.
/// </summary>
public interface IFileStorage
{
    /// <summary>
    /// Stores <paramref name="content"/> (read from its current position to the end) under a new,
    /// random key and returns that key. The user's file name is never part of the key.
    /// </summary>
    /// <param name="extension">".pdf", ".jpg" or ".png", from the detected content type.</param>
    Task<string> SaveAsync(Stream content, string extension, CancellationToken cancellationToken);

    /// <summary>Opens a stored file for reading, or returns null when it is not there (purged or lost).</summary>
    Task<Stream?> OpenReadAsync(string key, CancellationToken cancellationToken);

    /// <summary>Removes a stored file. Does nothing when it is already gone.</summary>
    Task DeleteAsync(string key, CancellationToken cancellationToken);
}
