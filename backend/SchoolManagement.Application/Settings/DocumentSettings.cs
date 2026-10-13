namespace SchoolManagement.Application.Settings;

/// <summary>
/// G1b: bound from the "Documents" section of appsettings.json (or environment variables such as
/// Documents__MaxFileSizeBytes). Changing a value needs a restart of the API, not a release.
/// Checked at startup (Program.cs): a bad value stops the API with a clear message instead of
/// failing on the first upload.
/// </summary>
public class DocumentSettings
{
    public const string SectionName = "Documents";

    /// <summary>
    /// Folder where the uploaded files are kept, OUTSIDE the web root. Empty = "App_Data/documents"
    /// next to the API. Production should point it at a disk that is backed up together with the
    /// database (see docs/deployment.md).
    /// </summary>
    public string StorageRoot { get; set; } = string.Empty;

    /// <summary>Largest file accepted, in bytes. Default 5 MB (5 242 880).</summary>
    public long MaxFileSizeBytes { get; set; } = 5 * 1024 * 1024;

    /// <summary>Days a deleted document's file is kept before the purge job removes it. Default 90.</summary>
    public int DeletedFileRetentionDays { get; set; } = 90;

    /// <summary>A document whose expiry date is within this many days is flagged "expiring soon". Default 60.</summary>
    public int ExpiringSoonDays { get; set; } = 60;

    /// <summary>Turns the daily purge job off (the integration tests do).</summary>
    public bool PurgeEnabled { get; set; } = true;

    /// <summary>The largest value <see cref="MaxFileSizeBytes"/> may take: the size column is an int,
    /// and anything bigger than this is not a "document" any more.</summary>
    public const long MaxFileSizeCeiling = 100L * 1024 * 1024;

    /// <summary>Problems with the values, for the startup check. Empty when all is well.</summary>
    public IEnumerable<string> Problems()
    {
        if (MaxFileSizeBytes < 1 || MaxFileSizeBytes > MaxFileSizeCeiling)
            yield return $"{SectionName}:MaxFileSizeBytes must be between 1 and {MaxFileSizeCeiling} (it is {MaxFileSizeBytes}).";
        if (DeletedFileRetentionDays < 0)
            yield return $"{SectionName}:DeletedFileRetentionDays cannot be negative (it is {DeletedFileRetentionDays}).";
        if (ExpiringSoonDays < 0)
            yield return $"{SectionName}:ExpiringSoonDays cannot be negative (it is {ExpiringSoonDays}).";
    }
}
