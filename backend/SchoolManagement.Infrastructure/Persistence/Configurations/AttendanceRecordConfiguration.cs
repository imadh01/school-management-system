using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;
using SchoolManagement.Domain.Entities;

namespace SchoolManagement.Infrastructure.Persistence.Configurations;

public class AttendanceRecordConfiguration : IEntityTypeConfiguration<AttendanceRecord>
{
    public void Configure(EntityTypeBuilder<AttendanceRecord> builder)
    {
        builder.ToTable("AttendanceRecords", t => t.HasCheckConstraint(
            "CK_AttendanceRecords_Status",
            "[Status] IN ('Present','Absent','Late','Half Day','Leave')"));
        builder.HasKey(r => r.Id);

        builder.Property(r => r.Status).HasMaxLength(10).IsRequired();
        builder.Property(r => r.Remarks).HasMaxLength(250);

        builder.HasOne(r => r.Session)
            .WithMany(s => s.Records)
            .HasForeignKey(r => r.SessionId)
            .OnDelete(DeleteBehavior.Cascade);

        builder.HasOne(r => r.Student)
            .WithMany()
            .HasForeignKey(r => r.StudentId)
            .OnDelete(DeleteBehavior.Restrict);

        // A student appears once per session.
        builder.HasIndex(r => new { r.SessionId, r.StudentId }).IsUnique();

        // Student history, last-7 dots and percentage queries.
        builder.HasIndex(r => new { r.StudentId, r.SessionId });

        builder.HasQueryFilter(r => !r.Student.IsDeleted && !r.Session.ClassSection.IsDeleted);
    }
}
