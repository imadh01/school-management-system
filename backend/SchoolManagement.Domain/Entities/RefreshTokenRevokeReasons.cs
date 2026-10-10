namespace SchoolManagement.Domain.Entities;

/// <summary>
/// Why a refresh token stopped being usable. The database CHECK constraint is generated from
/// <see cref="All"/>, so this list is the single definition.
/// </summary>
public static class RefreshTokenRevokeReasons
{
    /// <summary>Normal: the token was exchanged for a new one. Presenting it again is reuse.</summary>
    public const string Rotated = "Rotated";
    /// <summary>The user logged out on this device.</summary>
    public const string Logout = "Logout";
    /// <summary>The user chose "sign out everywhere".</summary>
    public const string LogoutAll = "LogoutAll";
    /// <summary>The user changed their password; every older session ends.</summary>
    public const string PasswordChanged = "PasswordChanged";
    /// <summary>A rotated token was presented again: possible theft, so the whole family is killed.</summary>
    public const string ReuseDetected = "ReuseDetected";
    /// <summary>The user was deactivated/deleted or their SecurityStamp changed since the token was issued.</summary>
    public const string SessionInvalidated = "SessionInvalidated";

    public static readonly string[] All =
    {
        Rotated, Logout, LogoutAll, PasswordChanged, ReuseDetected, SessionInvalidated,
    };

    /// <summary>Column width of RefreshTokens.RevokedReason.</summary>
    public const int MaxLength = 20;
}
