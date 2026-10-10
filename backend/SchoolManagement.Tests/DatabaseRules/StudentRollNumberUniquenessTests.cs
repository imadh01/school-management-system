using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.DependencyInjection;
using SchoolManagement.Infrastructure.Persistence;
using SchoolManagement.Tests.Support;
using Xunit;

namespace SchoolManagement.Tests.DatabaseRules;

/// <summary>
/// Bug #1. Roll numbers are unique among ACTIVE enrollments only. The old unique index on
/// Students (ClassSectionId, RollNumber) ignores that and keeps a Left student's roll reserved.
/// </summary>
[Collection("Database")]
public class StudentRollNumberUniquenessTests
{
    private readonly SqlServerWebApplicationFactory _factory;

    public StudentRollNumberUniquenessTests(SqlServerWebApplicationFactory factory) => _factory = factory;

    [Fact]
    public async Task RollNumber_of_a_student_who_left_can_be_reused_in_the_same_section()
    {
        using var scope = _factory.Services.CreateScope();
        var db = scope.ServiceProvider.GetRequiredService<ApplicationDbContext>();
        var section = await TestData.CreateSectionAsync(db);

        db.Students.Add(TestData.NewStudent(section, "7", status: "Left"));
        await db.SaveChangesAsync();

        db.Students.Add(TestData.NewStudent(section, "7")); // new Active student takes the free roll

        await db.SaveChangesAsync(); // must not throw
    }

    [Fact]
    public async Task RollNumber_cannot_be_shared_by_two_active_students_in_one_section()
    {
        using var scope = _factory.Services.CreateScope();
        var db = scope.ServiceProvider.GetRequiredService<ApplicationDbContext>();
        var section = await TestData.CreateSectionAsync(db);

        db.Students.Add(TestData.NewStudent(section, "9"));
        await db.SaveChangesAsync();

        db.Students.Add(TestData.NewStudent(section, "9"));

        await Assert.ThrowsAsync<DbUpdateException>(() => db.SaveChangesAsync());
    }
}
