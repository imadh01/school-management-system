using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;
using SchoolManagement.Domain.Entities;

namespace SchoolManagement.Infrastructure.Persistence.Configurations;

public class TimetablePeriodConfiguration : IEntityTypeConfiguration<TimetablePeriod>
{
    public void Configure(EntityTypeBuilder<TimetablePeriod> builder)
    {
        builder.ToTable("TimetablePeriods", t =>
        {
            t.HasCheckConstraint("CK_TimetablePeriods_StartBeforeEnd", "[StartTime] < [EndTime]");
            t.HasCheckConstraint("CK_TimetablePeriods_Kind", CheckSql.In("Kind", PeriodKinds.All));
            t.HasCheckConstraint("CK_TimetablePeriods_SortOrder", "[SortOrder] >= 1");
            t.HasCheckConstraint("CK_TimetablePeriods_LabelNotBlank", "LEN(LTRIM(RTRIM([Label]))) > 0");
        });

        builder.HasKey(p => p.Id);
        builder.Property(p => p.RowVersion).IsRowVersion();

        builder.Property(p => p.Label).HasMaxLength(50).IsRequired();
        builder.Property(p => p.StartTime).HasColumnType("time(0)").IsRequired();
        builder.Property(p => p.EndTime).HasColumnType("time(0)").IsRequired();
        builder.Property(p => p.Kind).HasMaxLength(20).IsRequired().HasDefaultValue(PeriodKinds.Teaching);
        builder.Property(p => p.SortOrder).IsRequired();

        // Both indexes look at live rows only, so a deleted "Period 3" does not block creating a new one.
        builder.HasIndex(p => new { p.AcademicYearId, p.SortOrder })
            .IsUnique()
            .HasFilter("[IsDeleted] = 0")
            .HasDatabaseName("UX_TimetablePeriods_Year_SortOrder");
        builder.HasIndex(p => new { p.AcademicYearId, p.Label })
            .IsUnique()
            .HasFilter("[IsDeleted] = 0")
            .HasDatabaseName("UX_TimetablePeriods_Year_Label");

        builder.HasOne(p => p.AcademicYear)
            .WithMany()
            .HasForeignKey(p => p.AcademicYearId)
            .OnDelete(DeleteBehavior.Restrict);

        builder.Property(p => p.CreatedAt).IsRequired();
        builder.Property(p => p.UpdatedAt).IsRequired();
        builder.Property(p => p.IsDeleted).IsRequired().HasDefaultValue(false);

        builder.HasQueryFilter(p => !p.IsDeleted);
    }
}
