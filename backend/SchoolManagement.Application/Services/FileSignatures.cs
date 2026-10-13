using System.Globalization;
using SchoolManagement.Domain.Entities;

namespace SchoolManagement.Application.Services;

/// <summary>
/// G1b: what a file really is, decided from its first bytes ("magic numbers"), never from its name
/// or the Content-Type the browser sent. Renaming virus.exe to virus.pdf does not change its bytes,
/// so it is refused; a real PNG named scan.pdf is accepted as a PNG (and downloaded as .png).
/// </summary>
public static class FileSignatures
{
    /// <summary>How many bytes <see cref="Detect"/> needs to look at.</summary>
    public const int HeaderLength = 8;

    private static readonly byte[] Pdf = "%PDF-"u8.ToArray();
    private static readonly byte[] Png = { 0x89, 0x50, 0x4E, 0x47, 0x0D, 0x0A, 0x1A, 0x0A };
    private static readonly byte[] Jpeg = { 0xFF, 0xD8, 0xFF };

    /// <summary>The content type of an accepted file (DocumentContentTypes), or null for anything else.</summary>
    public static string? Detect(ReadOnlySpan<byte> header)
    {
        if (header.StartsWith(Pdf)) return DocumentContentTypes.Pdf;
        if (header.StartsWith(Png)) return DocumentContentTypes.Png;
        if (header.StartsWith(Jpeg)) return DocumentContentTypes.Jpeg;
        return null;
    }

    /// <summary>
    /// The download name: the original name with its extension replaced by the real type's, so a PNG
    /// uploaded as "scan.pdf" downloads as "scan.png" and opens in the right program.
    /// </summary>
    public static string DownloadName(string originalFileName, string contentType)
    {
        var baseName = Path.GetFileNameWithoutExtension(originalFileName);
        if (string.IsNullOrWhiteSpace(baseName)) baseName = "document";
        return baseName + DocumentContentTypes.Extensions[contentType];
    }

    /// <summary>"5.31 MB (5,567,201 bytes)": short enough to read, exact enough to compare with the limit.
    /// Under 1 KB just "8 bytes".</summary>
    public static string DescribeSize(long bytes)
    {
        var culture = CultureInfo.InvariantCulture;
        var exact = bytes.ToString("N0", culture) + (bytes == 1 ? " byte" : " bytes");
        if (bytes < 1024) return exact;
        var readable = bytes >= 1024 * 1024
            ? (bytes / 1024d / 1024d).ToString("0.##", culture) + " MB"
            : (bytes / 1024d).ToString("0.##", culture) + " KB";
        return $"{readable} ({exact})";
    }

    /// <summary>The message for a file over the limit, with both numbers.</summary>
    public static string TooLargeMessage(long sizeBytes, long limitBytes) =>
        $"The file is {DescribeSize(sizeBytes)}. The largest file allowed is {DescribeSize(limitBytes)}.";
}
