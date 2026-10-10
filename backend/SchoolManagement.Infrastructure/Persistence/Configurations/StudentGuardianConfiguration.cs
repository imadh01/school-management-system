using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;
using SchoolManagement.Domain.Entities;

namespace SchoolManagement.Infrastructure.Persistence.Configurations;

public class StudentGuardianConfiguration : IEntityTypeConfiguration<StudentGuardian>
{
    public void Configure(EntityTypeBuilder<StudentGuardian> builder)
    {
        builder.ToTable("StudentGuardians", t =>
            t.HasCheckConstraint("CK_StudentGuardians_RelationType",
                "[RelationType] IN ('Father','Mother','Guardian')"));
        builder.HasKey(sg => new { sg.StudentId, sg.ParentId });

        builder.Property(sg => sg.RelationType).HasMaxLength(30).IsRequired();
        builder.Property(sg => sg.CreatedAt).HasDefaultValueSql("SYSUTCDATETIME()");
        builder.Property(sg => sg.UpdatedAt).HasDefaultValueSql("SYSUTCDATETIME()");

        // At most one primary contact per student.
        builder.HasIndex(sg => sg.StudentId)
            .IsUnique()
            .HasFilter("[IsPrimaryContact] = 1")
            .HasDatabaseName("UX_StudentGuardians_Student_Primary");

        builder.HasOne(sg => sg.Student)
            .WithMany()
            .HasForeignKey(sg => sg.StudentId)
            .OnDelete(DeleteBehavior.Cascade);

        builder.HasOne(sg => sg.Parent)
            .WithMany(p => p.StudentGuardians)
            .HasForeignKey(sg => sg.ParentId)
            .OnDelete(DeleteBehavior.Cascade);

        builder.HasQueryFilter(sg => !sg.Student.IsDeleted && !sg.Parent.IsDeleted);
    }
}
