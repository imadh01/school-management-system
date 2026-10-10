using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;
using SchoolManagement.Domain.Entities;

namespace SchoolManagement.Infrastructure.Persistence.Configurations;

public class TeacherConfiguration : IEntityTypeConfiguration<Teacher>
{
    public void Configure(EntityTypeBuilder<Teacher> builder)
    {
        builder.ToTable("Teachers");
        builder.HasKey(t => t.Id);

        builder.Property(t => t.Name).HasMaxLength(150).IsRequired();
        builder.Property(t => t.Phone).HasMaxLength(30);
        builder.Property(t => t.Specialization).HasMaxLength(100);

        // One login per teacher profile.
        builder.HasIndex(t => t.UserId).IsUnique().HasFilter("[IsDeleted] = 0");
        builder.HasOne(t => t.User)
            .WithOne()
            .HasForeignKey<Teacher>(t => t.UserId)
            .OnDelete(DeleteBehavior.Restrict);

        builder.Property(t => t.CreatedAt).IsRequired();
        builder.Property(t => t.UpdatedAt).IsRequired();
        builder.Property(t => t.IsDeleted).IsRequired().HasDefaultValue(false);

        // Required navigation to a soft-deletable User must be filtered too.
        builder.HasQueryFilter(t => !t.IsDeleted && !t.User.IsDeleted);
    }
}
