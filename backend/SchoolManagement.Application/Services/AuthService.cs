using SchoolManagement.Application.DTOs.Auth;
using SchoolManagement.Application.Interfaces;
using SchoolManagement.Domain.Entities;
using SchoolManagement.Domain.Exceptions;

namespace SchoolManagement.Application.Services;

public class AuthService : IAuthService
{
    private readonly IUserRepository _userRepository;
    private readonly IPasswordHasher _passwordHasher;
    private readonly IJwtTokenGenerator _tokenGenerator;
    private readonly IPermissionCacheService _permissionCache;
    private readonly IRefreshTokenService _refreshTokens;

    private const int MaxFailedAttempts = 5;
    private static readonly TimeSpan LockoutDuration = TimeSpan.FromMinutes(15);

    public AuthService(
        IUserRepository userRepository,
        IPasswordHasher passwordHasher,
        IJwtTokenGenerator tokenGenerator,
        IPermissionCacheService permissionCache,
        IRefreshTokenService refreshTokens)
    {
        _userRepository = userRepository;
        _passwordHasher = passwordHasher;
        _tokenGenerator = tokenGenerator;
        _permissionCache = permissionCache;
        _refreshTokens = refreshTokens;
    }

    public async Task<AuthSession?> LoginAsync(LoginRequest request, ClientInfo client, CancellationToken cancellationToken)
    {
        var user = await _userRepository.GetByUsernameOrEmailAsync(request.UsernameOrEmail, cancellationToken);

        // Same null for every failure path — caller can't distinguish
        // "wrong user" vs "wrong password" vs "inactive" vs "locked out".
        if (user is null)
            return null;

        if (user.Status != "Active")
            return null;

        // ── Lockout check ──────────────────────────────────────────────
        if (user.LockoutEnd.HasValue && user.LockoutEnd > DateTime.UtcNow)
            return null;

        // ── Password verification ──────────────────────────────────────
        if (!_passwordHasher.VerifyPassword(user.PasswordHash, request.Password))
        {
            user.AccessFailedCount++;
            if (user.AccessFailedCount >= MaxFailedAttempts)
            {
                user.LockoutEnd = DateTime.UtcNow.Add(LockoutDuration);
                user.AccessFailedCount = 0; // reset counter; lockout is now enforced by time
            }

            await _userRepository.SaveChangesAsync(cancellationToken);
            _permissionCache.InvalidateUser(user.Id); // invalidate AFTER commit
            return null;
        }

        // ── Successful login ───────────────────────────────────────────
        user.AccessFailedCount = 0;
        user.LockoutEnd = null;
        user.LastLoginAt = DateTime.UtcNow;

        await _userRepository.SaveChangesAsync(cancellationToken);
        _permissionCache.InvalidateUser(user.Id);

        // D2: every login is a new device session: a new refresh-token family.
        var refreshToken = await _refreshTokens.IssueAsync(user, client, cancellationToken);
        return new AuthSession(CreateAccessResult(user), refreshToken);
    }

    public async Task<AuthSession?> RefreshAsync(string? rawRefreshToken, ClientInfo client, CancellationToken cancellationToken)
    {
        // Unknown, expired, revoked and reused tokens all end here with null, so the caller
        // cannot learn which check failed. (Reuse has already revoked the family and been logged.)
        var lookup = await _refreshTokens.LookupAsync(rawRefreshToken, cancellationToken);
        if (lookup.State != RefreshTokenState.Active)
            return null;

        var current = lookup.Token!;

        // The token is valid, but is its owner still allowed in? Deleted users are filtered
        // out by the query filter (null). A different SecurityStamp means the password, roles or
        // status changed after this token was issued, so the session is over. This is the same
        // rule OnTokenValidated applies to access tokens (D1).
        // Lockout is deliberately NOT checked: it protects against password guessing, and
        // whoever holds a valid session already proved the password.
        var user = await _userRepository.GetByIdAsync(current.UserId, cancellationToken);
        if (user is null || user.Status != "Active" || user.SecurityStamp != current.SecurityStamp)
        {
            await _refreshTokens.RevokeFamilyAsync(
                current.FamilyId, RefreshTokenRevokeReasons.SessionInvalidated, cancellationToken);
            return null;
        }

        var rotated = await _refreshTokens.RotateAsync(current, user, client, cancellationToken);
        if (rotated is null)
            return null; // lost the race for this token = reuse; the family is already revoked

        return new AuthSession(CreateAccessResult(user), rotated);
    }

    public async Task LogoutAsync(string? rawRefreshToken, CancellationToken cancellationToken)
    {
        // Idempotent: a missing, unknown or already-revoked cookie is simply nothing to do.
        var lookup = await _refreshTokens.LookupAsync(rawRefreshToken, cancellationToken);
        if (lookup.State is RefreshTokenState.Active or RefreshTokenState.Expired)
        {
            await _refreshTokens.RevokeFamilyAsync(
                lookup.Token!.FamilyId, RefreshTokenRevokeReasons.Logout, cancellationToken);
        }
    }

    public async Task LogoutAllAsync(int userId, CancellationToken cancellationToken)
    {
        var user = await _userRepository.GetByIdAsync(userId, cancellationToken)
                   ?? throw new NotFoundException($"User {userId} not found.");

        // Rotating the stamp ends the ACCESS tokens on every device right now, instead of
        // leaving them working for up to 15 more minutes. "Sign out everywhere" should mean it.
        user.SecurityStamp = Guid.NewGuid();
        await _userRepository.SaveChangesAsync(cancellationToken);
        _permissionCache.InvalidateUser(userId); // invalidate AFTER commit

        // The new stamp already makes every refresh token unusable; revoking them as well
        // keeps the table truthful ("why did this session end?").
        await _refreshTokens.RevokeAllForUserAsync(userId, RefreshTokenRevokeReasons.LogoutAll, cancellationToken);
    }

    public async Task<CurrentUserResponse?> GetCurrentUserAsync(int userId, CancellationToken cancellationToken)
    {
        var info = await _permissionCache.GetUserInfoAsync(userId, cancellationToken);
        if (info is null)
            return null;

        return new CurrentUserResponse(
            userId,
            info.Username,
            info.Email,
            info.Roles,
            info.Permissions,
            info.MustChangePassword);
    }

    public async Task<AuthSession> ChangePasswordAsync(
        int userId, ChangePasswordRequest request, ClientInfo client, CancellationToken cancellationToken)
    {
        var user = await _userRepository.GetByIdAsync(userId, cancellationToken)
                   ?? throw new NotFoundException($"User {userId} not found.");

        if (!_passwordHasher.VerifyPassword(user.PasswordHash, request.CurrentPassword))
            throw new BusinessRuleException("Current password is incorrect.");

        user.PasswordHash = _passwordHasher.HashPassword(request.NewPassword);
        user.MustChangePassword = false;
        user.SecurityStamp = Guid.NewGuid(); // rotates stamp → old access AND refresh tokens rejected immediately

        await _userRepository.SaveChangesAsync(cancellationToken);
        _permissionCache.InvalidateUser(userId); // invalidate AFTER commit

        // D2: sign out every other device. Security does not depend on these two steps being in
        // the password's transaction: the new stamp above already kills every older refresh token
        // (RefreshAsync compares stamps). Revoking just records why they ended.
        await _refreshTokens.RevokeAllForUserAsync(userId, RefreshTokenRevokeReasons.PasswordChanged, cancellationToken);

        // ...and give THIS device a fresh session carrying the new stamp.
        var refreshToken = await _refreshTokens.IssueAsync(user, client, cancellationToken);
        return new AuthSession(CreateAccessResult(user), refreshToken);
    }

    public async Task UnlockUserAsync(int userId, CancellationToken cancellationToken)
    {
        var user = await _userRepository.GetByIdAsync(userId, cancellationToken)
                   ?? throw new NotFoundException($"User {userId} not found.");

        user.LockoutEnd = null;
        user.AccessFailedCount = 0;

        await _userRepository.SaveChangesAsync(cancellationToken);
        _permissionCache.InvalidateUser(userId);
    }

    /// <summary>A new short-lived access token (JWT) plus the user metadata the frontend shows.</summary>
    private AuthResult CreateAccessResult(User user)
    {
        var roles = user.UserRoles.Select(ur => ur.Role.Name).ToList();
        var (token, expiresAtUtc) = _tokenGenerator.GenerateToken(user);

        return new AuthResult(user.Id, user.Username, user.Email, roles, token, expiresAtUtc, user.MustChangePassword);
    }
}
