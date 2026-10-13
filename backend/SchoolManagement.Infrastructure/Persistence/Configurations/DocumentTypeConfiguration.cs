using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;
using SchoolManagement.Domain.Entities;

namespace SchoolManagement.Infrastructure.Persistence.Configurations;

/// <summary>G1b: document types are reference data with rules, seeded by the migration (Code-keyed SQL).</summary>
public class DocumentTypeConfiguration : IEntityTypeConfiguration<DocumentType>
{
    public void Configure(EntityTypeBuilder<DocumentType> builder)
    {
        builder.ToTable("DocumentTypes", t =>
            t.HasCheckConstraint("CK_DocumentTypes_CodeNotBlank", "LEN(LTRIM(RTRIM([Code]))) > 0"));

        builder.HasKey(t => t.Id);
        builder.Property(t => t.Code).HasMaxLength(40).IsRequired();
        builder.HasIndex(t => t.Code).IsUnique().HasDatabaseName("UX_DocumentTypes_Code");
        builder.Property(t => t.Name).HasMaxLength(80).IsRequired();
        builder.Property(t => t.IsSensitive).IsRequired().HasDefaultValue(false);
        builder.Property(t => t.HasExpiry).IsRequired().HasDefaultValue(false);
        builder.Property(t => t.ForStudents).IsRequired().HasDefaultValue(true);
        builder.Property(t => t.ForStaff).IsRequired().HasDefaultValue(false);
        builder.Property(t => t.SortOrder).IsRequired().HasDefaultValue(0);
        builder.Property(t => t.IsActive).IsRequired().HasDefaultValue(true);
    }
}
