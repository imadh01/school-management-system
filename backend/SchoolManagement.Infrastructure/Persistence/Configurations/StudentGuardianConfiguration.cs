using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;
using SchoolManagement.Domain.Entities;

namespace SchoolManagement.Infrastructure.Persistence.Configurations;

public class StudentGuardianConfiguration : IEntityTypeConfiguration<StudentGuardian>
{
    public void Configure(EntityTypeBuilder<StudentGuardian> builder)
    {
        builder.ToTable("StudentGuardians");
        builder.HasKey(sg => new { sg.StudentId, sg.ParentId });

        builder.Property(sg => sg.RelationType).HasMaxLength(30).IsRequired();

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
