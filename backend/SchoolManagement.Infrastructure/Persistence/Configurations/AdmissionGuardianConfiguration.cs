using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;
using SchoolManagement.Domain.Entities;

namespace SchoolManagement.Infrastructure.Persistence.Configurations;

public class AdmissionGuardianConfiguration : IEntityTypeConfiguration<AdmissionGuardian>
{
    public void Configure(EntityTypeBuilder<AdmissionGuardian> builder)
    {
        builder.ToTable("AdmissionGuardians", t =>
            t.HasCheckConstraint("CK_AdmissionGuardians_RelationType",
                "[RelationType] IN ('Father','Mother','Guardian')"));

        builder.HasKey(g => g.Id);

        builder.Property(g => g.RelationType).HasMaxLength(30).IsRequired();
        builder.Property(g => g.Name).HasMaxLength(100).IsRequired();
        builder.Property(g => g.Mobile).HasMaxLength(20);
        builder.Property(g => g.Email).HasMaxLength(100);
        builder.Property(g => g.IsPrimaryContact).IsRequired().HasDefaultValue(false);

        builder.Property(g => g.MobileKey)
            .HasMaxLength(20)
            .HasComputedColumnSql(MobileKeySql.Expression("Mobile"), stored: true);

        builder.Property(g => g.CreatedAt).HasDefaultValueSql("SYSUTCDATETIME()");
        builder.Property(g => g.UpdatedAt).HasDefaultValueSql("SYSUTCDATETIME()");

        builder.HasOne(g => g.Admission)
            .WithMany(a => a.Guardians)
            .HasForeignKey(g => g.AdmissionId)
            .OnDelete(DeleteBehavior.Cascade);

        // Loading guardians of one application; also the FK index.
        builder.HasIndex(g => g.AdmissionId).HasDatabaseName("IX_AdmissionGuardians_AdmissionId");

        // At most one primary contact per application.
        builder.HasIndex(g => g.AdmissionId)
            .IsUnique()
            .HasFilter("[IsPrimaryContact] = 1")
            .HasDatabaseName("UX_AdmissionGuardians_Admission_Primary");

        // Same filter as Admission so soft-deleted applications hide their guardians.
        builder.HasQueryFilter(g => !g.Admission.IsDeleted);
    }
}
