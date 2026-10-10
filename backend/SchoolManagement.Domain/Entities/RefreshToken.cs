using SchoolManagement.Domain.Auditing;

namespace SchoolManagement.Domain.Entities;

/// <summary>
/// One refresh token handed to one browser session. Every successful refresh revokes the
/// presented token (reason "Rotated") and issues a new one in the SAME family, so a family is
/// one login on one device, and at most one token in it is usable at any moment.
///
/// The raw token never touches the database: only its SHA-256 hash is stored, so a copy of this
/// table cannot be turned into working cookies.
///
/// Not soft-deleted and not audited: "revoked" is already its soft state, and a row is written on
/// every refresh (every 15 minutes per user), which would flood AuditLogs. Security events
/// (reuse detected, logout-all) go to the Serilog log instead. Old rows are purged daily.
/// </summary>
[AuditIgnore]
public class RefreshToken
{
    /// <summary>SHA-256 output size; the TokenHash column is binary(32).</summary>
    public const int TokenHashLength = 32;
    /// <summary>Longest textual IPv6 address (with an embedded IPv4 part).</summary>
    public const int MaxIpAddressLength = 45;
    public const int MaxUserAgentLength = 256;

    /// <summary>bigint: one row per refresh, so this table grows much faster than the others.</summary>
    public long Id { get; set; }

    public int UserId { get; set; }

    /// <summary>SHA-256 of the raw token. Unique; every refresh looks the token up by it.</summary>
    public byte[] TokenHash { get; set; } = Array.Empty<byte>();

    /// <summary>Shared by every token descended from one login. Reuse detection revokes the whole family.</summary>
    public Guid FamilyId { get; set; }

    /// <summary>
    /// The user's SecurityStamp when this token was issued. If the stamp has changed since
    /// (password change, role change, deactivation), the token is dead, the same rule D1 applies to access tokens.
    /// </summary>
    public Guid SecurityStamp { get; set; }

    /// <summary>
    /// UTC. Deliberately not called CreatedAt: the audit interceptor overwrites any CreatedAt
    /// column with its own clock, and this value must come from the service's TimeProvider.
    /// </summary>
    public DateTime IssuedAt { get; set; }

    /// <summary>UTC idle expiry: the session dies if not refreshed before this (7 days by default).</summary>
    public DateTime ExpiresAt { get; set; }

    /// <summary>UTC absolute expiry, fixed at login and copied forward on rotation (30 days by default).</summary>
    public DateTime FamilyExpiresAt { get; set; }

    /// <summary>Null while the token is usable.</summary>
    public DateTime? RevokedAt { get; set; }

    /// <summary>One of <see cref="RefreshTokenRevokeReasons"/>; set together with RevokedAt.</summary>
    public string? RevokedReason { get; set; }

    /// <summary>The token that replaced this one on rotation, so a family's chain can be traced.</summary>
    public long? ReplacedByTokenId { get; set; }

    /// <summary>For investigating reuse alerts and a future "active sessions" list. Never written to logs.</summary>
    public string? CreatedByIp { get; set; }

    public string? UserAgent { get; set; }
}
