using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;
using SchoolManagement.Domain.Entities;

namespace SchoolManagement.Infrastructure.Persistence.Configurations;

/// <summary>
/// G1a. The rules that keep addresses consistent live in the database, not only in the forms:
///   - exactly one owner per row (CK_Addresses_ExactlyOneOwner);
///   - at most one address of each type per owner (UX_Addresses_{Owner}_Type);
///   - at most one primary address per owner (UX_Addresses_{Owner}_Primary);
///   - the region belongs to the country (composite FK to CountryRegions).
/// </summary>
public class AddressConfiguration : IEntityTypeConfiguration<Address>
{
    /// <summary>The owner columns (E2 added StaffProfileId). The CHECK below is built from this list.</summary>
    internal static readonly string[] OwnerColumns = { "StudentId", "ParentId", "AdmissionId", "StaffProfileId" };

    public void Configure(EntityTypeBuilder<Address> builder)
    {
        builder.ToTable("Addresses", t =>
        {
            t.HasCheckConstraint("CK_Addresses_ExactlyOneOwner",
                string.Join(" + ", OwnerColumns.Select(c => $"(CASE WHEN [{c}] IS NULL THEN 0 ELSE 1 END)")) + " = 1");
            t.HasCheckConstraint("CK_Addresses_AddressType", CheckSql.In("AddressType", AddressTypes.All));
            t.HasCheckConstraint("CK_Addresses_Line1NotBlank", "LEN(LTRIM(RTRIM([Line1]))) > 0");
            t.HasCheckConstraint("CK_Addresses_CityNotBlank", "LEN(LTRIM(RTRIM([City]))) > 0");
        });

        builder.HasKey(a => a.Id);

        builder.Property(a => a.AddressType).HasMaxLength(20).IsRequired();
        builder.Property(a => a.IsPrimary).IsRequired().HasDefaultValue(false);
        builder.Property(a => a.Line1).HasMaxLength(200).IsRequired();
        builder.Property(a => a.Line2).HasMaxLength(200);
        builder.Property(a => a.City).HasMaxLength(60).IsRequired();
        builder.Property(a => a.PostalCode).HasMaxLength(12);

        builder.Property(a => a.CreatedAt).HasDefaultValueSql("SYSUTCDATETIME()");
        builder.Property(a => a.UpdatedAt).HasDefaultValueSql("SYSUTCDATETIME()");

        // Owners are soft-deleted, so in practice these cascades only run when a test or a purge
        // really deletes an owner row; the addresses go with it.
        builder.HasOne(a => a.Student).WithMany(s => s.Addresses)
            .HasForeignKey(a => a.StudentId).OnDelete(DeleteBehavior.Cascade);
        builder.HasOne(a => a.Parent).WithMany(p => p.Addresses)
            .HasForeignKey(a => a.ParentId).OnDelete(DeleteBehavior.Cascade);
        builder.HasOne(a => a.Admission).WithMany(ad => ad.Addresses)
            .HasForeignKey(a => a.AdmissionId).OnDelete(DeleteBehavior.Cascade);
        builder.HasOne(a => a.StaffProfile).WithMany(s => s.Addresses)
            .HasForeignKey(a => a.StaffProfileId).OnDelete(DeleteBehavior.Cascade);

        builder.HasOne(a => a.Country).WithMany()
            .HasForeignKey(a => a.CountryId).OnDelete(DeleteBehavior.Restrict);

        // (CountryId, RegionId) -> CountryRegions (CountryId, Id): a UAE address cannot point at an
        // Indian state. When RegionId is NULL the database does not check this key (as intended).
        builder.HasOne(a => a.Region).WithMany()
            .HasForeignKey(a => new { a.CountryId, a.RegionId })
            .HasPrincipalKey(r => new { r.CountryId, r.Id })
            .OnDelete(DeleteBehavior.Restrict)
            .HasConstraintName("FK_Addresses_CountryRegions_CountryId_RegionId");

        // One filtered unique index per owner and rule. The (owner, type) index also serves
        // "load this owner's addresses", so no separate index on the owner columns is needed.
        AddOwnerIndexes(builder, "StudentId", "Student");
        AddOwnerIndexes(builder, "ParentId", "Parent");
        AddOwnerIndexes(builder, "AdmissionId", "Admission");
        AddOwnerIndexes(builder, "StaffProfileId", "Staff");
    }

    private static void AddOwnerIndexes(EntityTypeBuilder<Address> builder, string column, string owner)
    {
        builder.HasIndex(column, nameof(Address.AddressType))
            .IsUnique()
            .HasFilter($"[{column}] IS NOT NULL")
            .HasDatabaseName($"UX_Addresses_{owner}_Type");

        builder.HasIndex(column)
            .IsUnique()
            .HasFilter($"[{column}] IS NOT NULL AND [IsPrimary] = 1")
            .HasDatabaseName($"UX_Addresses_{owner}_Primary");
    }
}
