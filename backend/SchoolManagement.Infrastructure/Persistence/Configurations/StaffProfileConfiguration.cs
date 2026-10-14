using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;
using SchoolManagement.Domain.Entities;

namespace SchoolManagement.Infrastructure.Persistence.Configurations;

/// <summary>E2: one staff profile per (not deleted) user; the employee number is unique when given.</summary>
public class StaffProfileConfiguration : IEntityTypeConfiguration<StaffProfile>
{
    public void Configure(EntityTypeBuilder<StaffProfile> builder)
    {
        builder.ToTable("StaffProfiles", t =>
        {
            t.HasCheckConstraint("CK_StaffProfiles_FullNameNotBlank", "LEN(LTRIM(RTRIM([FullName]))) > 0");
            t.HasCheckConstraint("CK_StaffProfiles_Gender", "[Gender] IS NULL OR " + CheckSql.In("Gender", Genders.All));
            t.HasCheckConstraint("CK_StaffProfiles_EmployeeNumberNotBlank",
                "[EmployeeNumber] IS NULL OR LEN(LTRIM(RTRIM([EmployeeNumber]))) > 0");
        });

        builder.HasKey(s => s.Id);

        builder.Property(s => s.FullName).HasMaxLength(150).IsRequired();
        builder.Property(s => s.Gender).HasMaxLength(10);
        builder.Property(s => s.Mobile).HasMaxLength(30); // Teachers.Phone is 30: the copy must not cut it
        builder.Property(s => s.Qualification).HasMaxLength(150);
        builder.Property(s => s.EmployeeNumber).HasMaxLength(30);
        builder.Property(s => s.BloodGroup).HasMaxLength(10);
        builder.Property(s => s.EmergencyContactName).HasMaxLength(150);
        builder.Property(s => s.EmergencyContactPhone).HasMaxLength(20);

        builder.Property(s => s.CreatedAt).HasDefaultValueSql("SYSUTCDATETIME()");
        builder.Property(s => s.UpdatedAt).HasDefaultValueSql("SYSUTCDATETIME()");
        builder.Property(s => s.IsDeleted).IsRequired().HasDefaultValue(false);
        builder.HasQueryFilter(s => !s.IsDeleted);

        // 1:1 with the login. Users are soft-deleted (the profile goes with them), so no cascade.
        builder.HasOne(s => s.User).WithOne(u => u.StaffProfile)
            .HasForeignKey<StaffProfile>(s => s.UserId)
            .OnDelete(DeleteBehavior.Restrict);
        builder.HasIndex(s => s.UserId).IsUnique().HasFilter("[IsDeleted] = 0")
            .HasDatabaseName("UX_StaffProfiles_UserId");

        // Decision 4: optional, unique when present (deleted staff free their number).
        builder.HasIndex(s => s.EmployeeNumber).IsUnique()
            .HasFilter("[EmployeeNumber] IS NOT NULL AND [IsDeleted] = 0")
            .HasDatabaseName("UX_StaffProfiles_EmployeeNumber");
    }
}
