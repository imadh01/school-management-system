using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.DependencyInjection;
using SchoolManagement.Application.Interfaces;
using SchoolManagement.Application.Services;
using SchoolManagement.Domain.Entities;
using SchoolManagement.Domain.Exceptions;
using SchoolManagement.Infrastructure.Persistence;
using SchoolManagement.Tests.Support;
using Xunit;

namespace SchoolManagement.Tests.DatabaseRules;

/// <summary>Restoring a deleted application: Admin only, 404 / 409 for the wrong cases, and the change is audited.</summary>
[Collection("Database")]
public class AdmissionRestoreTests
{
    private readonly SqlServerWebApplicationFactory _factory;

    public AdmissionRestoreTests(SqlServerWebApplicationFactory factory) => _factory = factory;

    private async Task<int> CreateDeletedApplicationAsync()
    {
        int id;
        using (var scope = _factory.Services.CreateScope())
        {
            var db = scope.ServiceProvider.GetRequiredService<ApplicationDbContext>();
            var section = await TestData.CreateSectionAsync(db);
            var (admission, _) = await TestData.CreateAdmittedAdmissionAsync(db, section);
            id = admission.Id;
        }

        using var deleteScope = _factory.Services.CreateScope();
        await deleteScope.ServiceProvider.GetRequiredService<IAdmissionService>().DeleteAsync(id, CancellationToken.None);
        return id;
    }

    [Fact]
    public async Task An_admin_restores_a_deleted_application_and_it_is_audited()
    {
        var id = await CreateDeletedApplicationAsync();

        using (var scope = _factory.Services.CreateScope())
        {
            var service = FakeCurrentUserPermissions.Build<AdmissionService>(scope.ServiceProvider, isAdmin: true);
            var restored = await service.RestoreAsync(id, CancellationToken.None);
            Assert.Equal(id, restored.Id);
        }

        using var verify = _factory.Services.CreateScope();
        var db = verify.ServiceProvider.GetRequiredService<ApplicationDbContext>();
        var row = await db.Admissions.IgnoreQueryFilters().AsNoTracking().SingleAsync(a => a.Id == id);
        Assert.False(row.IsDeleted);
        Assert.Null(row.DeletedAt);
        Assert.Null(row.DeletedBy);

        var logs = await db.AuditLogs.AsNoTracking()
            .Where(l => l.TableName == "Admissions" && l.RecordId == id.ToString() && l.Action == AuditActions.Update)
            .ToListAsync();
        Assert.Contains(logs, l => l.Changes != null && l.Changes.Contains("IsDeleted"));
    }

    [Fact]
    public async Task Anyone_who_is_not_an_admin_is_refused_and_the_application_stays_deleted()
    {
        var id = await CreateDeletedApplicationAsync();

        using (var scope = _factory.Services.CreateScope())
        {
            var service = FakeCurrentUserPermissions.Build<AdmissionService>(scope.ServiceProvider, isAdmin: false);
            await Assert.ThrowsAsync<ForbiddenException>(() => service.RestoreAsync(id, CancellationToken.None));
        }

        using var verify = _factory.Services.CreateScope();
        var db = verify.ServiceProvider.GetRequiredService<ApplicationDbContext>();
        Assert.True(await db.Admissions.IgnoreQueryFilters().AsNoTracking()
            .Where(a => a.Id == id).Select(a => a.IsDeleted).SingleAsync());
    }

    [Fact]
    public async Task An_unknown_id_is_404()
    {
        using var scope = _factory.Services.CreateScope();
        var service = FakeCurrentUserPermissions.Build<AdmissionService>(scope.ServiceProvider, isAdmin: true);
        await Assert.ThrowsAsync<NotFoundException>(() => service.RestoreAsync(int.MaxValue, CancellationToken.None));
    }

    [Fact]
    public async Task Restoring_an_application_that_is_not_deleted_is_409()
    {
        var id = await CreateDeletedApplicationAsync();
        using (var scope = _factory.Services.CreateScope())
            await FakeCurrentUserPermissions.Build<AdmissionService>(scope.ServiceProvider, isAdmin: true)
                .RestoreAsync(id, CancellationToken.None);

        using var again = _factory.Services.CreateScope();
        var service = FakeCurrentUserPermissions.Build<AdmissionService>(again.ServiceProvider, isAdmin: true);
        var ex = await Assert.ThrowsAsync<ConflictException>(() => service.RestoreAsync(id, CancellationToken.None));
        Assert.Contains("is not deleted", ex.Message);
    }
}
