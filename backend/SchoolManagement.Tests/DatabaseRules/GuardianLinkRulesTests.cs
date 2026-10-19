using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.DependencyInjection;
using SchoolManagement.Application.DTOs.Parents;
using SchoolManagement.Application.Interfaces;
using SchoolManagement.Domain.Entities;
using SchoolManagement.Domain.Exceptions;
using SchoolManagement.Infrastructure.Persistence;
using SchoolManagement.Tests.Support;
using Xunit;

namespace SchoolManagement.Tests.DatabaseRules;

/// <summary>
/// Fix 3. Linking a parent and a student (from the student side or the parent side) must answer with a
/// clear 404 / 409, never a 500:
///  - unknown student or parent: 404 naming it (before the fix a missing student, or a missing parent on the
///    parent side, reached the database and failed on the foreign key);
///  - a second primary contact for the same student: 409 naming the current primary (before the fix the filtered
///    unique index UX_StudentGuardians_Student_Primary raised an unhandled DbUpdateException);
///  - two people setting a primary at the same moment: exactly one wins, the other gets 409.
/// </summary>
[Collection("Database")]
public class GuardianLinkRulesTests
{
    private const int Missing = 999_999;
    private readonly SqlServerWebApplicationFactory _factory;

    public GuardianLinkRulesTests(SqlServerWebApplicationFactory factory) => _factory = factory;

    // ------------------------------------------------------------------ unknown ids (404)

    [Fact]
    public async Task Linking_from_the_student_side_to_an_unknown_student_is_404()
    {
        var parentId = await NewParentAsync();

        var ex = await Assert.ThrowsAsync<NotFoundException>(() =>
            LinkFromStudentAsync(Missing, new LinkGuardianRequest(parentId, "Father", false)));

        Assert.Contains($"Student {Missing}", ex.Message);
    }

    [Fact]
    public async Task Linking_from_the_student_side_to_an_unknown_parent_is_404()
    {
        var studentId = await NewStudentAsync();

        var ex = await Assert.ThrowsAsync<NotFoundException>(() =>
            LinkFromStudentAsync(studentId, new LinkGuardianRequest(Missing, "Father", false)));

        Assert.Contains($"Parent {Missing}", ex.Message);
    }

    [Fact]
    public async Task Linking_from_the_parent_side_to_an_unknown_parent_is_404()
    {
        var studentId = await NewStudentAsync();

        var ex = await Assert.ThrowsAsync<NotFoundException>(() =>
            LinkFromParentAsync(Missing, new LinkStudentRequest(studentId, "Father", false)));

        Assert.Contains($"Parent {Missing}", ex.Message);
    }

    [Fact]
    public async Task Linking_from_the_parent_side_to_an_unknown_student_is_404()
    {
        var parentId = await NewParentAsync();

        var ex = await Assert.ThrowsAsync<NotFoundException>(() =>
            LinkFromParentAsync(parentId, new LinkStudentRequest(Missing, "Father", false)));

        Assert.Contains($"Student {Missing}", ex.Message);
    }

    // ------------------------------------------------------------------ second primary (409)

    [Fact]
    public async Task A_second_primary_contact_from_the_student_side_is_409_and_keeps_the_first()
    {
        var (studentId, firstParentId, firstName) = await StudentWithPrimaryAsync();
        var secondParentId = await NewParentAsync();

        var ex = await Assert.ThrowsAsync<ConflictException>(() =>
            LinkFromStudentAsync(studentId, new LinkGuardianRequest(secondParentId, "Mother", true)));

        Assert.Contains("already has a primary contact", ex.Message);
        Assert.Contains(firstName, ex.Message);
        Assert.Equal(firstParentId, await PrimaryParentIdAsync(studentId));
        Assert.False(await LinkExistsAsync(studentId, secondParentId)); // nothing was added
    }

    [Fact]
    public async Task A_second_primary_contact_from_the_parent_side_is_409_and_keeps_the_first()
    {
        var (studentId, firstParentId, firstName) = await StudentWithPrimaryAsync();
        var secondParentId = await NewParentAsync();

        var ex = await Assert.ThrowsAsync<ConflictException>(() =>
            LinkFromParentAsync(secondParentId, new LinkStudentRequest(studentId, "Mother", true)));

        Assert.Contains("already has a primary contact", ex.Message);
        Assert.Contains(firstName, ex.Message);
        Assert.Equal(firstParentId, await PrimaryParentIdAsync(studentId));
        Assert.False(await LinkExistsAsync(studentId, secondParentId));
    }

    [Fact]
    public async Task A_primary_contact_can_be_linked_when_the_student_has_none()
    {
        var studentId = await NewStudentAsync();
        var parentId = await NewParentAsync();

        await LinkFromStudentAsync(studentId, new LinkGuardianRequest(parentId, "Father", true));

        Assert.Equal(parentId, await PrimaryParentIdAsync(studentId));
    }

    [Fact]
    public async Task A_non_primary_guardian_can_be_linked_next_to_an_existing_primary()
    {
        var (studentId, firstParentId, _) = await StudentWithPrimaryAsync();
        var secondParentId = await NewParentAsync();

        await LinkFromParentAsync(secondParentId, new LinkStudentRequest(studentId, "Mother", false));

        Assert.Equal(firstParentId, await PrimaryParentIdAsync(studentId));
        Assert.True(await LinkExistsAsync(studentId, secondParentId));
    }

    [Fact]
    public async Task Linking_the_same_pair_twice_is_still_409()
    {
        var studentId = await NewStudentAsync();
        var parentId = await NewParentAsync();
        await LinkFromStudentAsync(studentId, new LinkGuardianRequest(parentId, "Father", false));

        await Assert.ThrowsAsync<ConflictException>(() =>
            LinkFromStudentAsync(studentId, new LinkGuardianRequest(parentId, "Father", false)));
    }

    // ------------------------------------------------------------------ two people at once

    [Fact]
    public async Task Two_simultaneous_primary_links_for_one_student_give_one_success_and_one_409()
    {
        for (var round = 1; round <= 10; round++)
        {
            var studentId = await NewStudentAsync();
            var parentA = await NewParentAsync();
            var parentB = await NewParentAsync();

            var gate = new TaskCompletionSource();
            var attempts = new[] { parentA, parentB }.Select(parentId => Task.Run(async () =>
            {
                await gate.Task; // release both together
                try
                {
                    await LinkFromStudentAsync(studentId, new LinkGuardianRequest(parentId, "Guardian", true));
                    return (Exception?)null;
                }
                catch (Exception ex)
                {
                    return ex;
                }
            })).ToList();

            gate.SetResult();
            var outcomes = await Task.WhenAll(attempts);

            Assert.Single(outcomes, o => o is null);
            Assert.Single(outcomes, o => o is ConflictException); // never a raw DbUpdateException
            Assert.NotNull(await PrimaryParentIdAsync(studentId));
        }
    }

    // ------------------------------------------------------------------ helpers

    private async Task LinkFromStudentAsync(int studentId, LinkGuardianRequest request)
    {
        using var scope = _factory.Services.CreateScope();
        var service = scope.ServiceProvider.GetRequiredService<IStudentGuardianService>();
        await service.LinkAsync(studentId, request, CancellationToken.None);
    }

    private async Task LinkFromParentAsync(int parentId, LinkStudentRequest request)
    {
        using var scope = _factory.Services.CreateScope();
        var service = scope.ServiceProvider.GetRequiredService<IStudentGuardianService>();
        await service.LinkStudentAsync(parentId, request, CancellationToken.None);
    }

    private async Task<int> NewStudentAsync()
    {
        using var scope = _factory.Services.CreateScope();
        var db = scope.ServiceProvider.GetRequiredService<ApplicationDbContext>();
        var section = await TestData.CreateSectionAsync(db);
        var student = TestData.NewStudent(section, "1");
        db.Students.Add(student);
        await db.SaveChangesAsync();
        return student.Id;
    }

    private async Task<int> NewParentAsync(string? name = null)
    {
        using var scope = _factory.Services.CreateScope();
        var db = scope.ServiceProvider.GetRequiredService<ApplicationDbContext>();
        var parent = new Parent
        {
            Name = name ?? "Parent " + TestData.Unique(), Status = "Active",
            Mobile = "9" + Random.Shared.NextInt64(100_000_000, 999_999_999),
        };
        db.Parents.Add(parent);
        await db.SaveChangesAsync();
        return parent.Id;
    }

    private async Task<(int StudentId, int ParentId, string ParentName)> StudentWithPrimaryAsync()
    {
        var studentId = await NewStudentAsync();
        var name = "Primary " + TestData.Unique();
        var parentId = await NewParentAsync(name);
        await LinkFromStudentAsync(studentId, new LinkGuardianRequest(parentId, "Father", true));
        return (studentId, parentId, name);
    }

    private async Task<int?> PrimaryParentIdAsync(int studentId)
    {
        using var scope = _factory.Services.CreateScope();
        var db = scope.ServiceProvider.GetRequiredService<ApplicationDbContext>();
        return await db.StudentGuardians.AsNoTracking()
            .Where(g => g.StudentId == studentId && g.IsPrimaryContact)
            .Select(g => (int?)g.ParentId).SingleOrDefaultAsync();
    }

    private async Task<bool> LinkExistsAsync(int studentId, int parentId)
    {
        using var scope = _factory.Services.CreateScope();
        var db = scope.ServiceProvider.GetRequiredService<ApplicationDbContext>();
        return await db.StudentGuardians.AnyAsync(g => g.StudentId == studentId && g.ParentId == parentId);
    }
}
