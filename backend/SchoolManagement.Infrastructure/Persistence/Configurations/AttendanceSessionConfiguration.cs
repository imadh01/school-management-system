using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;
using SchoolManagement.Domain.Entities;

namespace SchoolManagement.Infrastructure.Persistence.Configurations;

public class AttendanceSessionConfiguration : IEntityTypeConfiguration<AttendanceSession>
{
    public void Configure(EntityTypeBuilder<AttendanceSession> builder)
    {
        builder.ToTable("AttendanceSessions");
        builder.HasKey(s => s.Id);

        builder.Property(s => s.Date).HasColumnType("date").IsRequired();

        builder.HasOne(s => s.ClassSection)
            .WithMany()
            .HasForeignKey(s => s.ClassSectionId)
            .OnDelete(DeleteBehavior.Restrict);

        builder.HasOne(s => s.Subject)
            .WithMany()
            .HasForeignKey(s => s.SubjectId)
            .OnDelete(DeleteBehavior.Restrict);

        builder.HasOne<User>()
            .WithMany()
            .HasForeignKey(s => s.TakenByUserId)
            .OnDelete(DeleteBehavior.Restrict);

        // SQL Server unique indexes treat NULLs as equal, but two filtered indexes
        // state the intent explicitly: one daily session per class/date, and one
        // session per class/date/subject.
        builder.HasIndex(s => new { s.ClassSectionId, s.Date })
            .IsUnique()
            .HasFilter("[SubjectId] IS NULL")
            .HasDatabaseName("UX_AttendanceSessions_Class_Date_Daily");

        builder.HasIndex(s => new { s.ClassSectionId, s.Date, s.SubjectId })
            .IsUnique()
            .HasFilter("[SubjectId] IS NOT NULL")
            .HasDatabaseName("UX_AttendanceSessions_Class_Date_Subject");

        // "Which classes have been marked on this date?" (dashboard, reports)
        builder.HasIndex(s => s.Date);

        builder.Property(s => s.CreatedAt).IsRequired();
        builder.Property(s => s.UpdatedAt).IsRequired();

        // Attendance is never soft-deleted itself, but it must follow its class.
        builder.HasQueryFilter(s => !s.ClassSection.IsDeleted);
    }
}