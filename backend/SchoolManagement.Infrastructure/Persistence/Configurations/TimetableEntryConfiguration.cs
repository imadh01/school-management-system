using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;
using SchoolManagement.Domain.Entities;

namespace SchoolManagement.Infrastructure.Persistence.Configurations;

public class TimetableEntryConfiguration : IEntityTypeConfiguration<TimetableEntry>
{
    public void Configure(EntityTypeBuilder<TimetableEntry> builder)
    {
        builder.ToTable("TimetableEntries", t =>
        {
            t.HasCheckConstraint("CK_TimetableEntries_DayOfWeek", "[DayOfWeek] BETWEEN 0 AND 6");
            t.HasCheckConstraint("CK_TimetableEntries_RoomNotBlank", "[Room] IS NULL OR LEN(LTRIM(RTRIM([Room]))) > 0");
        });

        builder.HasKey(e => e.Id);
        builder.Property(e => e.RowVersion).IsRowVersion();

        builder.Property(e => e.DayOfWeek).HasConversion<byte>().HasColumnType("tinyint").IsRequired();
        builder.Property(e => e.Room).HasMaxLength(30);

        // One live cell per class, day and period.
        builder.HasIndex(e => new { e.ClassSectionId, e.DayOfWeek, e.PeriodId })
            .IsUnique()
            .HasFilter("[IsDeleted] = 0")
            .HasDatabaseName("UX_TimetableEntries_Class_Day_Period");

        // The teacher-clash check: "which cells does this subject (so this teacher) already have on this day and period?"
        builder.HasIndex(e => new { e.SubjectId, e.DayOfWeek, e.PeriodId })
            .HasFilter("[IsDeleted] = 0")
            .HasDatabaseName("IX_TimetableEntries_Subject_Day_Period");

        // Restrict everywhere: nothing is removed by cascade; the services refuse the delete with a clear message.
        builder.HasOne(e => e.ClassSection)
            .WithMany()
            .HasForeignKey(e => e.ClassSectionId)
            .OnDelete(DeleteBehavior.Restrict);

        builder.HasOne(e => e.Period)
            .WithMany()
            .HasForeignKey(e => e.PeriodId)
            .OnDelete(DeleteBehavior.Restrict);

        builder.HasOne(e => e.Subject)
            .WithMany()
            .HasForeignKey(e => e.SubjectId)
            .OnDelete(DeleteBehavior.Restrict);

        builder.Property(e => e.CreatedAt).IsRequired();
        builder.Property(e => e.UpdatedAt).IsRequired();
        builder.Property(e => e.IsDeleted).IsRequired().HasDefaultValue(false);

        // Same rule as Subject: a required navigation to a soft-deletable entity must be filtered here too.
        builder.HasQueryFilter(e => !e.IsDeleted && !e.ClassSection.IsDeleted && !e.Period.IsDeleted && !e.Subject.IsDeleted);
    }
}
