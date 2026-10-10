using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;
using SchoolManagement.Domain.Entities;

namespace SchoolManagement.Infrastructure.Persistence.Configurations;

public class AuditLogConfiguration : IEntityTypeConfiguration<AuditLog>
{
    public void Configure(EntityTypeBuilder<AuditLog> builder)
    {
        builder.ToTable("AuditLogs", t =>
            t.HasCheckConstraint("CK_AuditLogs_Action", CheckSql.In("Action", AuditActions.All)));
        builder.HasKey(a => a.Id);

        builder.Property(a => a.OccurredAt).IsRequired();
        builder.Property(a => a.UserName).HasMaxLength(256);
        builder.Property(a => a.Action).HasMaxLength(10).IsRequired();
        builder.Property(a => a.TableName).HasMaxLength(100).IsRequired();
        builder.Property(a => a.RecordId).HasMaxLength(100).IsRequired();
        builder.Property(a => a.TraceId).HasMaxLength(64);
        // Changes stays nvarchar(max): a new student row can have dozens of fields.

        // "History of this record" - the main question an audit log answers.
        builder.HasIndex(a => new { a.TableName, a.RecordId, a.OccurredAt })
            .HasDatabaseName("IX_AuditLogs_Table_Record");

        // "What did this person change?"
        builder.HasIndex(a => new { a.UserId, a.OccurredAt })
            .HasDatabaseName("IX_AuditLogs_User_Time");

        // "What happened between these two dates?" and clean-up by age later.
        builder.HasIndex(a => a.OccurredAt)
            .HasDatabaseName("IX_AuditLogs_OccurredAt");

        // No query filter and no foreign keys: the log must outlive the records and users it describes.
    }
}
