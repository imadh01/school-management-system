using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;
using SchoolManagement.Domain.Entities;

namespace SchoolManagement.Infrastructure.Persistence.Configurations;

public class ParentConfiguration : IEntityTypeConfiguration<Parent>
{
    public void Configure(EntityTypeBuilder<Parent> builder)
    {
        builder.ToTable("Parents", t => t.HasCheckConstraint("CK_Parents_Status", CheckSql.In("Status", ActiveStatuses.All)));
        builder.HasKey(p => p.Id);
        builder.Property(e => e.RowVersion).IsRowVersion(); // optimistic concurrency

        builder.Property(p => p.Name).HasMaxLength(100).IsRequired();
        builder.Property(p => p.Email).HasMaxLength(100);
        builder.Property(p => p.Mobile).HasMaxLength(20).IsRequired();

        // Persisted computed column: one definition of "same number" shared with AdmissionGuardians.
        builder.Property(p => p.MobileKey)
            .HasMaxLength(20)
            .HasComputedColumnSql(MobileKeySql.Expression("Mobile"), stored: true);
        // Deliberately NOT unique: families may share a number (matching is staff-confirmed).
        builder.HasIndex(p => p.MobileKey).HasDatabaseName("IX_Parents_MobileKey");
        builder.Property(p => p.Status).HasMaxLength(20).IsRequired().HasDefaultValue("Active");

        builder.Property(p => p.Occupation).HasMaxLength(100);
        builder.Property(p => p.Nationality).HasMaxLength(50);
        builder.Property(p => p.CountryOfResidence).HasMaxLength(50);
        builder.Property(p => p.Timezone).HasMaxLength(50);
        builder.Property(p => p.PreferredLanguage).HasMaxLength(30);
        builder.Property(p => p.PreferredContactMethod).HasMaxLength(30);
        builder.Property(p => p.Whatsapp).HasMaxLength(20);

        builder.Property(p => p.NotifyAttendance).HasDefaultValue(true);
        builder.Property(p => p.NotifyExams).HasDefaultValue(true);
        builder.Property(p => p.NotifyFees).HasDefaultValue(true);
        builder.Property(p => p.NotifyNotices).HasDefaultValue(true);
        builder.Property(p => p.NotifyDiscipline).HasDefaultValue(true);

        builder.Property(p => p.Employer).HasMaxLength(100);
        builder.Property(p => p.JobTitle).HasMaxLength(100);
        builder.Property(p => p.WorkEmail).HasMaxLength(100);
        builder.Property(p => p.WorkPhone).HasMaxLength(20);

        builder.Property(p => p.AddressLine).HasMaxLength(200);
        builder.Property(p => p.City).HasMaxLength(50);
        builder.Property(p => p.State).HasMaxLength(50);
        builder.Property(p => p.Pincode).HasMaxLength(10);

        // One login per parent.
        builder.HasIndex(p => p.UserId)
            .IsUnique()
            .HasFilter("[UserId] IS NOT NULL AND [IsDeleted] = 0")
            .HasDatabaseName("UX_Parents_UserId");

        builder.HasOne(p => p.User)
            .WithMany()
            .HasForeignKey(p => p.UserId)
            .OnDelete(DeleteBehavior.Restrict);

        builder.Property(p => p.CreatedAt).IsRequired();
        builder.Property(p => p.UpdatedAt).IsRequired();
        builder.Property(p => p.IsDeleted).IsRequired().HasDefaultValue(false);

        builder.HasQueryFilter(p => !p.IsDeleted);
    }
}