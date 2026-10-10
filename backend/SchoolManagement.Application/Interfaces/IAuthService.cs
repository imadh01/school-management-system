using SchoolManagement.Application.DTOs.Auth;

namespace SchoolManagement.Application.Interfaces;

public interface IAuthService
{
    /// <summary>
    /// Null return means invalid credentials — the service doesn't
    /// distinguish "wrong username" from "wrong password" from "locked out"
    /// in its result, so the API layer can't leak which one was wrong.
    /// On success, starts a new refresh-token family (one per login/device).
    /// </summary>
    Task<AuthSession?> LoginAsync(LoginRequest request, ClientInfo client, CancellationToken cancellationToken);

    /// <summary>
    /// Exchanges a refresh token for a new access token and a rotated refresh token.
    /// Null for every failure (missing, unknown, expired, revoked, reused, user no longer
    /// allowed) so the API returns one indistinguishable 401.
    /// </summary>
    Task<AuthSession?> RefreshAsync(string? rawRefreshToken, ClientInfo client, CancellationToken cancellationToken);

    /// <summary>Ends this device's session (its whole token family). Idempotent.</summary>
    Task LogoutAsync(string? rawRefreshToken, CancellationToken cancellationToken);

    /// <summary>
    /// "Sign out everywhere": rotates the user's SecurityStamp (access tokens die immediately)
    /// and revokes every refresh token they hold.
    /// </summary>
    Task LogoutAllAsync(int userId, CancellationToken cancellationToken);

    /// <summary>Returns the current user's profile plus their resolved roles and permissions.</summary>
    Task<CurrentUserResponse?> GetCurrentUserAsync(int userId, CancellationToken cancellationToken);

    /// <summary>
    /// Changes the calling user's own password. Clears MustChangePassword, rotates
    /// SecurityStamp, signs out every other device and returns a fresh session for this one.
    /// Throws BusinessRuleException if the current password is wrong.
    /// </summary>
    Task<AuthSession> ChangePasswordAsync(
        int userId, ChangePasswordRequest request, ClientInfo client, CancellationToken cancellationToken);

    /// <summary>Clears lockout for a user. Requires Users.Create permission (admin).</summary>
    Task UnlockUserAsync(int userId, CancellationToken cancellationToken);
}
