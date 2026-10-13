using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;
using SchoolManagement.Domain.Entities;

namespace SchoolManagement.Infrastructure.Persistence.Configurations;

/// <summary>G1a: countries and their regions are reference data, seeded by the migration (name-keyed SQL).</summary>
public class CountryConfiguration : IEntityTypeConfiguration<Country>
{
    public void Configure(EntityTypeBuilder<Country> builder)
    {
        builder.ToTable("Countries", t =>
            t.HasCheckConstraint("CK_Countries_Code", "LEN([Code]) = 2 AND [Code] = UPPER([Code])"));

        builder.HasKey(c => c.Id);
        builder.Property(c => c.Code).HasMaxLength(2).IsFixedLength().IsRequired();
        builder.HasIndex(c => c.Code).IsUnique().HasDatabaseName("UX_Countries_Code");
        builder.Property(c => c.Name).HasMaxLength(60).IsRequired();
        builder.HasIndex(c => c.Name).IsUnique().HasDatabaseName("UX_Countries_Name");
        builder.Property(c => c.RegionLabel).HasMaxLength(30).IsRequired();
        builder.Property(c => c.PostalCodeRequired).IsRequired().HasDefaultValue(false);
        builder.Property(c => c.PostalCodePattern).HasMaxLength(100);
        builder.Property(c => c.PostalCodeHint).HasMaxLength(60);
        builder.Property(c => c.SortOrder).IsRequired().HasDefaultValue(0);
        builder.Property(c => c.IsActive).IsRequired().HasDefaultValue(true);

        builder.HasMany(c => c.Regions).WithOne(r => r.Country)
            .HasForeignKey(r => r.CountryId).OnDelete(DeleteBehavior.Restrict);
    }
}
