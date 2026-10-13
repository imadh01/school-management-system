using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;
using SchoolManagement.Domain.Entities;

namespace SchoolManagement.Infrastructure.Persistence.Configurations;

/// <summary>G1a: the states / emirates of a country.</summary>
public class CountryRegionConfiguration : IEntityTypeConfiguration<CountryRegion>
{
    public void Configure(EntityTypeBuilder<CountryRegion> builder)
    {
        builder.ToTable("CountryRegions");
        builder.HasKey(r => r.Id);
        builder.Property(r => r.Name).HasMaxLength(60).IsRequired();
        builder.Property(r => r.SortOrder).IsRequired().HasDefaultValue(0);
        builder.Property(r => r.IsActive).IsRequired().HasDefaultValue(true);

        // One name per country; also the target of the composite key used by Addresses.
        builder.HasIndex(r => new { r.CountryId, r.Name }).IsUnique().HasDatabaseName("UX_CountryRegions_Country_Name");
        builder.HasAlternateKey(r => new { r.CountryId, r.Id }).HasName("AK_CountryRegions_CountryId_Id");
    }
}
