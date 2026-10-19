using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;
using SchoolManagement.Domain.Entities;

namespace SchoolManagement.Infrastructure.Persistence.Configurations;

/// <summary>
/// G1b. The document rules live in the database as well as in DocumentService, so no code path can
/// break them:
///   - exactly one owner per row (CK_Documents_ExactlyOneOwner);
///   - status, content type and size are valid (CK_Documents_Status / _ContentType / _SizePositive);
///   - a reviewed document names its reviewer, a rejected one has a reason;
///   - the reviewer is never the uploader, except an Admin (checked in DocumentService, not here);
///   - the same file is attached to one owner only once while not deleted (UX_Documents_{Owner}_Sha256).
///
/// The size LIMIT is a setting (Documents:MaxFileSizeBytes), so the database only checks that a
/// size is positive: a CHECK with the number in it would have to be changed by a migration every
/// time the setting changes.
/// </summary>
public class DocumentConfiguration : IEntityTypeConfiguration<Document>
{
    /// <summary>The owner columns. E2 adds StaffProfileId here and in the CHECK below.</summary>
    internal static readonly string[] OwnerColumns = { "StudentId", "AdmissionId" };

    public void Configure(EntityTypeBuilder<Document> builder)
    {
        builder.ToTable("Documents", t =>
        {
            t.HasCheckConstraint("CK_Documents_ExactlyOneOwner",
                string.Join(" + ", OwnerColumns.Select(c => $"(CASE WHEN [{c}] IS NULL THEN 0 ELSE 1 END)")) + " = 1");
            t.HasCheckConstraint("CK_Documents_Status", CheckSql.In("Status", DocumentStatuses.All));
            t.HasCheckConstraint("CK_Documents_ContentType", CheckSql.In("ContentType", DocumentContentTypes.All));
            t.HasCheckConstraint("CK_Documents_SizePositive", "[SizeBytes] > 0");
            t.HasCheckConstraint("CK_Documents_ReviewedHasReviewer",
                "[Status] = 'Pending' OR ([VerifiedBy] IS NOT NULL AND [VerifiedAt] IS NOT NULL)");
            t.HasCheckConstraint("CK_Documents_PendingHasNoReviewer",
                "[Status] <> 'Pending' OR ([VerifiedBy] IS NULL AND [VerifiedAt] IS NULL AND [RejectionReason] IS NULL)");
            t.HasCheckConstraint("CK_Documents_RejectedHasReason",
                "[Status] <> 'Rejected' OR LEN(LTRIM(RTRIM([RejectionReason]))) > 0");
            // (No "uploader <> reviewer" CHECK: an Admin may review their own upload, and the database
            // cannot tell an Admin from anyone else. DocumentService enforces the rule for everyone else.)
        });

        builder.HasKey(d => d.Id);

        builder.Property(d => d.Title).HasMaxLength(120);
        builder.Property(d => d.OriginalFileName).HasMaxLength(255).IsRequired();
        builder.Property(d => d.ContentType).HasMaxLength(50).IsRequired();
        builder.Property(d => d.SizeBytes).IsRequired();
        builder.Property(d => d.Sha256).HasColumnType("binary(32)").IsRequired();
        builder.Property(d => d.StorageKey).HasMaxLength(200).IsRequired();
        builder.HasIndex(d => d.StorageKey).IsUnique().HasDatabaseName("UX_Documents_StorageKey");
        builder.Property(d => d.Status).HasMaxLength(20).IsRequired().HasDefaultValue(DocumentStatuses.Pending);
        builder.Property(d => d.RejectionReason).HasMaxLength(300);

        builder.Property(d => d.CreatedAt).HasDefaultValueSql("SYSUTCDATETIME()");
        builder.Property(d => d.UpdatedAt).HasDefaultValueSql("SYSUTCDATETIME()");
        builder.Property(d => d.RowVersion).IsRowVersion(); // two people reviewing/deleting at once -> 409
        builder.Property(d => d.IsDeleted).IsRequired().HasDefaultValue(false);
        builder.HasQueryFilter(d => !d.IsDeleted);

        // Owners are soft-deleted, so these never cascade in normal use. NO ACTION (Restrict) rather than
        // CASCADE: a hard delete of an owner must not silently orphan files in storage.
        builder.HasOne(d => d.Student).WithMany()
            .HasForeignKey(d => d.StudentId).OnDelete(DeleteBehavior.Restrict);
        builder.HasOne(d => d.Admission).WithMany()
            .HasForeignKey(d => d.AdmissionId).OnDelete(DeleteBehavior.Restrict);
        builder.HasOne(d => d.DocumentType).WithMany()
            .HasForeignKey(d => d.DocumentTypeId).OnDelete(DeleteBehavior.Restrict);
        builder.HasOne(d => d.VerifiedByUser).WithMany()
            .HasForeignKey(d => d.VerifiedBy).OnDelete(DeleteBehavior.Restrict);

        // The record's Documents tab (and the enrolment move). INCLUDE keeps the tab query on the index.
        builder.HasIndex(d => d.StudentId)
            .HasFilter("[StudentId] IS NOT NULL AND [IsDeleted] = 0")
            .IncludeProperties(d => new { d.DocumentTypeId, d.Status })
            .HasDatabaseName("IX_Documents_StudentId");
        builder.HasIndex(d => d.AdmissionId)
            .HasFilter("[AdmissionId] IS NOT NULL AND [IsDeleted] = 0")
            .HasDatabaseName("IX_Documents_AdmissionId");

        // The same file once per owner (a double click, a retry, the same scan picked twice).
        // A deleted document does not count, so a file deleted by mistake can be uploaded again.
        builder.HasIndex(d => new { d.StudentId, d.Sha256 })
            .IsUnique()
            .HasFilter("[StudentId] IS NOT NULL AND [IsDeleted] = 0")
            .HasDatabaseName("UX_Documents_Student_Sha256");
        builder.HasIndex(d => new { d.AdmissionId, d.Sha256 })
            .IsUnique()
            .HasFilter("[AdmissionId] IS NOT NULL AND [IsDeleted] = 0")
            .HasDatabaseName("UX_Documents_Admission_Sha256");

        // Documents page: "Pending" queue, newest first.
        builder.HasIndex(d => new { d.Status, d.CreatedAt })
            .HasFilter("[IsDeleted] = 0")
            .HasDatabaseName("IX_Documents_Status_CreatedAt");
        // "Expired / expiring soon" filter.
        builder.HasIndex(d => d.ExpiryDate)
            .HasFilter("[ExpiryDate] IS NOT NULL AND [IsDeleted] = 0")
            .HasDatabaseName("IX_Documents_ExpiryDate");
        // Purge job: deleted documents whose file is still stored.
        builder.HasIndex(d => d.DeletedAt)
            .HasFilter("[IsDeleted] = 1 AND [FilePurgedAt] IS NULL")
            .HasDatabaseName("IX_Documents_PurgeQueue");
    }
}
