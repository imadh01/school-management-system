using Microsoft.AspNetCore.Mvc.Testing;
using Microsoft.AspNetCore.TestHost;
using Microsoft.Extensions.DependencyInjection;
using SchoolManagement.Application.Interfaces;
using SchoolManagement.Domain.Constants;

namespace SchoolManagement.Tests.Support;

/// <summary>
/// The database-rule tests call services directly, with nobody signed in, so "is the caller an Admin?" is false.
/// This stands in for the signed-in user so the Admin-only paths (Restore) can be tested for both answers.
/// </summary>
public sealed class FakeCurrentUserPermissions : ICurrentUserPermissions
{
    private readonly bool _isAdmin;

    public FakeCurrentUserPermissions(bool isAdmin) => _isAdmin = isAdmin;

    public Task<bool> HasPermissionAsync(string permission, CancellationToken cancellationToken = default) =>
        Task.FromResult(_isAdmin);

    public Task<bool> IsInRoleAsync(string roleName, CancellationToken cancellationToken = default) =>
        Task.FromResult(_isAdmin && roleName == RoleNames.Admin);

    public Task<IReadOnlySet<string>> GetPermissionsAsync(CancellationToken cancellationToken = default) =>
        Task.FromResult<IReadOnlySet<string>>(_isAdmin ? new HashSet<string>(Permissions.All) : new HashSet<string>());

    /// <summary>A whole test host in which every request is made by an Admin (or by nobody).</summary>
    public static WebApplicationFactory<Program> AsCaller(WebApplicationFactory<Program> factory, bool isAdmin) =>
        factory.WithWebHostBuilder(host => host.ConfigureTestServices(services =>
            services.AddScoped<ICurrentUserPermissions>(_ => new FakeCurrentUserPermissions(isAdmin))));

    /// <summary>The real service, built from the scope's registrations but with this caller instead of the HTTP one.</summary>
    public static T Build<T>(IServiceProvider scope, bool isAdmin) where T : class =>
        ActivatorUtilities.CreateInstance<T>(scope, new FakeCurrentUserPermissions(isAdmin));
}
