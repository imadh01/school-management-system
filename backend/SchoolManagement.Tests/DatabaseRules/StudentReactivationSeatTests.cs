using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.DependencyInjection;
using SchoolManagement.Application.DTOs.Students;
using SchoolManagement.Application.Interfaces;
using SchoolManagement.Domain.Entities;
using SchoolManagement.Domain.Exceptions;
using SchoolManagement.Infrastructure.Persistence;
using SchoolManagement.Tests.Support;
using Xunit;

namespace SchoolManagement.Tests.DatabaseRules;

/// <summary>
/// Fix 1. An Inactive student does not hold a seat (seats count Active students only) but keeps an
/// open class period. Setting them back to Active in the same class takes a seat again, so it must
/// run the same checks as a class move or a returning student: the class must be Active and have room.
/// Before the fix the edit skipped both checks and a full class could go over capacity.
/// </summary>
[Collection("Database")]
public class StudentReactivationSeatTests
{
    private readonly SqlServerWebApplicationFactory _factory;

    public StudentReactivationSeatTests(SqlServerWebApplicationFactory factory) => _factory = factory;

    [Fact]
    public async Task Reactivating_an_inactive_student_into_a_full_class_is_rejected_with_409()
    {
        var (sectionId, inactive) = await ArrangeFullClassWithInactiveStudentAsync(capacity: 1);

        var ex = await Assert.ThrowsAsync<ConflictException>(() => ReactivateAsync(inactive, sectionId));

        Assert.Contains("is full", ex.Message);
        Assert.Equal(1, await ActiveCountAsync(sectionId)); // capacity not exceeded
        Assert.Equal("Inactive", await StatusOfAsync(inactive.Id)); // nothing was saved
    }

    [Fact]
    public async Task Reactivating_an_inactive_student_into_a_class_with_room_works()
    {
        var (sectionId, inactive) = await ArrangeFullClassWithInactiveStudentAsync(capacity: 2);

        await ReactivateAsync(inactive, sectionId);

        Assert.Equal(2, await ActiveCountAsync(sectionId));
        Assert.Equal("Active", await StatusOfAsync(inactive.Id));
    }

    [Fact]
    public async Task Editing_an_active_student_in_a_full_class_still_works()
    {
        // Only a move, a return or a reactivation is checked: an ordinary edit must not be blocked.
        int sectionId;
        Student active;
        using (var scope = _factory.Services.CreateScope())
        {
            var db = scope.ServiceProvider.GetRequiredService<ApplicationDbContext>();
            var section = await TestData.CreateSectionAsync(db, capacity: 1);
            sectionId = section.Id;
            active = TestData.NewStudent(section, "1");
            db.Students.Add(active);
            await db.SaveChangesAsync();
        }

        await ReactivateAsync(active, sectionId); // same request shape: Status "Active", same class and roll

        Assert.Equal(1, await ActiveCountAsync(sectionId));
    }

    [Fact]
    public async Task Keeping_an_inactive_student_inactive_needs_no_seat()
    {
        var (sectionId, inactive) = await ArrangeFullClassWithInactiveStudentAsync(capacity: 1);
        var request = TestData.MoveRequest(inactive, sectionId, inactive.RollNumber) with { Status = "Inactive" };

        using var scope = _factory.Services.CreateScope();
        var service = scope.ServiceProvider.GetRequiredService<IStudentService>();
        await service.UpdateAsync(inactive.Id, request, includeSensitive: false, CancellationToken.None);

        Assert.Equal("Inactive", await StatusOfAsync(inactive.Id));
    }

    // ------------------------------------------------------------------ helpers

    /// <summary>One Active student fills seat 1; a second student is Inactive but still has an Active class period.</summary>
    private async Task<(int SectionId, Student Inactive)> ArrangeFullClassWithInactiveStudentAsync(int capacity)
    {
        using var scope = _factory.Services.CreateScope();
        var db = scope.ServiceProvider.GetRequiredService<ApplicationDbContext>();
        var section = await TestData.CreateSectionAsync(db, capacity);

        db.Students.Add(TestData.NewStudent(section, "1"));
        var inactive = TestData.NewStudent(section, "2");
        db.Students.Add(inactive);
        await db.SaveChangesAsync();

        // What the edit page produces when Status is set to Inactive: status changes, the period stays open.
        inactive.Status = "Inactive";
        await db.SaveChangesAsync();
        return (section.Id, inactive);
    }

    private async Task ReactivateAsync(Student student, int sectionId)
    {
        using var scope = _factory.Services.CreateScope();
        var db = scope.ServiceProvider.GetRequiredService<ApplicationDbContext>();
        var current = await db.Students.AsNoTracking().SingleAsync(s => s.Id == student.Id);

        var request = TestData.MoveRequest(current, sectionId, current.RollNumber); // Status = "Active"
        var service = scope.ServiceProvider.GetRequiredService<IStudentService>();
        await service.UpdateAsync(student.Id, request, includeSensitive: false, CancellationToken.None);
    }

    private async Task<int> ActiveCountAsync(int sectionId)
    {
        using var scope = _factory.Services.CreateScope();
        var db = scope.ServiceProvider.GetRequiredService<ApplicationDbContext>();
        return await db.Students.CountAsync(s => s.ClassSectionId == sectionId && s.Status == "Active");
    }

    private async Task<string> StatusOfAsync(int studentId)
    {
        using var scope = _factory.Services.CreateScope();
        var db = scope.ServiceProvider.GetRequiredService<ApplicationDbContext>();
        return await db.Students.Where(s => s.Id == studentId).Select(s => s.Status).SingleAsync();
    }
}
