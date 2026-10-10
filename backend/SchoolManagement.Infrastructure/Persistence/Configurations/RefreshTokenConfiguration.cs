using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;
using SchoolManagement.Domain.Entities;

namespace SchoolManagement.Infrastructure.Persistence.Configurations;

public class RefreshTokenConfiguration : IEntityTypeConfiguration<RefreshToken>
{
    public void Configure(EntityTypeBuilder<RefreshToken> builder)
    {
        builder.ToTable("RefreshTokens", t =>
        {
            t.HasCheckConstraint("CK_RefreshTokens_RevokedReason",
                "[RevokedReason] IS NULL OR " + CheckSql.In("RevokedReason", RefreshTokenRevokeReasons.All));

            // A revoked token always says why; an active one has neither value.
            t.HasCheckConstraint("CK_RefreshTokens_Revoked",
                "([RevokedAt] IS NULL AND [RevokedReason] IS NULL) OR ([RevokedAt] IS NOT NULL AND [RevokedReason] IS NOT NULL)");

            // The idle expiry is after issue and never beyond the family's absolute expiry.
            // The service relies on this: checking ExpiresAt alone covers both lifetimes.
            t.HasCheckConstraint("CK_RefreshTokens_Expiry",
                "[ExpiresAt] > [IssuedAt] AND [ExpiresAt] <= [FamilyExpiresAt]");
        });

        builder.HasKey(t => t.Id);

        builder.Property(t => t.TokenHash)
            .HasMaxLength(RefreshToken.TokenHashLength)
            .IsFixedLength()                       // binary(32), not varbinary
            .IsRequired();

        builder.Property(t => t.RevokedReason)
            .HasMaxLength(RefreshTokenRevokeReasons.MaxLength)
            .IsUnicode(false);

        builder.Property(t => t.CreatedByIp)
            .HasMaxLength(RefreshToken.MaxIpAddressLength)
            .IsUnicode(false);

        builder.Property(t => t.UserAgent)
            .HasMaxLength(RefreshToken.MaxUserAgentLength);

        // Every /refresh and /logout: find the token by its hash.
        builder.HasIndex(t => t.TokenHash)
            .IsUnique()
            .HasDatabaseName("UX_RefreshTokens_TokenHash");

        // Reuse detection and logout: revoke a whole family.
        builder.HasIndex(t => t.FamilyId)
            .HasDatabaseName("IX_RefreshTokens_FamilyId");

        // Logout-all and password change: revoke a user's live sessions. Filtered, because
        // revoked rows (the large majority) are never looked up by user.
        builder.HasIndex(t => t.UserId)
            .HasFilter("[RevokedAt] IS NULL")
            .HasDatabaseName("IX_RefreshTokens_UserId_Active");

        // Daily cleanup: delete families that ended long ago.
        builder.HasIndex(t => t.FamilyExpiresAt)
            .HasDatabaseName("IX_RefreshTokens_FamilyExpiresAt");

        // NoAction: users are only ever soft-deleted, and the token rows are purged on their own schedule.
        builder.HasOne<User>()
            .WithMany()
            .HasForeignKey(t => t.UserId)
            .OnDelete(DeleteBehavior.NoAction);

        // Rotation chain inside a family. Cleanup deletes a whole family in one statement,
        // so these self-references never block a delete.
        builder.HasOne<RefreshToken>()
            .WithMany()
            .HasForeignKey(t => t.ReplacedByTokenId)
            .OnDelete(DeleteBehavior.NoAction);
    }
}
