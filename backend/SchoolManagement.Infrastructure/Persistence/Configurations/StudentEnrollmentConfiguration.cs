using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;
using SchoolManagement.Domain.Entities;

namespace SchoolManagement.Infrastructure.Persistence.Configurations;

public class StudentEnrollmentConfiguration : IEntityTypeConfiguration<StudentEnrollment>
{
    public void Configure(EntityTypeBuilder<StudentEnrollment> builder)
    {
        builder.ToTable("StudentEnrollments", t =>
        {
            t.HasCheckConstraint("CK_StudentEnrollments_Status",
                "[Status] IN ('Active','Promoted','Repeated','Transferred','Left')");
            // Active <=> still open (no EndDate); any closed status must have a sensible EndDate.
            t.HasCheckConstraint("CK_StudentEnrollments_EndDate",
                "([Status] = 'Active' AND [EndDate] IS NULL) " +
                "OR ([Status] <> 'Active' AND [EndDate] IS NOT NULL AND [EndDate] >= [StartDate])");
        });
        builder.HasKey(e => e.Id);

        builder.Property(e => e.RollNumber).HasMaxLength(20).IsRequired();
        builder.Property(e => e.Status).HasMaxLength(20).IsRequired().HasDefaultValue(EnrollmentStatuses.Active);
        builder.Property(e => e.Remarks).HasMaxLength(300);

        builder.Property(e => e.CreatedAt).HasDefaultValueSql("SYSUTCDATETIME()");
        builder.Property(e => e.UpdatedAt).HasDefaultValueSql("SYSUTCDATETIME()");

        // Restrict everywhere: history must never disappear because a parent row was removed.
        builder.HasOne(e => e.Student)
            .WithMany(s => s.Enrollments)
            .HasForeignKey(e => e.StudentId)
            .OnDelete(DeleteBehavior.Restrict);

        builder.HasOne(e => e.AcademicYear)
            .WithMany()
            .HasForeignKey(e => e.AcademicYearId)
            .OnDelete(DeleteBehavior.Restrict);

        builder.HasOne(e => e.ClassSection)
            .WithMany()
            .HasForeignKey(e => e.ClassSectionId)
            .OnDelete(DeleteBehavior.Restrict);

        // History page: all periods of one student, one academic year lookups. NOT unique,
        // because a mid-year class change legitimately gives a student two periods in one year.
        builder.HasIndex(e => new { e.StudentId, e.AcademicYearId })
            .HasDatabaseName("IX_StudentEnrollments_Student_Year");

        // A student has at most one current period.
        builder.HasIndex(e => e.StudentId)
            .IsUnique()
            .HasFilter("[Status] = 'Active'")
            .HasDatabaseName("UX_StudentEnrollments_Student_Active");

        // Roll numbers are unique within a section among current students only
        // (never school-wide, and past years don't block reuse).
        builder.HasIndex(e => new { e.ClassSectionId, e.RollNumber })
            .IsUnique()
            .HasFilter("[Status] = 'Active'")
            .HasDatabaseName("UX_StudentEnrollments_Section_Roll_Active");

        // Class lists and the future promotion screen: members of a section by status.
        builder.HasIndex(e => new { e.ClassSectionId, e.Status })
            .HasDatabaseName("IX_StudentEnrollments_Section_Status");

        // Same soft-delete rule as the other student child tables.
        builder.HasQueryFilter(e => !e.Student.IsDeleted);
    }
}
