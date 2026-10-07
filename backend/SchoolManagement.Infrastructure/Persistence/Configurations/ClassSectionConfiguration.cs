using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;
using SchoolManagement.Domain.Entities;

namespace SchoolManagement.Infrastructure.Persistence.Configurations;

public class ClassSectionConfiguration : IEntityTypeConfiguration<ClassSection>
{
    public void Configure(EntityTypeBuilder<ClassSection> builder)
    {
        builder.ToTable("ClassSections", t =>
            t.HasCheckConstraint("CK_ClassSections_Floor", "[Floor] IS NULL OR [Floor] >= 0"));

        builder.HasKey(c => c.Id);

        builder.Property(c => c.Name).HasMaxLength(50).IsRequired();
        builder.Property(c => c.Section).HasMaxLength(10).IsRequired();
        builder.Property(c => c.Stage).HasMaxLength(20).IsRequired().HasDefaultValue("Primary");
        builder.Property(c => c.Medium).HasMaxLength(20).IsRequired().HasDefaultValue("English");
        builder.Property(c => c.Stream).HasMaxLength(20).IsRequired().HasDefaultValue("General");
        builder.Property(c => c.Building).HasMaxLength(50);
        builder.Property(c => c.Room).HasMaxLength(30);
        builder.Property(c => c.Status).HasMaxLength(20).IsRequired().HasDefaultValue("Active");

        // Filtered so a soft-deleted section doesn't block re-creating the same name+section.
        builder.HasIndex(c => new { c.AcademicYearId, c.Name, c.Section })
            .IsUnique()
            .HasFilter("[IsDeleted] = 0");

        builder.HasOne(c => c.AcademicYear)
            .WithMany(a => a.ClassSections)
            .HasForeignKey(c => c.AcademicYearId)
            .OnDelete(DeleteBehavior.Restrict);

        builder.HasOne(c => c.ClassTeacher)
            .WithMany()
            .HasForeignKey(c => c.ClassTeacherId)
            .OnDelete(DeleteBehavior.Restrict);
        builder.HasIndex(c => c.ClassTeacherId);

        builder.Property(c => c.CreatedAt).IsRequired();
        builder.Property(c => c.UpdatedAt).IsRequired();
        builder.Property(c => c.IsDeleted).IsRequired().HasDefaultValue(false);

        builder.HasQueryFilter(c => !c.IsDeleted);

        // Computed properties, not real columns
        builder.Ignore(c => c.DisplayName);
        builder.Ignore(c => c.Code);
    }
}