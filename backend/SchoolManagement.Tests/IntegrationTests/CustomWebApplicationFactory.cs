using Microsoft.AspNetCore.Hosting;
using Microsoft.AspNetCore.Mvc.Testing;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.VisualStudio.TestPlatform.TestHost;
using SchoolManagement.Infrastructure.Persistence;
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

    protected override void ConfigureWebHost(IWebHostBuilder builder)
    {
        builder.ConfigureAppConfiguration((_, config) =>
        {
            config.AddInMemoryCollection(new Dictionary<string, string?>
            {
                ["ConnectionStrings:DefaultConnection"] = TestConnectionString,
                ["Jwt:Issuer"] = "SchoolManagementApi",
                ["Jwt:Audience"] = "SchoolManagementClient",
                ["Jwt:SigningKey"] = "test-signing-key-at-least-32-characters-long!!",
                ["Jwt:ExpiryMinutes"] = "60",
            });
        });
    }

    public async Task InitializeAsync()
    {
        using var scope = Services.CreateScope();
        var db = scope.ServiceProvider.GetRequiredService<ApplicationDbContext>();
        await db.Database.EnsureDeletedAsync();
        await db.Database.MigrateAsync(); // applies all migrations, including the Role/Admin seed data
    }

    public new async Task DisposeAsync()
    {
        using var scope = Services.CreateScope();
        var db = scope.ServiceProvider.GetRequiredService<ApplicationDbContext>();
        await db.Database.EnsureDeletedAsync();
    }
}