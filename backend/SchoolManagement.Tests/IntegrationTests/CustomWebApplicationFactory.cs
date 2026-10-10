// D1: Updated — admin seed now sets SecurityStamp (required by the new
//     JWT / OnTokenValidated flow) and the factory clears the permission
//     cache between test classes to avoid stale data.
//
// D1-fix: Added PostConfigure<JwtBearerOptions> so the JWT *validation*
//         signing key matches the test signing key.  Without this, Program.cs
//         captures the real key from user secrets before the test config
//         override takes effect, causing every authenticated request to 401.
//
// D2: Turns off the daily refresh-token cleanup job, so no background
//     work touches the test database while it is dropped and re-created.

using Microsoft.AspNetCore.Authentication.JwtBearer;
using Microsoft.AspNetCore.Hosting;
using Microsoft.AspNetCore.Mvc.Testing;
using Microsoft.AspNetCore.TestHost;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.IdentityModel.Tokens;
using Microsoft.VisualStudio.TestPlatform.TestHost;
using SchoolManagement.Application.Interfaces;
using SchoolManagement.Domain.Entities;
using SchoolManagement.Infrastructure.Persistence;
using System.Text;
using Xunit;

namespace SchoolManagement.Tests.IntegrationTests;

/// <summary>
/// Boots the real API pipeline (DI, middleware, controllers) against a
/// dedicated test database, separate from your dev database, so test
/// runs never touch real data. Recreated fresh on every test run via
/// EnsureDeleted + Migrate, so tests never depend on leftover state.
/// </summary>
public class CustomWebApplicationFactory : WebApplicationFactory<Program>, IAsyncLifetime
{
    private const string TestConnectionString =
        "Server=IMADH\\SQLEXPRESS;Database=SchoolManagementDb_IntegrationTests;Trusted_Connection=True;TrustServerCertificate=True;";

    private const string TestSigningKey =
        "test-signing-key-at-least-32-characters-long!!";

    protected override void ConfigureWebHost(IWebHostBuilder builder)
    {
        builder.ConfigureAppConfiguration((_, config) =>
        {
            config.AddInMemoryCollection(new Dictionary<string, string?>
            {
                ["ConnectionStrings:DefaultConnection"] = TestConnectionString,
                ["Jwt:Issuer"] = "SchoolManagementApi",
                ["Jwt:Audience"] = "SchoolManagementClient",
                ["Jwt:SigningKey"] = TestSigningKey,
                ["Jwt:ExpiryMinutes"] = "60",
                ["RefreshToken:CleanupEnabled"] = "false",
            });
        });

        // ConfigureAppConfiguration runs during Build(), which is AFTER
        // Program.cs has already read jwtSettings from builder.Configuration.
        // That means TokenValidationParameters.IssuerSigningKey still holds
        // the real key from user secrets.  PostConfigure overwrites it with
        // the test key so token generation and validation use the same key.
        builder.ConfigureTestServices(services =>
        {
            services.PostConfigure<JwtBearerOptions>(
                JwtBearerDefaults.AuthenticationScheme, options =>
                {
                    options.TokenValidationParameters.IssuerSigningKey =
                        new SymmetricSecurityKey(Encoding.UTF8.GetBytes(TestSigningKey));
                });
        });
    }

    public async Task InitializeAsync()
    {
        using var scope = Services.CreateScope();
        var db = scope.ServiceProvider.GetRequiredService<ApplicationDbContext>();
        await db.Database.EnsureDeletedAsync();
        await db.Database.MigrateAsync(); // applies all migrations, including the Role/Admin seed data

        // Clear the singleton permission cache so it reloads from the fresh DB.
        var cache = scope.ServiceProvider.GetRequiredService<IPermissionCacheService>();
        cache.InvalidateAll();

        // The migrations seed the roles and permissions but NOT a user account, so these tests
        // create the "admin" account they log in with. Test database only - never real data.
        var hasher = scope.ServiceProvider.GetRequiredService<IPasswordHasher>();
        var admin = new User
        {
            Username = "admin",
            Email = "admin@test.local",
            PasswordHash = hasher.HashPassword("Admin@123"),
            Status = "Active",
            SecurityStamp = Guid.NewGuid(), // D1: required for security_stamp claim
        };
        admin.UserRoles.Add(new UserRole { RoleId = 1, AssignedAt = DateTime.UtcNow });
        db.Users.Add(admin);
        await db.SaveChangesAsync();
    }

    public new async Task DisposeAsync()
    {
        using var scope = Services.CreateScope();
        var db = scope.ServiceProvider.GetRequiredService<ApplicationDbContext>();
        await db.Database.EnsureDeletedAsync();
    }
}

/// <summary>
/// All tests that use CustomWebApplicationFactory share ONE instance of it.
/// Without this, xUnit builds one factory per test class and runs the classes
/// in parallel; each factory drops and recreates the same database, so they
/// kept deleting the database underneath each other.
/// </summary>
[CollectionDefinition("LegacyApi")]
public class LegacyApiCollection : ICollectionFixture<CustomWebApplicationFactory>
{
}
