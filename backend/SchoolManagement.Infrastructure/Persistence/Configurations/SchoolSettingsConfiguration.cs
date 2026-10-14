using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;
using SchoolManagement.Domain.Entities;

namespace SchoolManagement.Infrastructure.Persistence.Configurations;

/// <summary>
/// E2: the single school record. The CHECK on Id makes a second row impossible; the row itself is
/// seeded by the E2a migration. The time-zone id is checked by the service (SQL Server cannot tell a
/// real IANA id from a typo); the other rules are CHECKs.
/// </summary>
public class SchoolSettingsConfiguration : IEntityTypeConfiguration<SchoolSettings>
{
    public void Configure(EntityTypeBuilder<SchoolSettings> builder)
    {
        builder.ToTable("SchoolSettings", t =>
        {
            t.HasCheckConstraint("CK_SchoolSettings_SingleRow", $"[Id] = {SchoolSettings.SingletonId}");
            t.HasCheckConstraint("CK_SchoolSettings_SchoolNameNotBlank", "LEN(LTRIM(RTRIM([SchoolName]))) > 0");
            t.HasCheckConstraint("CK_SchoolSettings_TimeZoneNotBlank", "LEN(LTRIM(RTRIM([TimeZoneId]))) > 0");
            // Binary collation: the database's default collation ignores case, so "aed" would pass
            // both "= UPPER(...)" and "[^A-Z]". BIN2 compares the actual characters.
            t.HasCheckConstraint("CK_SchoolSettings_CurrencyCode",
                "LEN([CurrencyCode]) = 3 AND [CurrencyCode] COLLATE Latin1_General_BIN2 NOT LIKE '%[^A-Z]%'");
            t.HasCheckConstraint("CK_SchoolSettings_WorkingDays", $"[WorkingDays] BETWEEN 1 AND {WorkingDays.All}");
        });

        builder.HasKey(s => s.Id);
        builder.Property(s => s.Id).ValueGeneratedNever();

        builder.Property(s => s.SchoolName).HasMaxLength(150).IsRequired();
        builder.Property(s => s.ShortName).HasMaxLength(30);
        builder.Property(s => s.Email).HasMaxLength(100);
        builder.Property(s => s.Phone).HasMaxLength(20);
        builder.Property(s => s.Website).HasMaxLength(200);
        builder.Property(s => s.AddressText).HasMaxLength(300);
        builder.Property(s => s.TimeZoneId).HasMaxLength(64).IsRequired();
        builder.Property(s => s.CurrencyCode).HasMaxLength(3).IsFixedLength().IsRequired();
        builder.Property(s => s.WorkingDays).IsRequired();
        builder.Property(s => s.AboutTagline).HasMaxLength(300);

        builder.Property(s => s.CreatedAt).HasDefaultValueSql("SYSUTCDATETIME()");
        builder.Property(s => s.UpdatedAt).HasDefaultValueSql("SYSUTCDATETIME()");
        builder.Property(s => s.RowVersion).IsRowVersion();

        builder.HasOne(s => s.Country).WithMany()
            .HasForeignKey(s => s.CountryId).OnDelete(DeleteBehavior.Restrict);
    }
}
