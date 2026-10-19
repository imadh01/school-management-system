using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.DependencyInjection;
using SchoolManagement.Application.Interfaces;
using SchoolManagement.Domain.Entities;
using SchoolManagement.Domain.Exceptions;
using SchoolManagement.Infrastructure.Persistence;
using SchoolManagement.Tests.Support;
using Xunit;

namespace SchoolManagement.Tests.DatabaseRules;

/// <summary>
/// Fix 2. "Set as primary contact" on a student's guardians.
///  - The link must exist, otherwise 404. Before the fix an unknown link cleared the current primary and returned 204.
///  - The switch is one transaction: the old primary is cleared and the new one set together, never one without the other.
///  - UX_StudentGuardians_Student_Primary (filtered unique index) allows one primary per student. Before the fix both
///    rows were updated by ONE SaveChanges, whose statement order follows the primary key, so switching to the parent
///    with the lower id tried to set the new primary before clearing the old one and the index refused it.
/// Runs against the real SQL Server, because the index is the point.
/// </summary>
[Collection("Database")]
public class GuardianPrimaryContactTests
{
    private readonly SqlServerWebApplicationFactory _factory;

    public GuardianPrimaryContactTests(SqlServerWebApplicationFactory factory) => _factory = factory;

    [Fact]
    public async Task The_database_allows_only_one_primary_contact_per_student()
    {
        var (studentId, parents) = await ArrangeAsync(primaryIndex: 0, parentCount: 2);

        using var scope = _factory.Services.CreateScope();
        var db = scope.ServiceProvider.GetRequiredService<ApplicationDbContext>();
        var second = await db.StudentGuardians.SingleAsync(g => g.StudentId == studentId && g.ParentId == parents[1]);
        second.IsPrimaryContact = true;

        await Assert.ThrowsAsync<DbUpdateException>(() => db.SaveChangesAsync());
    }

    [Fact]
    public async Task Setting_a_primary_for_a_link_that_does_not_exist_is_404_and_keeps_the_current_primary()
    {
        var (studentId, parents) = await ArrangeAsync(primaryIndex: 0, parentCount: 1);
        var stranger = await NewParentAsync();

        await Assert.ThrowsAsync<NotFoundException>(() => SetPrimaryAsync(stranger, studentId));

        Assert.Equal(parents[0], await PrimaryParentIdAsync(studentId)); // untouched
    }

    [Fact]
    public async Task Switching_the_primary_to_the_parent_with_the_lower_id_works()
    {
        // Parents are created in order, so parents[0] has the lower id. The old primary is the higher id.
        var (studentId, parents) = await ArrangeAsync(primaryIndex: 1, parentCount: 2);

        await SetPrimaryAsync(parents[0], studentId);

        Assert.Equal(parents[0], await PrimaryParentIdAsync(studentId));
        Assert.Equal(1, await PrimaryCountAsync(studentId));
    }

    [Fact]
    public async Task Switching_the_primary_to_the_parent_with_the_higher_id_works()
    {
        var (studentId, parents) = await ArrangeAsync(primaryIndex: 0, parentCount: 2);

        await SetPrimaryAsync(parents[1], studentId);

        Assert.Equal(parents[1], await PrimaryParentIdAsync(studentId));
        Assert.Equal(1, await PrimaryCountAsync(studentId));
    }

    [Fact]
    public async Task Setting_the_parent_who_is_already_primary_changes_nothing()
    {
        var (studentId, parents) = await ArrangeAsync(primaryIndex: 0, parentCount: 2);

        await SetPrimaryAsync(parents[0], studentId);

        Assert.Equal(parents[0], await PrimaryParentIdAsync(studentId));
        Assert.Equal(1, await PrimaryCountAsync(studentId));
    }

    [Fact]
    public async Task A_student_with_no_primary_gets_one()
    {
        var (studentId, parents) = await ArrangeAsync(primaryIndex: -1, parentCount: 2);

        await SetPrimaryAsync(parents[1], studentId);

        Assert.Equal(parents[1], await PrimaryParentIdAsync(studentId));
    }

    // ------------------------------------------------------------------ helpers

    /// <summary>A student linked to <paramref name="parentCount"/> parents; the one at <paramref name="primaryIndex"/> is primary (-1 = none).</summary>
    private async Task<(int StudentId, List<int> ParentIds)> ArrangeAsync(int primaryIndex, int parentCount)
    {
        var parentIds = new List<int>();
        for (var i = 0; i < parentCount; i++) parentIds.Add(await NewParentAsync());

        using var scope = _factory.Services.CreateScope();
        var db = scope.ServiceProvider.GetRequiredService<ApplicationDbContext>();
        var section = await TestData.CreateSectionAsync(db);
        var student = TestData.NewStudent(section, "1");
        db.Students.Add(student);
        await db.SaveChangesAsync();

        for (var i = 0; i < parentIds.Count; i++)
            db.StudentGuardians.Add(new StudentGuardian
            {
                StudentId = student.Id, ParentId = parentIds[i],
                RelationType = i == 0 ? "Father" : "Mother",
                IsPrimaryContact = i == primaryIndex,
            });
        await db.SaveChangesAsync();
        return (student.Id, parentIds);
    }

    private async Task<int> NewParentAsync()
    {
        using var scope = _factory.Services.CreateScope();
        var db = scope.ServiceProvider.GetRequiredService<ApplicationDbContext>();
        var parent = new Parent
        {
            Name = "Parent " + TestData.Unique(), Status = "Active",
            Mobile = "9" + Random.Shared.NextInt64(100_000_000, 999_999_999),
        };
        db.Parents.Add(parent);
        await db.SaveChangesAsync();
        return parent.Id;
    }

    private async Task SetPrimaryAsync(int parentId, int studentId)
    {
        using var scope = _factory.Services.CreateScope();
        var service = scope.ServiceProvider.GetRequiredService<IStudentGuardianService>();
        await service.SetPrimaryAsync(parentId, studentId, CancellationToken.None);
    }

    private async Task<int?> PrimaryParentIdAsync(int studentId)
    {
        using var scope = _factory.Services.CreateScope();
        var db = scope.ServiceProvider.GetRequiredService<ApplicationDbContext>();
        return await db.StudentGuardians.AsNoTracking()
            .Where(g => g.StudentId == studentId && g.IsPrimaryContact)
            .Select(g => (int?)g.ParentId).SingleOrDefaultAsync();
    }

    private async Task<int> PrimaryCountAsync(int studentId)
    {
        using var scope = _factory.Services.CreateScope();
        var db = scope.ServiceProvider.GetRequiredService<ApplicationDbContext>();
        return await db.StudentGuardians.CountAsync(g => g.StudentId == studentId && g.IsPrimaryContact);
    }
}
