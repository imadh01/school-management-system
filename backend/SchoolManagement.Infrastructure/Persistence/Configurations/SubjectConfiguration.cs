using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;
using SchoolManagement.Domain.Entities;

namespace SchoolManagement.Infrastructure.Persistence.Configurations;

public class SubjectConfiguration : IEntityTypeConfiguration<Subject>
{
    public void Configure(EntityTypeBuilder<Subject> builder)
    {
        builder.ToTable("Subjects", t =>
        {
            t.HasCheckConstraint(
                "CK_Subjects_MarksByType",
                "([Type] IN ('Theory','Practical') AND [MaxMarks] IS NOT NULL AND [PassMarks] IS NOT NULL " +
                " AND [TheoryMax] IS NULL AND [TheoryPass] IS NULL AND [PracticalMax] IS NULL AND [PracticalPass] IS NULL) " +
                "OR " +
                "([Type] = 'Both' AND [TheoryMax] IS NOT NULL AND [TheoryPass] IS NOT NULL " +
                " AND [PracticalMax] IS NOT NULL AND [PracticalPass] IS NOT NULL AND [MaxMarks] IS NULL AND [PassMarks] IS NULL)");
            t.HasCheckConstraint("CK_Subjects_Status", CheckSql.In("Status", ActiveStatuses.All));
            // A pass mark can never be higher than its maximum (columns that are NULL for this type are skipped).
            t.HasCheckConstraint("CK_Subjects_PassNotAboveMax",
                "([PassMarks] IS NULL OR [MaxMarks] IS NULL OR [PassMarks] <= [MaxMarks]) " +
                "AND ([TheoryPass] IS NULL OR [TheoryMax] IS NULL OR [TheoryPass] <= [TheoryMax]) " +
                "AND ([PracticalPass] IS NULL OR [PracticalMax] IS NULL OR [PracticalPass] <= [PracticalMax])");
        });

        builder.HasKey(s => s.Id);

        builder.Property(s => s.Name).HasMaxLength(100).IsRequired();
        builder.Property(s => s.Code).HasMaxLength(20).IsRequired();
        builder.Property(s => s.Type).HasMaxLength(20).IsRequired();
        builder.Property(s => s.Status).HasMaxLength(20).IsRequired().HasDefaultValue("Active");

        // Filtered so a soft-deleted subject's code doesn't block reuse of the same Code+Class.
        builder.HasIndex(s => new { s.Code, s.ClassSectionId })
            .IsUnique()
            .HasFilter("[IsDeleted] = 0");

        builder.HasOne(s => s.ClassSection)
            .WithMany()
            .HasForeignKey(s => s.ClassSectionId)
            .OnDelete(DeleteBehavior.Restrict);

        builder.HasOne(s => s.Teacher)
            .WithMany()
            .HasForeignKey(s => s.TeacherId)
            .OnDelete(DeleteBehavior.Restrict);

        builder.HasIndex(s => s.TeacherId);

        builder.Property(s => s.CreatedAt).IsRequired();
        builder.Property(s => s.UpdatedAt).IsRequired();
        builder.Property(s => s.IsDeleted).IsRequired().HasDefaultValue(false);

        // Propagate the ClassSection's own soft-delete filter — same fix as the
        // StudentGuardian/UserRole query-filter bug: a required navigation to a
        // soft-deletable entity must be filtered here too, or a deleted class
        // section's subjects silently keep appearing.
        builder.HasQueryFilter(s => !s.IsDeleted && !s.ClassSection.IsDeleted);
    }
}