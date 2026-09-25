using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;
using SchoolManagement.Domain.Entities;

namespace SchoolManagement.Infrastructure.Persistence.Configurations;

public class AcademicYearConfiguration : IEntityTypeConfiguration<AcademicYear>
{
    public void Configure(EntityTypeBuilder<AcademicYear> builder)
    {
        builder.ToTable("AcademicYears");

        builder.HasKey(a => a.Id);

        builder.Property(a => a.Name).HasMaxLength(20).IsRequired();
        builder.HasIndex(a => a.Name).IsUnique();

        builder.Property(a => a.Status).HasMaxLength(20).IsRequired().HasDefaultValue("Upcoming");

        builder.HasIndex(a => a.IsCurrent)
            .IsUnique()
            .HasFilter("[IsCurrent] = 1");

        builder.Property(a => a.CreatedAt).IsRequired();
        builder.Property(a => a.UpdatedAt).IsRequired();
    }
}
