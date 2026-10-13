using System.Text.RegularExpressions;
using SchoolManagement.Application.Interfaces;

namespace SchoolManagement.Infrastructure.Storage;

/// <summary>
/// G1b: keeps uploaded files in a folder on this server, outside the web root, as
/// "yyyy/MM/{random}.ext". The key is generated here and never contains anything the user typed,
/// so a file name like "..\..\web.config" cannot reach the disk.
///
/// A file is written to a temporary name first and renamed when complete, so a crash or a
/// cancelled upload never leaves a half-written file under a real key.
/// </summary>
public sealed partial class LocalFileStorage : IFileStorage
{
    private readonly string _root;

    /// <param name="root">Absolute folder (resolved from Documents:StorageRoot in Program.cs).</param>
    public LocalFileStorage(string root)
    {
        if (!Path.IsPathFullyQualified(root))
            throw new ArgumentException("The documents storage root must be an absolute path.", nameof(root));
        _root = Path.TrimEndingDirectorySeparator(Path.GetFullPath(root));
    }

    public async Task<string> SaveAsync(Stream content, string extension, CancellationToken cancellationToken)
    {
        if (!ExtensionPattern().IsMatch(extension))
            throw new ArgumentException($"Unexpected extension '{extension}'.", nameof(extension));

        var now = DateTime.UtcNow;
        var key = $"{now:yyyy}/{now:MM}/{Guid.NewGuid():N}{extension}";
        var path = PathOf(key);
        Directory.CreateDirectory(Path.GetDirectoryName(path)!);

        var temporary = path + ".uploading";
        try
        {
            await using (var file = new FileStream(temporary, FileMode.CreateNew, FileAccess.Write, FileShare.None,
                             bufferSize: 81920, useAsync: true))
            {
                await content.CopyToAsync(file, cancellationToken);
                await file.FlushAsync(cancellationToken);
            }
            File.Move(temporary, path);
        }
        catch
        {
            TryDelete(temporary);
            throw;
        }

        return key;
    }

    public Task<Stream?> OpenReadAsync(string key, CancellationToken cancellationToken)
    {
        var path = PathOf(key);
        Stream? stream = File.Exists(path)
            ? new FileStream(path, FileMode.Open, FileAccess.Read, FileShare.Read, bufferSize: 81920, useAsync: true)
            : null;
        return Task.FromResult(stream);
    }

    public Task DeleteAsync(string key, CancellationToken cancellationToken)
    {
        var path = PathOf(key);
        if (File.Exists(path))
            File.Delete(path);
        return Task.CompletedTask;
    }

    /// <summary>The full path of a key, refusing anything that is not a key this class made.</summary>
    private string PathOf(string key)
    {
        if (!KeyPattern().IsMatch(key))
            throw new ArgumentException($"'{key}' is not a valid storage key.", nameof(key));

        var path = Path.GetFullPath(Path.Combine(_root, key.Replace('/', Path.DirectorySeparatorChar)));
        // Belt and braces: the pattern already rules out "..", but never leave the root.
        if (!path.StartsWith(_root + Path.DirectorySeparatorChar, StringComparison.Ordinal))
            throw new ArgumentException($"'{key}' points outside the storage folder.", nameof(key));
        return path;
    }

    private static void TryDelete(string path)
    {
        try { File.Delete(path); }
        catch (IOException) { /* best effort */ }
        catch (UnauthorizedAccessException) { /* best effort */ }
    }

    [GeneratedRegex(@"^\.(pdf|jpg|png)$")]
    private static partial Regex ExtensionPattern();

    [GeneratedRegex(@"^\d{4}/\d{2}/[0-9a-f]{32}\.(pdf|jpg|png)$")]
    private static partial Regex KeyPattern();
}
