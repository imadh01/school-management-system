using System.Text.Json;
using SchoolManagement.Application.Interfaces;
using SchoolManagement.Domain.Entities;

namespace SchoolManagement.Infrastructure.Persistence;

/// <summary>
/// Writes one AuditLogs row (Action = "Read") when sensitive data is opened (Batch F1).
/// Same columns as the change log written by AuditableEntitySaveChangesInterceptor, so "who looked at
/// this student" and "who changed this student" are answered by the same query.
/// </summary>
public sealed class SensitiveReadLog : ISensitiveReadLog
{
    private static readonly JsonSerializerOptions JsonOptions = new() { PropertyNamingPolicy = JsonNamingPolicy.CamelCase };

    private readonly ApplicationDbContext _context;
    private readonly ICurrentUser _currentUser;
    private readonly TimeProvider _time;

    public SensitiveReadLog(ApplicationDbContext context, ICurrentUser currentUser, TimeProvider time)
    {
        _context = context;
        _currentUser = currentUser;
        _time = time;
    }

    public async Task RecordAsync(
        string tableName, int recordId, IReadOnlyCollection<string> shown, CancellationToken cancellationToken)
    {
        // This save must contain the log row and nothing else. A pending change here would mean a read
        // path modified an entity, and the log row would silently commit it.
        if (_context.ChangeTracker.HasChanges())
            throw new InvalidOperationException(
                "A sensitive read was logged while other changes were pending. Read paths must not modify entities.");

        _context.AuditLogs.Add(new AuditLog
        {
            OccurredAt = _time.GetUtcNow().UtcDateTime,
            UserId = _currentUser.UserId,
            UserName = Truncate(_currentUser.UserName, 256),
            Action = AuditActions.Read,
            TableName = tableName,
            RecordId = recordId.ToString(),
            Changes = JsonSerializer.Serialize(new { shown }, JsonOptions),
            TraceId = Truncate(_currentUser.TraceId, 64),
        });

        await _context.SaveChangesAsync(cancellationToken);
    }

    private static string? Truncate(string? value, int max) =>
        value is null || value.Length <= max ? value : value[..max];
}
