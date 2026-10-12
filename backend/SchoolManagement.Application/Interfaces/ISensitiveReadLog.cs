namespace SchoolManagement.Application.Interfaces;

/// <summary>
/// Leaves an AuditLogs row (Action = "Read") when someone opens sensitive data, e.g. a student's
/// health or identity details (Batch F1). The row records WHAT was shown, never the values.
/// Writes have their own Insert/Update rows, so only reads are recorded here.
/// </summary>
public interface ISensitiveReadLog
{
    /// <param name="tableName">The record's table, e.g. "Students".</param>
    /// <param name="recordId">The record's primary key.</param>
    /// <param name="shown">The sensitive parts the response contained, e.g. "Health", "Identity (masked)".</param>
    Task RecordAsync(string tableName, int recordId, IReadOnlyCollection<string> shown, CancellationToken cancellationToken);
}
