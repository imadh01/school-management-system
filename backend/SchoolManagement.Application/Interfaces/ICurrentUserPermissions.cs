namespace SchoolManagement.Application.Interfaces;

/// <summary>
/// "May the caller do X?" for decisions made INSIDE an action (masking data, widening a scope),
/// where an [Authorize(Policy = ...)] attribute can't express the rule.
///
/// The JWT carries no permissions or roles (D1), so never read them from claims
/// (User.HasClaim / User.IsInRole always return false). Ask this service instead: it reads the
/// same permission cache as the authorization handler, including the "Admin holds the whole
/// catalog" rule, so both paths always agree.
/// </summary>
public interface ICurrentUserPermissions
{
    /// <summary>False when nobody is signed in.</summary>
    Task<bool> HasPermissionAsync(string permission, CancellationToken cancellationToken = default);

    /// <summary>False when nobody is signed in.</summary>
    Task<bool> IsInRoleAsync(string roleName, CancellationToken cancellationToken = default);

    /// <summary>Every permission the caller holds (the whole catalog for Admin). Empty when nobody is signed in.</summary>
    Task<IReadOnlySet<string>> GetPermissionsAsync(CancellationToken cancellationToken = default);
}
