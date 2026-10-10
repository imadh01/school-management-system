using SchoolManagement.Domain.Auditing;

namespace SchoolManagement.Domain.Entities;

/// <summary>
/// One row per create / edit / delete of a record: who did it, when, and which fields changed.
/// Written automatically on save (see AuditableEntitySaveChangesInterceptor); never edited or deleted by the app.
/// </summary>
[AuditIgnore] // the audit log does not audit itself
public class AuditLog
{
    public long Id { get; set; }

    /// <summary>UTC time of the change.</summary>
    public DateTime OccurredAt { get; set; }

    /// <summary>
    /// The logged-in user. Deliberately NOT a foreign key: the log must survive even if the user row is
    /// ever removed. Null when the change was made by the system itself (seeding, background work).
    /// </summary>
    public int? UserId { get; set; }

    /// <summary>The username at that moment (a snapshot, so the log stays readable if the user is renamed).</summary>
    public string? UserName { get; set; }

    /// <summary>Insert | Update | Delete (a soft delete is logged as Delete).</summary>
    public string Action { get; set; } = string.Empty;

    /// <summary>Database table of the record, e.g. "Students".</summary>
    public string TableName { get; set; } = string.Empty;

    /// <summary>Primary key of the record. Composite keys read "StudentId=5, ParentId=9".</summary>
    public string RecordId { get; set; } = string.Empty;

    /// <summary>
    /// JSON of the fields that changed: {"Capacity":{"old":30,"new":40}}. Inserts hold only "new", deletes only "old".
    /// Sensitive fields show "***" instead of their value.
    /// </summary>
    public string? Changes { get; set; }

    /// <summary>The request's correlation/trace ID, to match this row with the Serilog log lines.</summary>
    public string? TraceId { get; set; }
}

public static class AuditActions
{
    public const string Insert = "Insert";
    public const string Update = "Update";
    public const string Delete = "Delete";

    public static readonly string[] All = { Insert, Update, Delete };
}