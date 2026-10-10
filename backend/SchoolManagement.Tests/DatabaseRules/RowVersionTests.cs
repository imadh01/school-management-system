using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata;
using Microsoft.Extensions.DependencyInjection;
using SchoolManagement.Application.DTOs.ClassSections;
using SchoolManagement.Application.DTOs.Parents;
using SchoolManagement.Application.Interfaces;
using SchoolManagement.Domain.Entities;
using SchoolManagement.Infrastructure.Persistence;
using SchoolManagement.Tests.Support;
using Xunit;

namespace SchoolManagement.Tests.DatabaseRules;

/// <summary>
/// Optimistic concurrency. Two people open the same record, both press Save: the first save wins,
/// the second must be refused (409) instead of silently overwriting the first person's change.
/// </summary>
[Collection("Database")]
public class RowVersionTests
{
    private readonly SqlServerWebApplicationFactory _factory;

    public RowVersionTests(SqlServerWebApplicationFactory factory) => _factory = factory;

    [Theory]
    [InlineData(typeof(Student))]
    [InlineData(typeof(Admission))]
    [InlineData(typeof(ClassSection))]
    [InlineData(typeof(Parent))]
    public void Entity_has_a_database_generated_row_version(Type entityType)
    {
        using var scope = _factory.Services.CreateScope();
        var db = scope.ServiceProvider.GetRequiredService<ApplicationDbContext>();

        var property = db.Model.FindEntityType(entityType)!.FindProperty("RowVersion");

        Assert.NotNull(property);
        Assert.True(property!.IsConcurrencyToken);
        Assert.Equal(ValueGenerated.OnAddOrUpdate, property.ValueGenerated);
    }

    [Fact]
    public async Task A_second_student_save_with_an_out_of_date_version_is_refused()
    {
        Student student;
        ClassSection section;
        using (var scope = _factory.Services.CreateScope())
        {
            var db = scope.ServiceProvider.GetRequiredService<ApplicationDbContext>();
            section = await TestData.CreateSectionAsync(db);
            student = TestData.NewStudent(section, "1");
            db.Students.Add(student);
            await db.SaveChangesAsync();
        }
        var stale = Convert.ToBase64String(student.RowVersion);

        using (var scope = _factory.Services.CreateScope())
        {
            var service = scope.ServiceProvider.GetRequiredService<IStudentService>();
            await service.UpdateAsync(student.Id, TestData.MoveRequest(student, section.Id, "1", stale), false, default); // first save wins
        }

        using (var scope = _factory.Services.CreateScope())
        {
            var service = scope.ServiceProvider.GetRequiredService<IStudentService>();
            await Assert.ThrowsAsync<DbUpdateConcurrencyException>(() =>
                service.UpdateAsync(student.Id, TestData.MoveRequest(student, section.Id, "1", stale), false, default));
        }
    }

    [Fact]
    public async Task A_second_parent_save_with_an_out_of_date_version_is_refused()
    {
        Parent parent;
        using (var scope = _factory.Services.CreateScope())
        {
            var db = scope.ServiceProvider.GetRequiredService<ApplicationDbContext>();
            parent = new Parent { Name = "Parent " + TestData.Unique(), Mobile = "9" + Random.Shared.NextInt64(100_000_000, 999_999_999) };
            db.Parents.Add(parent);
            await db.SaveChangesAsync();
        }
        var stale = Convert.ToBase64String(parent.RowVersion);
        UpdateParentRequest Request() => new(
            parent.Name, null, parent.Mobile, "Active",
            null, null, null, null, null, null, null, false,
            true, true, true, true, true,
            null, null, null, null, false,
            null, null, null, null,
            stale);

        using (var scope = _factory.Services.CreateScope())
            await scope.ServiceProvider.GetRequiredService<IParentService>().UpdateAsync(parent.Id, Request(), default);

        using (var scope = _factory.Services.CreateScope())
            await Assert.ThrowsAsync<DbUpdateConcurrencyException>(() =>
                scope.ServiceProvider.GetRequiredService<IParentService>().UpdateAsync(parent.Id, Request(), default));
    }

    [Fact]
    public async Task A_second_class_save_with_an_out_of_date_version_is_refused()
    {
        ClassSection section;
        using (var scope = _factory.Services.CreateScope())
        {
            var db = scope.ServiceProvider.GetRequiredService<ApplicationDbContext>();
            section = await TestData.CreateSectionAsync(db, capacity: 30);
        }
        var stale = Convert.ToBase64String(section.RowVersion);
        UpdateClassSectionRequest Request() => new(
            "Class 1", "A", null, "Primary", "English", "General", 30, null, null, null, true, stale);

        using (var scope = _factory.Services.CreateScope())
            await scope.ServiceProvider.GetRequiredService<IClassSectionService>().UpdateAsync(section.Id, Request(), default);

        using (var scope = _factory.Services.CreateScope())
            await Assert.ThrowsAsync<DbUpdateConcurrencyException>(() =>
                scope.ServiceProvider.GetRequiredService<IClassSectionService>().UpdateAsync(section.Id, Request(), default));
    }
}
