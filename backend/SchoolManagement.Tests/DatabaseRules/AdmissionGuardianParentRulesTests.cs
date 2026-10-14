using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.DependencyInjection;
using SchoolManagement.Domain.Entities;
using SchoolManagement.Infrastructure.Persistence;
using SchoolManagement.Tests.Support;
using Xunit;

namespace SchoolManagement.Tests.DatabaseRules;

/// <summary>
/// G2: what the database itself guarantees about AdmissionGuardians.ParentId, whatever the application code does:
/// one application cannot list the same parent twice, many guardians may have no parent, and a parent that an
/// application points at cannot be hard-deleted.
/// </summary>
[Collection("Database")]
public class AdmissionGuardianParentRulesTests
{
    private readonly SqlServerWebApplicationFactory _factory;

    public AdmissionGuardianParentRulesTests(SqlServerWebApplicationFactory factory) => _factory = factory;

    [Fact]
    public async Task The_same_parent_twice_on_one_application_is_refused_by_the_unique_index()
    {
        using var scope = _factory.Services.CreateScope();
        var db = scope.ServiceProvider.GetRequiredService<ApplicationDbContext>();
        var parent = await NewParentAsync(db);
        var (admission, father) = await NewAdmissionAsync(db);
        father.ParentId = parent.Id;
        await db.SaveChangesAsync();

        db.AdmissionGuardians.Add(new AdmissionGuardian
        {
            AdmissionId = admission.Id, RelationType = "Mother", Name = "M", ParentId = parent.Id,
        });

        var error = await Assert.ThrowsAsync<DbUpdateException>(() => db.SaveChangesAsync());
        Assert.Contains("UX_AdmissionGuardians_Admission_Parent", error.InnerException!.Message);
    }

    [Fact]
    public async Task The_same_parent_on_two_different_applications_is_fine()
    {
        using var scope = _factory.Services.CreateScope();
        var db = scope.ServiceProvider.GetRequiredService<ApplicationDbContext>();
        var parent = await NewParentAsync(db);
        var (_, first) = await NewAdmissionAsync(db);
        var (_, second) = await NewAdmissionAsync(db);

        first.ParentId = parent.Id;
        second.ParentId = parent.Id; // two siblings applying in the same year

        await db.SaveChangesAsync();
        Assert.Equal(2, await db.AdmissionGuardians.CountAsync(g => g.ParentId == parent.Id));
    }

    [Fact]
    public async Task Several_guardians_with_no_parent_on_one_application_are_fine()
    {
        using var scope = _factory.Services.CreateScope();
        var db = scope.ServiceProvider.GetRequiredService<ApplicationDbContext>();
        var (admission, _) = await NewAdmissionAsync(db); // father has ParentId = NULL

        db.AdmissionGuardians.Add(new AdmissionGuardian { AdmissionId = admission.Id, RelationType = "Mother", Name = "M" });
        db.AdmissionGuardians.Add(new AdmissionGuardian { AdmissionId = admission.Id, RelationType = "Guardian", Name = "G" });

        await db.SaveChangesAsync(); // NULLs do not collide in the filtered unique index
        Assert.Equal(3, await db.AdmissionGuardians.CountAsync(g => g.AdmissionId == admission.Id));
    }

    [Fact]
    public async Task A_parent_that_an_application_points_at_cannot_be_hard_deleted()
    {
        using var scope = _factory.Services.CreateScope();
        var db = scope.ServiceProvider.GetRequiredService<ApplicationDbContext>();
        var parent = await NewParentAsync(db);
        var (_, father) = await NewAdmissionAsync(db);
        father.ParentId = parent.Id;
        await db.SaveChangesAsync();

        await Assert.ThrowsAnyAsync<Exception>(() =>
            db.Database.ExecuteSqlInterpolatedAsync($"DELETE FROM Parents WHERE Id = {parent.Id}"));
    }

    private static async Task<Parent> NewParentAsync(ApplicationDbContext db)
    {
        var parent = new Parent { Name = "Parent " + TestData.Unique(), Mobile = "9" + Random.Shared.NextInt64(100_000_000, 999_999_999) };
        db.Parents.Add(parent);
        await db.SaveChangesAsync();
        return parent;
    }

    private static async Task<(Admission Admission, AdmissionGuardian Father)> NewAdmissionAsync(ApplicationDbContext db)
    {
        var section = await TestData.CreateSectionAsync(db, capacity: 5);
        return await TestData.CreateAdmittedAdmissionAsync(db, section);
    }
}
