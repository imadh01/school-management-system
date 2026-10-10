// File: backend/SchoolManagement.Infrastructure/Security/PermissionPolicyProvider.cs
using Microsoft.AspNetCore.Authorization;
using Microsoft.Extensions.Options;
using SchoolManagement.Domain.Constants;

namespace SchoolManagement.Infrastructure.Security;

/// <summary>
/// Convention-based policy provider: any policy name that matches a
/// permission in <see cref="Permissions.All"/> automatically generates
/// a policy with a <see cref="PermissionRequirement"/>.
///
/// Composite policies (like "Attendance.Write") and the FallbackPolicy
/// are still read from AuthorizationOptions via the default provider.
/// </summary>
public class PermissionPolicyProvider : IAuthorizationPolicyProvider
{
    private readonly DefaultAuthorizationPolicyProvider _fallback;

    public PermissionPolicyProvider(IOptions<AuthorizationOptions> options)
    {
        _fallback = new DefaultAuthorizationPolicyProvider(options);
    }

    /// <summary>
    /// .NET 8 added this property to IAuthorizationPolicyProvider.
    /// We delegate to the default provider, which returns true.
    /// This allows the authorization middleware to cache resolved policies.
    /// </summary>
    public bool AllowsCachingPolicies => _fallback.AllowsCachingPolicies;

    public Task<AuthorizationPolicy?> GetPolicyAsync(string policyName)
    {
        if (Permissions.All.Contains(policyName))
        {
            var policy = new AuthorizationPolicyBuilder()
                .AddRequirements(new PermissionRequirement(policyName))
                .Build();
            return Task.FromResult<AuthorizationPolicy?>(policy);
        }

        // Fall through to manually registered policies (Attendance.Write, etc.)
        return _fallback.GetPolicyAsync(policyName);
    }

    public Task<AuthorizationPolicy> GetDefaultPolicyAsync()
        => _fallback.GetDefaultPolicyAsync();

    public Task<AuthorizationPolicy?> GetFallbackPolicyAsync()
        => _fallback.GetFallbackPolicyAsync();
}