using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;
using SchoolManagement.Domain.Entities;

namespace SchoolManagement.Infrastructure.Persistence.Configurations;

public class ClassSectionConfiguration : IEntityTypeConfiguration<ClassSection>
{
    public void Configure(EntityTypeBuilder<ClassSection> builder)
    {
        builder.ToTable("ClassSections");

        builder.HasKey(c => c.Id);

        builder.Property(c => c.Name).HasMaxLength(50).IsRequired();
        builder.Property(c => c.Section).HasMaxLength(10).IsRequired();
        builder.Property(c => c.Room).HasMaxLength(30);
        builder.Property(c => c.Status).HasMaxLength(20).IsRequired().HasDefaultValue("Active");

        builder.HasIndex(c => new { c.AcademicYearId, c.Name, c.Section }).IsUnique();

        builder.HasOne(c => c.AcademicYear)
            .WithMany(a => a.ClassSections)
            .HasForeignKey(c => c.AcademicYearId)
            .OnDelete(DeleteBehavior.Restrict); // don't let a year deletion cascade-wipe class data

        builder.Property(c => c.CreatedAt).IsRequired();
        builder.Property(c => c.UpdatedAt).IsRequired();
        builder.Property(c => c.IsDeleted).IsRequired().HasDefaultValue(false);

        builder.HasQueryFilter(c => !c.IsDeleted);

        // Computed property, not a real column
        builder.Ignore(c => c.DisplayName);
    }
}
