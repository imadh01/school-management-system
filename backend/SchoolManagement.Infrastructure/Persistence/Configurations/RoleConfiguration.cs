using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;
using SchoolManagement.Domain.Entities;

namespace SchoolManagement.Infrastructure.Persistence.Configurations;

public class RoleConfiguration : IEntityTypeConfiguration<Role>
{
    public void Configure(EntityTypeBuilder<Role> builder)
    {
        builder.ToTable("Roles");

        builder.HasKey(r => r.Id);

        builder.Property(r => r.Name)
            .HasMaxLength(30)
            .IsRequired();
        builder.HasIndex(r => r.Name).IsUnique();

        builder.Property(r => r.Description)
            .HasMaxLength(200);

        // E1: system flag, active flag, audit columns, optimistic concurrency.
        builder.Property(r => r.IsSystem).IsRequired().HasDefaultValue(false);
        builder.Property(r => r.IsActive).IsRequired().HasDefaultValue(true);
        builder.Property(r => r.CreatedAt).IsRequired();
        builder.Property(r => r.UpdatedAt).IsRequired();
        builder.Property(r => r.RowVersion).IsRowVersion();

        // Roles are runtime data (admins edit them through the Roles API), so they are NOT seeded
        // with HasData: a later migration must never overwrite an admin's changes. The six system
        // roles were inserted by the first migration and are flagged IsSystem by the E1 migration.
    }
}