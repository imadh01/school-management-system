using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;
using SchoolManagement.Domain.Entities;

namespace SchoolManagement.Infrastructure.Persistence.Configurations;

public class AdmissionConfiguration : IEntityTypeConfiguration<Admission>
{
    public void Configure(EntityTypeBuilder<Admission> builder)
    {
        builder.ToTable("Admissions");

        builder.HasKey(a => a.Id);

        builder.Property(a => a.RegNo).HasMaxLength(30).IsRequired();
        builder.HasIndex(a => a.RegNo).IsUnique();

        builder.Property(a => a.FirstName).HasMaxLength(50).IsRequired();
        builder.Property(a => a.MiddleName).HasMaxLength(50);
        builder.Property(a => a.LastName).HasMaxLength(50).IsRequired();
        builder.Property(a => a.Gender).HasMaxLength(10).IsRequired();

        builder.Property(a => a.AdmissionType).HasMaxLength(20).IsRequired();
        builder.Property(a => a.PreviousSchool).HasMaxLength(100);
        builder.Property(a => a.Phone).HasMaxLength(20).IsRequired();
        builder.Property(a => a.Email).HasMaxLength(100);

        builder.Property(a => a.Status).HasMaxLength(20).IsRequired().HasDefaultValue("Registered");
        builder.Property(a => a.RejectionReason).HasMaxLength(300);

        builder.Property(a => a.FatherName).HasMaxLength(100);
        builder.Property(a => a.FatherMobile).HasMaxLength(20);
        builder.Property(a => a.MotherName).HasMaxLength(100);
        builder.Property(a => a.MotherMobile).HasMaxLength(20);
        builder.Property(a => a.GuardianName).HasMaxLength(100);
        builder.Property(a => a.GuardianRelation).HasMaxLength(30);
        builder.Property(a => a.GuardianMobile).HasMaxLength(20);

        builder.Property(a => a.AddressLine).HasMaxLength(200);
        builder.Property(a => a.City).HasMaxLength(50);
        builder.Property(a => a.State).HasMaxLength(50);
        builder.Property(a => a.Pincode).HasMaxLength(10);

        builder.Property(a => a.AdmissionFee).HasColumnType("decimal(10,2)");
        builder.Property(a => a.AdmissionFeeReference).HasMaxLength(30);
        builder.Property(a => a.BloodGroup).HasMaxLength(10);
        builder.Property(a => a.Religion).HasMaxLength(50);
        builder.Property(a => a.Category).HasMaxLength(50);
        builder.Property(a => a.MedicalNotes).HasMaxLength(300);
        builder.Property(a => a.Remarks).HasMaxLength(300);

        builder.Property(a => a.RollNumber).HasMaxLength(20);
        builder.Property(a => a.AdmissionNumber).HasMaxLength(30);
        builder.Property(a => a.EntryPoint).HasMaxLength(50);
        builder.Property(a => a.TransportRequired).IsRequired().HasDefaultValue(false);

        builder.HasOne(a => a.AcademicYear)
            .WithMany()
            .HasForeignKey(a => a.AcademicYearId)
            .OnDelete(DeleteBehavior.Restrict);

        builder.HasOne(a => a.AppliedForClassSection)
            .WithMany()
            .HasForeignKey(a => a.AppliedForClassSectionId)
            .OnDelete(DeleteBehavior.Restrict);

        builder.HasOne(a => a.AllottedClassSection)
            .WithMany()
            .HasForeignKey(a => a.AllottedClassSectionId)
            .OnDelete(DeleteBehavior.Restrict);

        builder.Property(a => a.CreatedAt).IsRequired();
        builder.Property(a => a.UpdatedAt).IsRequired();
        builder.Property(a => a.IsDeleted).IsRequired().HasDefaultValue(false);

        builder.HasQueryFilter(a => !a.IsDeleted);

        // Composite index: the pipeline UI filters by status constantly, and
        // per-year filtering is the other dominant query pattern.
        builder.HasIndex(a => new { a.AcademicYearId, a.Status });
    }
}