using Microsoft.AspNetCore.Hosting;
using Microsoft.AspNetCore.Mvc.Testing;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.DependencyInjection;
using SchoolManagement.Infrastructure.Persistence;
using Xunit;

namespace SchoolManagement.Tests.Support;

/// <summary>
/// Boots the real API (DI, middleware, EF Core) against a REAL SQL Server: your local SQL Express,
/// in a dedicated throw-away database that is dropped and re-created from the migrations when the
/// tests start and dropped again when they finish. Your dev database is never touched.
///
/// To point the tests at another SQL Server (a build server, Docker...), set the environment
/// variable SCHOOLMGMT_TEST_CONNECTION to a full connection string. Its database name MUST contain
/// "Test" - the factory refuses to drop anything else.
/// One database is shared by all tests in the "Database" collection (see DatabaseCollection).
/// </summary>
public class SqlServerWebApplicationFactory : WebApplicationFactory<Program>, IAsyncLifetime
{
    private const string DefaultConnectionString =
        "Server=IMADH\\SQLEXPRESS;Database=SchoolManagementDb_BatchTests;Trusted_Connection=True;TrustServerCertificate=True;";

    private readonly string _connectionString =
        Environment.GetEnvironmentVariable("SCHOOLMGMT_TEST_CONNECTION") ?? DefaultConnectionString;

    protected override void ConfigureWebHost(IWebHostBuilder builder)
    {
        builder.ConfigureAppConfiguration((_, config) =>
        {
            config.AddInMemoryCollection(new Dictionary<string, string?>
            {
                ["ConnectionStrings:DefaultConnection"] = _connectionString,
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
        EnsureItIsATestDatabase(db);

        await db.Database.EnsureDeletedAsync();
        await db.Database.MigrateAsync(); // applies every migration, including the seed data
    }

    async Task IAsyncLifetime.DisposeAsync()
    {
        using (var scope = Services.CreateScope())
        {
            var db = scope.ServiceProvider.GetRequiredService<ApplicationDbContext>();
            EnsureItIsATestDatabase(db);
            await db.Database.EnsureDeletedAsync();
        }
        await base.DisposeAsync();
    }

    /// <summary>Safety net: never drop a database whose name does not say it is for tests.</summary>
    private static void EnsureItIsATestDatabase(ApplicationDbContext db)
    {
        var name = db.Database.GetDbConnection().Database;
        if (!name.Contains("Test", StringComparison.OrdinalIgnoreCase))
            throw new InvalidOperationException(
                $"Refusing to drop database '{name}': its name must contain 'Test'.");
    }
}

/// <summary>Tests marked [Collection("Database")] share ONE database and run one after another.</summary>
[CollectionDefinition("Database")]
public class DatabaseCollection : ICollectionFixture<SqlServerWebApplicationFactory>
{
}