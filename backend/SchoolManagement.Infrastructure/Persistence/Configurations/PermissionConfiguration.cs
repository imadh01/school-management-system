using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;
using SchoolManagement.Domain.Entities;

namespace SchoolManagement.Infrastructure.Persistence.Configurations;

public class PermissionConfiguration : IEntityTypeConfiguration<Permission>
{
    public void Configure(EntityTypeBuilder<Permission> builder)
    {
        builder.ToTable("Permissions");

        builder.HasKey(p => p.Id);

        builder.Property(p => p.Name)
            .HasMaxLength(100)
            .IsRequired();
        builder.HasIndex(p => p.Name).IsUnique();

        builder.Property(p => p.Module)
            .HasMaxLength(50)
            .IsRequired();
        builder.HasIndex(p => p.Module); // powers a permissions-admin screen grouped by module

        builder.Property(p => p.Description)
            .HasMaxLength(200);
    }
}