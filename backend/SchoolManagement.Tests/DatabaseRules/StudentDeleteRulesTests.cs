using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.DependencyInjection;
using SchoolManagement.Application.Interfaces;
using SchoolManagement.Domain.Exceptions;
using SchoolManagement.Infrastructure.Persistence;
using SchoolManagement.Tests.Support;
using Xunit;

namespace SchoolManagement.Tests.DatabaseRules;

/// <summary>
/// Bug #2. Decision: a student who has academic history cannot be soft-deleted (use status
/// "Left" for normal exits). Deleting is only for records created by mistake. Otherwise a
/// deleted student keeps an Active enrollment that still holds a roll number and a seat.
/// (Attendance-history case is added with the fix in batch A.)
/// </summary>
[Collection("Database")]
public class StudentDeleteRulesTests
{
    private readonly SqlServerWebApplicationFactory _factory;

    public StudentDeleteRulesTests(SqlServerWebApplicationFactory factory) => _factory = factory;

    [Fact]
    public async Task Deleting_a_student_with_enrollments_is_rejected_with_409()
    {
        int studentId;
        using (var scope = _factory.Services.CreateScope())
        {
            var db = scope.ServiceProvider.GetRequiredService<ApplicationDbContext>();
            var section = await TestData.CreateSectionAsync(db);
            var student = TestData.NewStudent(section, "1");
            db.Students.Add(student);
            await db.SaveChangesAsync();
            studentId = student.Id;
        }

        using (var scope = _factory.Services.CreateScope())
        {
            var service = scope.ServiceProvider.GetRequiredService<IStudentService>();
            await Assert.ThrowsAsync<ConflictException>(() => service.DeleteAsync(studentId, CancellationToken.None));
        }

        using (var scope = _factory.Services.CreateScope())
        {
            var db = scope.ServiceProvider.GetRequiredService<ApplicationDbContext>();
            var stillThere = await db.Students.AnyAsync(s => s.Id == studentId); // global filter hides deleted rows
            Assert.True(stillThere, "The student must not have been soft-deleted.");
        }
    }

    [Fact]
    public async Task Deleting_a_student_with_no_history_still_works()
    {
        int studentId;
        using (var scope = _factory.Services.CreateScope())
        {
            var db = scope.ServiceProvider.GetRequiredService<ApplicationDbContext>();
            var section = await TestData.CreateSectionAsync(db);
            var student = TestData.NewStudent(section, "2");
            student.Enrollments.Clear(); // created by mistake, never enrolled in a period
            db.Students.Add(student);
            await db.SaveChangesAsync();
            studentId = student.Id;
        }

        using var scope2 = _factory.Services.CreateScope();
        var service = scope2.ServiceProvider.GetRequiredService<IStudentService>();
        await service.DeleteAsync(studentId, CancellationToken.None); // must not throw
    }
}
