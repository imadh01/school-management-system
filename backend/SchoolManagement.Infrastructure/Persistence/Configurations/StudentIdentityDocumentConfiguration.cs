using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;
using SchoolManagement.Domain.Entities;

namespace SchoolManagement.Infrastructure.Persistence.Configurations;

public class StudentIdentityDocumentConfiguration : IEntityTypeConfiguration<StudentIdentityDocument>
{
    public void Configure(EntityTypeBuilder<StudentIdentityDocument> builder)
    {
        builder.ToTable("StudentIdentityDocuments", t =>
            t.HasCheckConstraint("CK_StudentIdentityDocuments_Aadhaar",
                "[AadhaarNumber] IS NULL OR (LEN([AadhaarNumber]) = 12 AND [AadhaarNumber] NOT LIKE '%[^0-9]%')"));

        builder.HasKey(d => d.StudentId); // 1:1

        builder.Property(d => d.AadhaarNumber).HasMaxLength(12);
        builder.Property(d => d.PassportNumber).HasMaxLength(30);
        builder.Property(d => d.VisaType).HasMaxLength(30);

        builder.Property(d => d.CreatedAt).HasDefaultValueSql("SYSUTCDATETIME()");
        builder.Property(d => d.UpdatedAt).HasDefaultValueSql("SYSUTCDATETIME()");

        // Optional, but unique when present (duplicate-student detection).
        builder.HasIndex(d => d.AadhaarNumber)
            .IsUnique()
            .HasFilter("[AadhaarNumber] IS NOT NULL")
            .HasDatabaseName("UX_StudentIdentityDocuments_Aadhaar");

        builder.HasOne(d => d.Student)
            .WithOne(s => s.IdentityDocument)
            .HasForeignKey<StudentIdentityDocument>(d => d.StudentId)
            .OnDelete(DeleteBehavior.Cascade);

        builder.HasQueryFilter(d => !d.Student.IsDeleted);
    }
}
