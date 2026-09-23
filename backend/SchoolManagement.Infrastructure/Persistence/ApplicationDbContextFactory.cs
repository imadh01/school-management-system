using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Design;
using SchoolManagement.Infrastructure.Persistence.Interceptors;

namespace SchoolManagement.Infrastructure.Persistence;

/// <summary>
/// Used only by the `dotnet ef` CLI at design time to build a DbContext
/// for generating migrations. Superseded once SchoolManagement.API
/// registers ApplicationDbContext for real via DI + appsettings.json —
/// this class is not used at runtime.
/// </summary>
public class ApplicationDbContextFactory : IDesignTimeDbContextFactory<ApplicationDbContext>
{
    public ApplicationDbContext CreateDbContext(string[] args)
    {
        var connectionString = Environment.GetEnvironmentVariable("SCHOOLMANAGEMENT_CONNECTION_STRING")
            ?? "Server=IMADH\\SQLEXPRESS;Database=SchoolManagementDb;Trusted_Connection=True;TrustServerCertificate=True;";

        var optionsBuilder = new DbContextOptionsBuilder<ApplicationDbContext>();
        optionsBuilder.UseSqlServer(connectionString);

        return new ApplicationDbContext(optionsBuilder.Options, new AuditableEntitySaveChangesInterceptor());
    }
}