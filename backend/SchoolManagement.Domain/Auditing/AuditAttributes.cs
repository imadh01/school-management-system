namespace SchoolManagement.Domain.Auditing;

/// <summary>
/// Put on a property (or a whole entity class) whose VALUE must never be written to the audit log.
/// The log still records that the field changed, but shows "***" instead of the old and new value.
/// Use it for anything sensitive: password hashes, Aadhaar / passport numbers, health details...
/// </summary>
[AttributeUsage(AttributeTargets.Class | AttributeTargets.Property)]
public sealed class AuditMaskedAttribute : Attribute
{
}

/// <summary>
/// On a property: changes to it are left out of the audit log (e.g. LastLoginAt, which changes at every login).
/// On an entity class: that table gets no audit-log rows at all (e.g. attendance, which has one row per
/// student per day and would flood the log). The CreatedAt / CreatedBy / UpdatedAt / UpdatedBy columns
/// are still filled in either way.
/// </summary>
[AttributeUsage(AttributeTargets.Class | AttributeTargets.Property)]
public sealed class AuditIgnoreAttribute : Attribute
{
}
