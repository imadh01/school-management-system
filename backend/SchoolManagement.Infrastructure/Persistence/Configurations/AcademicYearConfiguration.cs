using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;
using SchoolManagement.Domain.Entities;

namespace SchoolManagement.Infrastructure.Persistence.Configurations;

public class AcademicYearConfiguration : IEntityTypeConfiguration<AcademicYear>
{
    public void Configure(EntityTypeBuilder<AcademicYear> builder)
    {
        builder.ToTable("AcademicYears", t =>
        {
            t.HasCheckConstraint("CK_AcademicYears_Dates", "[EndDate] > [StartDate]");
            t.HasCheckConstraint("CK_AcademicYears_Status", CheckSql.In("Status", AcademicYearStatuses.All));
            // IsCurrent is a mirror of Status = 'Current'; the two can never disagree.
            t.HasCheckConstraint("CK_AcademicYears_CurrentMirror",
                "([IsCurrent] = 1 AND [Status] = 'Current') OR ([IsCurrent] = 0 AND [Status] <> 'Current')");
        });

        builder.HasKey(a => a.Id);
        builder.Property(a => a.RowVersion).IsRowVersion();

        builder.Property(a => a.Name).HasMaxLength(20).IsRequired();
        // Live rows only, so the name of a deleted year can be used again.
        builder.HasIndex(a => a.Name)
            .IsUnique()
            .HasFilter("[IsDeleted] = 0")
            .HasDatabaseName("UX_AcademicYears_Name");

        builder.Property(a => a.Status).HasMaxLength(20).IsRequired().HasDefaultValue(AcademicYearStatuses.Upcoming);

        builder.HasIndex(a => a.IsCurrent)
            .IsUnique()
            .HasFilter("[IsCurrent] = 1");

        builder.Property(a => a.CreatedAt).IsRequired();
        builder.Property(a => a.UpdatedAt).IsRequired();
        builder.Property(a => a.IsDeleted).IsRequired().HasDefaultValue(false);

        builder.HasQueryFilter(a => !a.IsDeleted);
    }
}
