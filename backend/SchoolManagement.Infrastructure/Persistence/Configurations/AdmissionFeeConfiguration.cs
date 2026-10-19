using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;
using SchoolManagement.Domain.Entities;

namespace SchoolManagement.Infrastructure.Persistence.Configurations;

public class AdmissionFeeConfiguration : IEntityTypeConfiguration<AdmissionFee>
{
    public void Configure(EntityTypeBuilder<AdmissionFee> builder)
    {
        builder.ToTable("AdmissionFees", t =>
            t.HasCheckConstraint("CK_AdmissionFees_AmountNotNegative", "[Amount] >= 0"));

        builder.HasKey(f => f.Id);
        builder.Property(f => f.ClassName).HasMaxLength(50).IsRequired();
        builder.Property(f => f.Amount).HasColumnType("decimal(10,2)").IsRequired();

        builder.Property(f => f.CreatedAt).HasDefaultValueSql("SYSUTCDATETIME()");
        builder.Property(f => f.UpdatedAt).HasDefaultValueSql("SYSUTCDATETIME()");

        // One fee per class.
        builder.HasIndex(f => f.ClassName).IsUnique().HasDatabaseName("UX_AdmissionFees_ClassName");
    }
}
