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

/// <summary>
/// Restoring a deleted student. Only a student with no class history can be deleted, so these tests create such a student
/// (no enrolment period) and delete it through the service. A restored Active student takes a seat again.
/// </summary>
[Collection("Database")]
public class StudentRestoreTests
{
    private readonly SqlServerWebApplicationFactory _factory;

    public StudentRestoreTests(SqlServerWebApplicationFactory factory) => _factory = factory;

    private async Task<(int StudentId, int SectionId)> CreateDeletedStudentAsync(int? capacity = null)
    {
        int studentId, sectionId;
        using (var scope = _factory.Services.CreateScope())
        {
            var db = scope.ServiceProvider.GetRequiredService<ApplicationDbContext>();
            var section = await TestData.CreateSectionAsync(db, capacity);
            var student = TestData.NewStudent(section, "1");
            student.Enrollments.Clear();
            db.Students.Add(student);
            await db.SaveChangesAsync();
            studentId = student.Id;
            sectionId = section.Id;
        }

        using var deleteScope = _factory.Services.CreateScope();
        await deleteScope.ServiceProvider.GetRequiredService<IStudentService>().DeleteAsync(studentId, CancellationToken.None);
        return (studentId, sectionId);
    }

    [Fact]
    public async Task An_admin_restores_a_deleted_student_and_it_is_audited()
    {
        var (id, _) = await CreateDeletedStudentAsync();

        using (var scope = _factory.Services.CreateScope())
            await FakeCurrentUserPermissions.Build<StudentService>(scope.ServiceProvider, isAdmin: true)
                .RestoreAsync(id, CancellationToken.None);

        using var verify = _factory.Services.CreateScope();
        var db = verify.ServiceProvider.GetRequiredService<ApplicationDbContext>();
        var row = await db.Students.IgnoreQueryFilters().AsNoTracking().SingleAsync(s => s.Id == id);
        Assert.False(row.IsDeleted);
        Assert.Null(row.DeletedAt);
        Assert.Null(row.DeletedBy);
        Assert.True(await db.AuditLogs.AnyAsync(l =>
            l.TableName == "Students" && l.RecordId == id.ToString() && l.Action == AuditActions.Update));
    }

    [Fact]
    public async Task A_non_admin_cannot_restore_or_list_deleted_students()
    {
        var (id, _) = await CreateDeletedStudentAsync();

        using var scope = _factory.Services.CreateScope();
        var service = FakeCurrentUserPermissions.Build<StudentService>(scope.ServiceProvider, isAdmin: false);
        await Assert.ThrowsAsync<ForbiddenException>(() => service.RestoreAsync(id, CancellationToken.None));
        await Assert.ThrowsAsync<ForbiddenException>(() => service.GetDeletedAsync(CancellationToken.None));
    }

    [Fact]
    public async Task Unknown_id_is_404_and_a_student_that_is_not_deleted_is_409()
    {
        var (id, _) = await CreateDeletedStudentAsync();
        using var scope = _factory.Services.CreateScope();
        var service = FakeCurrentUserPermissions.Build<StudentService>(scope.ServiceProvider, isAdmin: true);

        await Assert.ThrowsAsync<NotFoundException>(() => service.RestoreAsync(int.MaxValue, CancellationToken.None));

        await service.RestoreAsync(id, CancellationToken.None);
        var ex = await Assert.ThrowsAsync<ConflictException>(() => service.RestoreAsync(id, CancellationToken.None));
        Assert.Contains("is not deleted", ex.Message);
    }

    [Fact]
    public async Task Restoring_into_a_class_that_filled_up_meanwhile_is_409_and_the_student_stays_deleted()
    {
        var (id, sectionId) = await CreateDeletedStudentAsync(capacity: 1);

        using (var scope = _factory.Services.CreateScope())
        {
            var db = scope.ServiceProvider.GetRequiredService<ApplicationDbContext>();
            var section = await db.ClassSections.SingleAsync(c => c.Id == sectionId);
            db.Students.Add(TestData.NewStudent(section, "2")); // takes the only seat
            await db.SaveChangesAsync();
        }

        using (var scope = _factory.Services.CreateScope())
        {
            var service = FakeCurrentUserPermissions.Build<StudentService>(scope.ServiceProvider, isAdmin: true);
            var ex = await Assert.ThrowsAsync<ConflictException>(() => service.RestoreAsync(id, CancellationToken.None));
            Assert.Contains("is full", ex.Message);
        }

        using var verify = _factory.Services.CreateScope();
        var check = verify.ServiceProvider.GetRequiredService<ApplicationDbContext>();
        Assert.True(await check.Students.IgnoreQueryFilters().AsNoTracking()
            .Where(s => s.Id == id).Select(s => s.IsDeleted).SingleAsync());
    }

    [Fact]
    public async Task The_deleted_list_shows_the_student_to_an_admin()
    {
        var (id, _) = await CreateDeletedStudentAsync();

        using var scope = _factory.Services.CreateScope();
        var service = FakeCurrentUserPermissions.Build<StudentService>(scope.ServiceProvider, isAdmin: true);
        var list = await service.GetDeletedAsync(CancellationToken.None);
        Assert.Contains(list, r => r.Id == id);
    }
}
