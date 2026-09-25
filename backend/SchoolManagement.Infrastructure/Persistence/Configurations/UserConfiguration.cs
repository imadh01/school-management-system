using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;
using SchoolManagement.Domain.Entities;

namespace SchoolManagement.Infrastructure.Persistence.Configurations;

public class UserConfiguration : IEntityTypeConfiguration<User>
{
    public void Configure(EntityTypeBuilder<User> builder)
    {
        builder.ToTable("Users", tb =>
            tb.HasCheckConstraint("CK_Users_Status", "[Status] IN ('Active', 'Inactive', 'Suspended')"));

        builder.HasKey(u => u.Id);

        builder.Property(u => u.Username)
            .HasMaxLength(50)
            .IsRequired();
        builder.HasIndex(u => u.Username).IsUnique();

        builder.Property(u => u.Email)
            .HasMaxLength(100)
            .IsRequired();
        builder.HasIndex(u => u.Email).IsUnique();

        builder.Property(u => u.PasswordHash)
            .IsRequired();

        builder.Property(u => u.Status)
            .HasMaxLength(20)
            .IsRequired()
            .HasDefaultValue("Active");

        builder.Property(u => u.CreatedAt).IsRequired();
        builder.Property(u => u.UpdatedAt).IsRequired();

        builder.Property(u => u.IsDeleted)
            .IsRequired()
            .HasDefaultValue(false);

        // Self-referencing audit FKs — NoAction to avoid SQL Server's
        // "multiple cascade paths" error with three self-FKs on one table.
        builder.HasOne<User>()
            .WithMany()
            .HasForeignKey(u => u.CreatedBy)
            .OnDelete(DeleteBehavior.NoAction);

        builder.HasOne<User>()
            .WithMany()
            .HasForeignKey(u => u.UpdatedBy)
            .OnDelete(DeleteBehavior.NoAction);

        builder.HasOne<User>()
            .WithMany()
            .HasForeignKey(u => u.DeletedBy)
            .OnDelete(DeleteBehavior.NoAction);

        // Soft delete enforced here, once, not repeated in every query later.
        builder.HasQueryFilter(u => !u.IsDeleted);
    }
}