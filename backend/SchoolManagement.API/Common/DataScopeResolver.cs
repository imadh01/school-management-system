using SchoolManagement.Application.Interfaces;
using SchoolManagement.Domain.Exceptions;

namespace SchoolManagement.API.Common;

/// <summary>
/// Turns the caller's permissions (from the permission cache, never the JWT) into a DataScope.
/// Resolved per request; nothing is cached here, so a role change applies on the next request.
/// </summary>
public sealed class DataScopeResolver : IDataScopeResolver
{
    private readonly ICurrentUserPermissions _permissions;
    private readonly ICurrentUser _currentUser;

    public DataScopeResolver(ICurrentUserPermissions permissions, ICurrentUser currentUser)
    {
        _permissions = permissions;
        _currentUser = currentUser;
    }

    public async Task<DataScope> ResolveAsync(
        string fullPermission, string scopedPermission, CancellationToken cancellationToken)
    {
        // Full View wins when a role holds both.
        if (await _permissions.HasPermissionAsync(fullPermission, cancellationToken))
            return DataScope.Unrestricted;

        if (_currentUser.UserId is int userId &&
            await _permissions.HasPermissionAsync(scopedPermission, cancellationToken))
            return DataScope.RelatedTo(userId);

        // The endpoint's "X.Read" policy normally refuses this caller before we get here.
        throw new ForbiddenException("You do not have permission to view this.");
    }
}
