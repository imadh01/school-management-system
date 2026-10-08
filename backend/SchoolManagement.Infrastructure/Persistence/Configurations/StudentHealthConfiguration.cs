using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;
using SchoolManagement.Domain.Entities;

namespace SchoolManagement.Infrastructure.Persistence.Configurations;

public class StudentHealthConfiguration : IEntityTypeConfiguration<StudentHealth>
{
    public void Configure(EntityTypeBuilder<StudentHealth> builder)
    {
        builder.ToTable("StudentHealth");
        builder.HasKey(h => h.StudentId); // 1:1 — the student's key is this table's key

        builder.Property(h => h.BloodGroup).HasMaxLength(10);
        builder.Property(h => h.Allergies).HasMaxLength(300);
        builder.Property(h => h.DietaryRequirements).HasMaxLength(200);
        builder.Property(h => h.MedicalNotes).HasMaxLength(500);
        builder.Property(h => h.SpecialEducationalNeeds).HasMaxLength(500);
        builder.Property(h => h.InsuranceProvider).HasMaxLength(100);

        builder.Property(h => h.CreatedAt).HasDefaultValueSql("SYSUTCDATETIME()");
        builder.Property(h => h.UpdatedAt).HasDefaultValueSql("SYSUTCDATETIME()");

        builder.HasOne(h => h.Student)
            .WithOne(s => s.Health)
            .HasForeignKey<StudentHealth>(h => h.StudentId)
            .OnDelete(DeleteBehavior.Cascade);

        builder.HasQueryFilter(h => !h.Student.IsDeleted);
    }
}
