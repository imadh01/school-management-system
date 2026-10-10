
using System.Collections.Concurrent;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.DependencyInjection;
using SchoolManagement.Application.Interfaces;
using SchoolManagement.Domain.Constants;
using SchoolManagement.Infrastructure.Persistence;

namespace SchoolManagement.Infrastructure.Security;

/// <summary>
/// Singleton in-memory permission cache.  Loads a user's security info
/// from the database on first access and holds it until explicitly
/// invalidated.  Callers MUST invalidate AFTER their transaction commits,
/// not before, to avoid a race where another request reloads stale data.
///
/// Admin shortcut: any user whose active roles include "Admin" is treated
/// as holding every permission in <see cref="Permissions.All"/>, so new
/// permissions added to the catalog are immediately available to Admin
/// without a migration or a startup sync.
///
/// Single-instance assumption — this is NOT distributed.
/// </summary>
public class PermissionCacheService : IPermissionCacheService
{
    private readonly IServiceScopeFactory _scopeFactory;
    private readonly ConcurrentDictionary<int, CachedUserInfo> _cache = new();

    public PermissionCacheService(IServiceScopeFactory scopeFactory)
    {
        _scopeFactory = scopeFactory;
    }

    public async Task<CachedUserInfo?> GetUserInfoAsync(int userId, CancellationToken cancellationToken = default)
    {
        if (_cache.TryGetValue(userId, out var cached))
            return cached;

        return await LoadAndCacheAsync(userId, cancellationToken);
    }

    public void InvalidateUser(int userId)
    {
        _cache.TryRemove(userId, out _);
    }

    public void InvalidateAll()
    {
        _cache.Clear();
    }

    // ────────────────────────────────────────────────────────────────────
    private async Task<CachedUserInfo?> LoadAndCacheAsync(int userId, CancellationToken ct)
    {
        using var scope = _scopeFactory.CreateScope();
        var db = scope.ServiceProvider.GetRequiredService<ApplicationDbContext>();

        // IgnoreQueryFilters so we can detect soft-deleted and inactive
        // users and reject their tokens in OnTokenValidated.
        var user = await db.Users
            .IgnoreQueryFilters()
            .Include(u => u.UserRoles)
                .ThenInclude(ur => ur.Role)
                    .ThenInclude(r => r.RolePermissions)
                        .ThenInclude(rp => rp.Permission)
            .AsNoTracking()
            .FirstOrDefaultAsync(u => u.Id == userId, ct);

        if (user is null)
            return null;

        var roleNames = user.UserRoles
            .Select(ur => ur.Role.Name)
            .ToList();

        // Admin gets the entire permission catalog automatically.
        IReadOnlyList<string> permissions;
        if (roleNames.Contains(RoleNames.Admin))
        {
            permissions = Permissions.All.ToList();
        }
        else
        {
            permissions = user.UserRoles
                .SelectMany(ur => ur.Role.RolePermissions)
                .Select(rp => rp.Permission.Name)
                .Distinct()
                .ToList();
        }

        var info = new CachedUserInfo(
            SecurityStamp: user.SecurityStamp.ToString(),
            Username: user.Username,
            Email: user.Email,
            Status: user.Status,
            IsDeleted: user.IsDeleted,
            MustChangePassword: user.MustChangePassword,
            LockoutEnd: user.LockoutEnd,
            Roles: roleNames,
            Permissions: permissions);

        // TryAdd is harmless if another thread beat us — both loaded
        // the same snapshot, so either value is correct.
        _cache.TryAdd(userId, info);
        return info;
    }
}
