using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;
using SchoolManagement.Domain.Entities;

namespace SchoolManagement.Infrastructure.Persistence.Configurations;

public class StudentPickupPersonConfiguration : IEntityTypeConfiguration<StudentPickupPerson>
{
    public void Configure(EntityTypeBuilder<StudentPickupPerson> builder)
    {
        builder.ToTable("StudentPickupPersons");
        builder.HasKey(p => p.Id);

        builder.Property(p => p.Name).HasMaxLength(100).IsRequired();
        builder.Property(p => p.Relation).HasMaxLength(50).IsRequired();
        builder.Property(p => p.Phone).HasMaxLength(20).IsRequired();
        builder.Property(p => p.IdNote).HasMaxLength(100);

        builder.Property(p => p.CreatedAt).HasDefaultValueSql("SYSUTCDATETIME()");
        builder.Property(p => p.UpdatedAt).HasDefaultValueSql("SYSUTCDATETIME()");

        builder.HasOne(p => p.Student)
            .WithMany(s => s.PickupPersons)
            .HasForeignKey(p => p.StudentId)
            .OnDelete(DeleteBehavior.Cascade);

        builder.HasIndex(p => p.StudentId).HasDatabaseName("IX_StudentPickupPersons_StudentId");

        builder.HasQueryFilter(p => !p.Student.IsDeleted);
    }
}
