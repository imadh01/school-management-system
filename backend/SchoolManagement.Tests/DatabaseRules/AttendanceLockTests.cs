using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.DependencyInjection;
using SchoolManagement.Application.DTOs.Attendance;
using SchoolManagement.Application.Interfaces;
using SchoolManagement.Domain.Entities;
using SchoolManagement.Domain.Exceptions;
using SchoolManagement.Infrastructure.Persistence;
using SchoolManagement.Tests.Support;
using Xunit;

namespace SchoolManagement.Tests.DatabaseRules;

/// <summary>
/// A register locks 24 hours after it was first saved; only an Admin can change it after that.
/// The "first saved" time is moved back in the database, which is the only way to wait 24 hours in a test.
/// </summary>
[Collection("Database")]
public class AttendanceLockTests
{
    private readonly SqlServerWebApplicationFactory _factory;

    public AttendanceLockTests(SqlServerWebApplicationFactory factory) => _factory = factory;

    private static DateOnly Today => DateOnly.FromDateTime(DateTime.UtcNow);

    private sealed record Setup(int SectionId, int StudentId, AttendanceActor Staff, AttendanceActor Admin);

    // AttendanceSessions.TakenByUserId has a foreign key to Users, so the actors need a real user row.
    private async Task<Setup> SavedRegisterAsync(double savedHoursAgo)
    {
        int sectionId, studentId, userId;
        using (var scope = _factory.Services.CreateScope())
        {
            var db = scope.ServiceProvider.GetRequiredService<ApplicationDbContext>();
            var section = await TestData.CreateSectionAsync(db);
            var student = TestData.NewStudent(section, "1");
            db.Students.Add(student);

            var name = "att_" + TestData.Unique();
            var user = new User
            {
                Username = name,
                Email = $"{name}@test.local",
                PasswordHash = "not-a-real-hash",
                Status = UserStatuses.Active,
                SecurityStamp = Guid.NewGuid(),
            };
            db.Users.Add(user);
            await db.SaveChangesAsync();
            sectionId = section.Id;
            studentId = student.Id;
            userId = user.Id;
        }

        var setup = new Setup(
            sectionId, studentId,
            new AttendanceActor(userId, CanManageAll: true, IsAdmin: false),
            new AttendanceActor(userId, CanManageAll: true, IsAdmin: true));

        using (var scope = _factory.Services.CreateScope())
            await scope.ServiceProvider.GetRequiredService<IAttendanceService>()
                .SaveAsync(setup.Staff, Request(setup, "Present"), CancellationToken.None);

        using (var scope = _factory.Services.CreateScope())
        {
            var db = scope.ServiceProvider.GetRequiredService<ApplicationDbContext>();
            var when = DateTime.UtcNow.AddHours(-savedHoursAgo);
            await db.AttendanceSessions.Where(s => s.ClassSectionId == sectionId)
                .ExecuteUpdateAsync(s => s.SetProperty(x => x.CreatedAt, when));
        }
        return setup;
    }

    private static SaveAttendanceRequest Request(Setup setup, string status) =>
        new(setup.SectionId, Today, null, new List<AttendanceEntry> { new(setup.StudentId, status, null) });

    [Fact]
    public async Task Staff_can_still_change_a_register_saved_23_hours_ago()
    {
        var setup = await SavedRegisterAsync(savedHoursAgo: 23);

        using var scope = _factory.Services.CreateScope();
        await scope.ServiceProvider.GetRequiredService<IAttendanceService>()
            .SaveAsync(setup.Staff, Request(setup, "Absent"), CancellationToken.None); // must not throw
    }

    [Fact]
    public async Task Staff_cannot_change_a_register_saved_25_hours_ago_and_nothing_changes()
    {
        var setup = await SavedRegisterAsync(savedHoursAgo: 25);

        using (var scope = _factory.Services.CreateScope())
        {
            var ex = await Assert.ThrowsAsync<BusinessRuleException>(() =>
                scope.ServiceProvider.GetRequiredService<IAttendanceService>()
                    .SaveAsync(setup.Staff, Request(setup, "Absent"), CancellationToken.None));
            Assert.Contains("Only an Admin can change it now", ex.Message);
        }

        using var verify = _factory.Services.CreateScope();
        var db = verify.ServiceProvider.GetRequiredService<ApplicationDbContext>();
        var status = await db.AttendanceRecords.AsNoTracking()
            .Where(r => r.StudentId == setup.StudentId).Select(r => r.Status).SingleAsync();
        Assert.Equal("Present", status);
    }

    [Fact]
    public async Task An_admin_can_change_a_register_saved_25_hours_ago()
    {
        var setup = await SavedRegisterAsync(savedHoursAgo: 25);

        using (var scope = _factory.Services.CreateScope())
            await scope.ServiceProvider.GetRequiredService<IAttendanceService>()
                .SaveAsync(setup.Admin, Request(setup, "Late"), CancellationToken.None);

        using var verify = _factory.Services.CreateScope();
        var db = verify.ServiceProvider.GetRequiredService<ApplicationDbContext>();
        Assert.Equal("Late", await db.AttendanceRecords.AsNoTracking()
            .Where(r => r.StudentId == setup.StudentId).Select(r => r.Status).SingleAsync());
    }
}
