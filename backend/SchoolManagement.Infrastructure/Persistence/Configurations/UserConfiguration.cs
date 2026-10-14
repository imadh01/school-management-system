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

        builder.Property(u => u.Email)
            .HasMaxLength(100)
            .IsRequired();

        // Unique among NON-deleted users only (decision #7): a soft-deleted user's
        // username/email can be reused. This matches UserService's "already taken?"
        // check, which also ignores deleted users through the query filter below.
        builder.HasIndex(u => u.Username).IsUnique().HasFilter("[IsDeleted] = 0");
        builder.HasIndex(u => u.Email).IsUnique().HasFilter("[IsDeleted] = 0");

        builder.Property(u => u.PasswordHash)
            .IsRequired();

        builder.Property(u => u.Status)
            .HasMaxLength(20)
            .IsRequired()
            .HasDefaultValue("Active");

        // ── D1: Security infrastructure columns ────────────────────────
        builder.Property(u => u.SecurityStamp)
            .IsRequired()
            .HasDefaultValueSql("NEWID()");

        builder.Property(u => u.MustChangePassword)
            .IsRequired()
            .HasDefaultValue(false);

        builder.Property(u => u.LockoutEnd);

        builder.Property(u => u.AccessFailedCount)
            .IsRequired()
            .HasDefaultValue(0);

        // ── Audit ──────────────────────────────────────────────────────
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

        // E2: optimistic concurrency for the Users page (account + profile edits). App-managed version (see User.RowVersion): a concurrency token the services change on edits,
        // so sign-in bookkeeping (LastLoginAt, failed attempts) never makes an edit form stale.
        builder.Property(u => u.RowVersion).HasColumnType("binary(8)").IsConcurrencyToken().IsRequired();

        // Soft delete enforced here, once, not repeated in every query later.
        builder.HasQueryFilter(u => !u.IsDeleted);
    }
}