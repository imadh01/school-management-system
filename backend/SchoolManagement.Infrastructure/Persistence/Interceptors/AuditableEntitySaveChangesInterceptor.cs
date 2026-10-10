using System.Collections.Concurrent;
using System.Text.Json;
using System.Text.Json.Serialization;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.ChangeTracking;
using Microsoft.EntityFrameworkCore.Diagnostics;
using Microsoft.EntityFrameworkCore.Metadata;
using Microsoft.EntityFrameworkCore.Storage;
using SchoolManagement.Application.Interfaces;
using SchoolManagement.Domain.Auditing;
using SchoolManagement.Domain.Entities;

namespace SchoolManagement.Infrastructure.Persistence.Interceptors;

/// <summary>
/// Runs on every SaveChanges and does two jobs, so no service has to remember to:
///
/// 1. STAMPS the audit columns of every row: CreatedAt/UpdatedAt (UTC), CreatedBy/UpdatedBy (the logged-in
///    user) and, for a soft delete, DeletedAt/DeletedBy. Any table with columns of those names is covered.
///
/// 2. WRITES THE AUDIT LOG: one AuditLogs row per created / edited / deleted record, with the changed fields.
///    Fields marked [AuditMasked] show "***"; fields or tables marked [AuditIgnore] are left out.
///    The log row is saved in the SAME database transaction as the change itself.
///
/// One instance lives per request (scoped), because it remembers what it must write after the save.
/// </summary>
public class AuditableEntitySaveChangesInterceptor : SaveChangesInterceptor
{
    private const string Masked = "***";

    // Audit columns are described by the stamps, not by the log; RowVersion changes on every save.
    private static readonly HashSet<string> NotLogged = new()
    {
        "CreatedAt", "CreatedBy", "UpdatedAt", "UpdatedBy", "DeletedAt", "DeletedBy", "RowVersion",
    };

    private static readonly JsonSerializerOptions JsonOptions = new()
    {
        PropertyNamingPolicy = JsonNamingPolicy.CamelCase,
        DefaultIgnoreCondition = JsonIgnoreCondition.WhenWritingNull,
    };

    private static readonly ConcurrentDictionary<Type, TypeRules> RulesCache = new();

    private readonly ICurrentUser? _currentUser;

    // Inserts can only be logged AFTER the save, because the new record's Id does not exist before it.
    private readonly List<PendingInsert> _pendingInserts = new();
    private IDbContextTransaction? _ownTransaction; // started by us only when the caller has no transaction
    private bool _writingAuditRows;                 // true while we save the log rows (avoids logging the log)

    public AuditableEntitySaveChangesInterceptor(ICurrentUser? currentUser = null)
    {
        _currentUser = currentUser;
    }

    // ------------------------------------------------------------------ before the save

    public override InterceptionResult<int> SavingChanges(
        DbContextEventData eventData, InterceptionResult<int> result)
    {
        if (eventData.Context is { } context && PrepareAudit(context) && context.Database.CurrentTransaction is null)
            _ownTransaction = context.Database.BeginTransaction();

        return base.SavingChanges(eventData, result);
    }

    public override async ValueTask<InterceptionResult<int>> SavingChangesAsync(
        DbContextEventData eventData, InterceptionResult<int> result, CancellationToken cancellationToken = default)
    {
        if (eventData.Context is { } context && PrepareAudit(context) && context.Database.CurrentTransaction is null)
            _ownTransaction = await context.Database.BeginTransactionAsync(cancellationToken);

        return await base.SavingChangesAsync(eventData, result, cancellationToken);
    }

    // ------------------------------------------------------------------ after the save

    public override int SavedChanges(SaveChangesCompletedEventData eventData, int result)
    {
        if (!_writingAuditRows && eventData.Context is { } context)
        {
            try
            {
                if (QueuePendingInserts(context))
                {
                    _writingAuditRows = true;
                    try { context.SaveChanges(); }
                    finally { _writingAuditRows = false; }
                }
                CommitOwnTransaction();
            }
            catch
            {
                RollbackOwnTransaction();
                throw;
            }
        }

        return base.SavedChanges(eventData, result);
    }

    public override async ValueTask<int> SavedChangesAsync(
        SaveChangesCompletedEventData eventData, int result, CancellationToken cancellationToken = default)
    {
        if (!_writingAuditRows && eventData.Context is { } context)
        {
            try
            {
                if (QueuePendingInserts(context))
                {
                    _writingAuditRows = true;
                    try { await context.SaveChangesAsync(cancellationToken); }
                    finally { _writingAuditRows = false; }
                }
                await CommitOwnTransactionAsync(cancellationToken);
            }
            catch
            {
                await RollbackOwnTransactionAsync();
                throw;
            }
        }

        return await base.SavedChangesAsync(eventData, result, cancellationToken);
    }

    public override void SaveChangesFailed(DbContextErrorEventData eventData)
    {
        _pendingInserts.Clear();
        RollbackOwnTransaction();
        base.SaveChangesFailed(eventData);
    }

    public override async Task SaveChangesFailedAsync(
        DbContextErrorEventData eventData, CancellationToken cancellationToken = default)
    {
        _pendingInserts.Clear();
        await RollbackOwnTransactionAsync();
        await base.SaveChangesFailedAsync(eventData, cancellationToken);
    }

    public override void SaveChangesCanceled(DbContextEventData eventData)
    {
        _pendingInserts.Clear();
        RollbackOwnTransaction();
        base.SaveChangesCanceled(eventData);
    }

    public override async Task SaveChangesCanceledAsync(
        DbContextEventData eventData, CancellationToken cancellationToken = default)
    {
        _pendingInserts.Clear();
        await RollbackOwnTransactionAsync();
        await base.SaveChangesCanceledAsync(eventData, cancellationToken);
    }

    // ------------------------------------------------------------------ the work

    /// <summary>
    /// Stamps every changed row and adds the audit-log rows for edits and deletes.
    /// Returns true when there are inserts to log after the save (so the save needs a transaction).
    /// </summary>
    private bool PrepareAudit(DbContext context)
    {
        if (_writingAuditRows) return false;
        _pendingInserts.Clear();

        var now = DateTime.UtcNow;
        var userId = _currentUser?.UserId;
        var userName = Truncate(_currentUser?.UserName ?? (userId is null ? "system" : null), 256);
        var traceId = Truncate(_currentUser?.TraceId, 64);

        // ToList: we add AuditLog rows to the tracker below, which must not disturb this loop.
        var entries = context.ChangeTracker.Entries()
            .Where(e => e.State is EntityState.Added or EntityState.Modified or EntityState.Deleted)
            .Where(e => e.Entity is not AuditLog)
            .ToList();

        foreach (var entry in entries)
        {
            var softDeleted = IsSoftDeleted(entry);
            Stamp(entry, now, userId, softDeleted);

            var rules = RulesFor(entry.Entity.GetType());
            if (rules.Ignored) continue;

            var changes = BuildChanges(entry, rules);
            if (entry.State == EntityState.Modified && changes.Count == 0) continue; // nothing real changed

            var log = new AuditLog
            {
                OccurredAt = now,
                UserId = userId,
                UserName = userName,
                Action = entry.State switch
                {
                    EntityState.Added => AuditActions.Insert,
                    EntityState.Deleted => AuditActions.Delete,
                    _ => softDeleted ? AuditActions.Delete : AuditActions.Update,
                },
                TableName = entry.Metadata.GetTableName() ?? entry.Metadata.ClrType.Name,
                RecordId = string.Empty,
                Changes = changes.Count == 0 ? null : JsonSerializer.Serialize(changes, JsonOptions),
                TraceId = traceId,
            };

            if (entry.State == EntityState.Added)
            {
                _pendingInserts.Add(new PendingInsert(entry, log)); // Id is known only after the save
            }
            else
            {
                log.RecordId = RecordIdOf(entry);
                context.Set<AuditLog>().Add(log);
            }
        }

        return _pendingInserts.Count > 0;
    }

    /// <summary>Adds the log rows of the records that were just inserted (their Ids exist now).</summary>
    private bool QueuePendingInserts(DbContext context)
    {
        if (_pendingInserts.Count == 0) return false;

        foreach (var pending in _pendingInserts)
        {
            pending.Log.RecordId = RecordIdOf(pending.Entry);
            context.Set<AuditLog>().Add(pending.Log);
        }
        _pendingInserts.Clear();
        return true;
    }

    private static void Stamp(EntityEntry entry, DateTime now, int? userId, bool softDeleted)
    {
        if (entry.State == EntityState.Added)
        {
            SetIfPresent(entry, "CreatedAt", now);
            SetIfPresent(entry, "UpdatedAt", now);
            if (userId is not null)
            {
                SetIfPresent(entry, "CreatedBy", userId);
                SetIfPresent(entry, "UpdatedBy", userId);
            }
        }
        else if (entry.State == EntityState.Modified)
        {
            SetIfPresent(entry, "UpdatedAt", now);
            if (userId is not null) SetIfPresent(entry, "UpdatedBy", userId);

            if (softDeleted)
            {
                // Services may already have set DeletedAt; only fill it in when they did not.
                if (entry.Metadata.FindProperty("DeletedAt") is not null &&
                    entry.Property("DeletedAt").CurrentValue is null)
                    SetIfPresent(entry, "DeletedAt", now);

                if (userId is not null) SetIfPresent(entry, "DeletedBy", userId);
            }
        }
    }

    private static void SetIfPresent(EntityEntry entry, string propertyName, object? value)
    {
        if (entry.Metadata.FindProperty(propertyName) is not null)
            entry.Property(propertyName).CurrentValue = value;
    }

    /// <summary>True when this save flips IsDeleted from false to true.</summary>
    private static bool IsSoftDeleted(EntityEntry entry)
    {
        if (entry.State != EntityState.Modified || entry.Metadata.FindProperty("IsDeleted") is null)
            return false;

        var property = entry.Property("IsDeleted");
        return property.IsModified && property.OriginalValue is false && property.CurrentValue is true;
    }

    /// <summary>The fields to put in the log: all of them for an insert/delete, only the changed ones for an edit.</summary>
    private static Dictionary<string, FieldChange> BuildChanges(EntityEntry entry, TypeRules rules)
    {
        var changes = new Dictionary<string, FieldChange>();

        foreach (var property in entry.Properties)
        {
            var metadata = property.Metadata;
            if (metadata.IsPrimaryKey()
                || NotLogged.Contains(metadata.Name)
                || rules.IgnoredProperties.Contains(metadata.Name)
                || metadata.ValueGenerated is ValueGenerated.OnAddOrUpdate or ValueGenerated.OnUpdate) // computed by the database
                continue;

            object? oldValue;
            object? newValue;
            switch (entry.State)
            {
                case EntityState.Added:
                    oldValue = null;
                    newValue = property.CurrentValue;
                    break;
                case EntityState.Deleted:
                    oldValue = property.OriginalValue;
                    newValue = null;
                    break;
                default:
                    if (!property.IsModified) continue;
                    oldValue = property.OriginalValue;
                    newValue = property.CurrentValue;
                    if (Equals(oldValue, newValue)) continue; // compare the REAL values, mask afterwards
                    break;
            }

            if (oldValue is null && newValue is null) continue;

            var mask = rules.MaskEverything || rules.MaskedProperties.Contains(metadata.Name);
            changes[metadata.Name] = new FieldChange(Show(oldValue, mask), Show(newValue, mask));
        }

        return changes;
    }

    private static object? Show(object? value, bool mask) => value is null ? null : mask ? Masked : value;

    private static string RecordIdOf(EntityEntry entry)
    {
        var key = entry.Metadata.FindPrimaryKey();
        if (key is null) return string.Empty;

        var values = key.Properties
            .Select(p => (p.Name, Value: entry.Property(p.Name).CurrentValue))
            .ToList();

        return values.Count == 1
            ? Convert.ToString(values[0].Value) ?? string.Empty
            : string.Join(", ", values.Select(v => $"{v.Name}={v.Value}"));
    }

    private static string? Truncate(string? value, int maxLength) =>
        value is null || value.Length <= maxLength ? value : value[..maxLength];

    // ------------------------------------------------------------------ our own transaction

    private void CommitOwnTransaction()
    {
        var transaction = _ownTransaction;
        _ownTransaction = null;
        if (transaction is null) return;
        try { transaction.Commit(); }
        finally { transaction.Dispose(); }
    }

    private async Task CommitOwnTransactionAsync(CancellationToken cancellationToken)
    {
        var transaction = _ownTransaction;
        _ownTransaction = null;
        if (transaction is null) return;
        try { await transaction.CommitAsync(cancellationToken); }
        finally { await transaction.DisposeAsync(); }
    }

    private void RollbackOwnTransaction()
    {
        var transaction = _ownTransaction;
        _ownTransaction = null;
        if (transaction is null) return;
        try { transaction.Rollback(); }
        finally { transaction.Dispose(); }
    }

    private async Task RollbackOwnTransactionAsync()
    {
        var transaction = _ownTransaction;
        _ownTransaction = null;
        if (transaction is null) return;
        try { await transaction.RollbackAsync(); }
        finally { await transaction.DisposeAsync(); }
    }

    // ------------------------------------------------------------------ small helpers

    private static TypeRules RulesFor(Type type) => RulesCache.GetOrAdd(type, static t =>
    {
        var masked = new HashSet<string>();
        var ignored = new HashSet<string>();
        foreach (var property in t.GetProperties())
        {
            if (property.IsDefined(typeof(AuditMaskedAttribute), inherit: true)) masked.Add(property.Name);
            if (property.IsDefined(typeof(AuditIgnoreAttribute), inherit: true)) ignored.Add(property.Name);
        }

        return new TypeRules(
            Ignored: t.IsDefined(typeof(AuditIgnoreAttribute), inherit: true),
            MaskEverything: t.IsDefined(typeof(AuditMaskedAttribute), inherit: true),
            MaskedProperties: masked,
            IgnoredProperties: ignored);
    });

    private sealed record TypeRules(
        bool Ignored, bool MaskEverything, HashSet<string> MaskedProperties, HashSet<string> IgnoredProperties);

    private sealed record PendingInsert(EntityEntry Entry, AuditLog Log);

    // Written to JSON as {"old": ..., "new": ...}; null sides are left out.
    internal sealed record FieldChange(object? Old, object? New);
}