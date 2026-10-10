using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;
using SchoolManagement.Domain.Entities;

namespace SchoolManagement.Infrastructure.Persistence.Configurations;

public class StudentConfiguration : IEntityTypeConfiguration<Student>
{
    public void Configure(EntityTypeBuilder<Student> builder)
    {
        builder.ToTable("Students", t =>
        {
            t.HasCheckConstraint("CK_Students_Status", CheckSql.In("Status", StudentStatuses.All));
            t.HasCheckConstraint("CK_Students_AdmissionType", CheckSql.In("AdmissionType", AdmissionTypes.All));
            t.HasCheckConstraint("CK_Students_FeeConcession",
                "[FeeConcessionPercent] IS NULL OR ([FeeConcessionPercent] >= 0 AND [FeeConcessionPercent] <= 100)");
        });
        builder.HasKey(s => s.Id);
        builder.Property(e => e.RowVersion).IsRowVersion(); // optimistic concurrency

        builder.Property(s => s.AdmNo).HasMaxLength(30).IsRequired();
        builder.HasIndex(s => s.AdmNo).IsUnique();

        builder.Property(s => s.RollNumber).HasMaxLength(20).IsRequired();
        // Roll numbers are unique among ACTIVE enrollments (UX_StudentEnrollments_Section_Roll_Active).
        // This table only mirrors the current class and roll, so it must not enforce uniqueness itself:
        // a student who left keeps their last roll here. This index serves "students of a class" lookups.
        builder.HasIndex(s => new { s.ClassSectionId, s.Status }).HasDatabaseName("IX_Students_ClassSectionId_Status");

        // One login per student.
        builder.HasIndex(s => s.UserId)
            .IsUnique()
            .HasFilter("[UserId] IS NOT NULL AND [IsDeleted] = 0")
            .HasDatabaseName("UX_Students_UserId");

        builder.Property(s => s.Status).HasMaxLength(20).IsRequired().HasDefaultValue("Active");
        builder.Property(s => s.PhotoUrl).HasMaxLength(500);

        builder.Property(s => s.FirstName).HasMaxLength(50).IsRequired();
        builder.Property(s => s.MiddleName).HasMaxLength(50);
        builder.Property(s => s.LastName).HasMaxLength(50).IsRequired();
        builder.Property(s => s.Gender).HasMaxLength(10).IsRequired();

        builder.Property(s => s.Mobile).HasMaxLength(20);
        builder.Property(s => s.Email).HasMaxLength(100);
        builder.Property(s => s.AddressLine).HasMaxLength(200);
        builder.Property(s => s.City).HasMaxLength(50);
        builder.Property(s => s.State).HasMaxLength(50);
        builder.Property(s => s.Pincode).HasMaxLength(10);

        builder.Property(s => s.Category).HasMaxLength(FieldLimits.Category).IsRequired().HasDefaultValue("General");
        builder.Property(s => s.Religion).HasMaxLength(50);
        builder.Property(s => s.PreviousSchool).HasMaxLength(100);
        builder.Property(s => s.TransportRequired).IsRequired().HasDefaultValue(false);
        builder.Property(s => s.TransportRoute).HasMaxLength(100);

        builder.Property(s => s.Nationality).HasMaxLength(50);
        builder.Property(s => s.SecondNationality).HasMaxLength(50);
        builder.Property(s => s.CountryOfBirth).HasMaxLength(50);
        builder.Property(s => s.PreferredName).HasMaxLength(50);

        builder.Property(s => s.MotherTongue).HasMaxLength(50);
        builder.Property(s => s.HomeLanguage).HasMaxLength(50);
        builder.Property(s => s.EnglishProficiency).HasMaxLength(30);
        builder.Property(s => s.CurriculumTrack).HasMaxLength(50);
        builder.Property(s => s.AdmissionType).HasMaxLength(FieldLimits.AdmissionType).IsRequired().HasDefaultValue(AdmissionTypes.New);

        builder.Property(s => s.CustodyArrangement).HasMaxLength(20);
        builder.Property(s => s.MediaConsent).IsRequired().HasDefaultValue(true);

        builder.Property(s => s.House).HasMaxLength(30);
        builder.Property(s => s.EalCode).HasMaxLength(30);

        builder.Property(s => s.FeeConcessionPercent).HasColumnType("decimal(5,2)");

        builder.HasOne(s => s.ClassSection)
            .WithMany()
            .HasForeignKey(s => s.ClassSectionId)
            .OnDelete(DeleteBehavior.Restrict);

        builder.HasOne(s => s.Admission)
            .WithOne(a => a.Student)
            .HasForeignKey<Student>(s => s.AdmissionId)
            .OnDelete(DeleteBehavior.Restrict);

        builder.HasOne(s => s.User)
            .WithMany()
            .HasForeignKey(s => s.UserId)
            .OnDelete(DeleteBehavior.Restrict);

        builder.Property(s => s.CreatedAt).IsRequired();
        builder.Property(s => s.UpdatedAt).IsRequired();
        builder.Property(s => s.IsDeleted).IsRequired().HasDefaultValue(false);

        builder.HasQueryFilter(s => !s.IsDeleted);
    }
}