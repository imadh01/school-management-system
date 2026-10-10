using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.DependencyInjection;
using SchoolManagement.Application.DTOs.Admissions;
using SchoolManagement.Application.DTOs.Students;
using SchoolManagement.Application.Interfaces;
using SchoolManagement.Domain.Exceptions;
using SchoolManagement.Infrastructure.Persistence;
using SchoolManagement.Tests.Support;
using Xunit;

namespace SchoolManagement.Tests.DatabaseRules;

/// <summary>
/// Bug #4. Capacity is checked in code ("count, then insert"), so two staff enrolling into the
/// last seat at the same moment can both pass the check. Exactly one must win.
/// Before the fix this fails most runs (it is a race, so a lucky run can pass: that is why it
/// repeats the race 20 times). After the fix (row lock on the class section) it must always pass.
/// The second test does the same for two students moving into the last seat of a class.
/// </summary>
[Collection("Database")]
public class EnrolCapacityRaceTests
{
    private const int Rounds = 20;
    private readonly SqlServerWebApplicationFactory _factory;

    public EnrolCapacityRaceTests(SqlServerWebApplicationFactory factory) => _factory = factory;

    [Fact]
    public async Task Two_simultaneous_enrolments_into_the_last_seat_admit_exactly_one_student()
    {
        for (var round = 1; round <= Rounds; round++)
        {
            int sectionId;
            var jobs = new List<(int AdmissionId, EnrollAdmissionRequest Request)>();

            using (var scope = _factory.Services.CreateScope())
            {
                var db = scope.ServiceProvider.GetRequiredService<ApplicationDbContext>();
                var section = await TestData.CreateSectionAsync(db, capacity: 1);
                sectionId = section.Id;

                for (var i = 1; i <= 2; i++)
                {
                    var (admission, guardian) = await TestData.CreateAdmittedAdmissionAsync(db, section);
                    jobs.Add((admission.Id, TestData.EnrolRequest(section, guardian, rollNumber: i.ToString())));
                }
            }

            var gate = new TaskCompletionSource();
            var attempts = jobs.Select(job => Task.Run(async () =>
            {
                await gate.Task; // release both together
                using var scope = _factory.Services.CreateScope();
                var service = scope.ServiceProvider.GetRequiredService<IAdmissionService>();
                try
                {
                    await service.EnrollAsync(job.AdmissionId, job.Request, CancellationToken.None);
                    return (Exception?)null;
                }
                catch (Exception ex)
                {
                    return ex;
                }
            })).ToList();

            gate.SetResult();
            var outcomes = await Task.WhenAll(attempts);

            using var verify = _factory.Services.CreateScope();
            var verifyDb = verify.ServiceProvider.GetRequiredService<ApplicationDbContext>();
            var seatsTaken = await verifyDb.Students.CountAsync(s => s.ClassSectionId == sectionId);

            Assert.True(seatsTaken == 1, $"Round {round}: {seatsTaken} students hold a seat in a class with capacity 1.");
            Assert.Single(outcomes, o => o is null);
            Assert.Single(outcomes, o => o is ConflictException);
        }
    }

    [Fact]
    public async Task Two_simultaneous_class_moves_into_the_last_seat_admit_exactly_one_student()
    {
        for (var round = 1; round <= Rounds; round++)
        {
            int targetId;
            var jobs = new List<(int StudentId, UpdateStudentRequest Request)>();

            using (var scope = _factory.Services.CreateScope())
            {
                var db = scope.ServiceProvider.GetRequiredService<ApplicationDbContext>();
                var source = await TestData.CreateSectionAsync(db);                       // no capacity limit
                var target = await TestData.CreateSiblingSectionAsync(db, source, "B", capacity: 1);
                targetId = target.Id;

                for (var i = 1; i <= 2; i++)
                {
                    var student = TestData.NewStudent(source, rollNumber: i.ToString());
                    db.Students.Add(student);
                    await db.SaveChangesAsync();
                    jobs.Add((student.Id, TestData.MoveRequest(student, target.Id, rollNumber: i.ToString())));
                }
            }

            var gate = new TaskCompletionSource();
            var attempts = jobs.Select(job => Task.Run(async () =>
            {
                await gate.Task; // release both together
                using var scope = _factory.Services.CreateScope();
                var service = scope.ServiceProvider.GetRequiredService<IStudentService>();
                try
                {
                    await service.UpdateAsync(job.StudentId, job.Request, includeSensitive: false, CancellationToken.None);
                    return (Exception?)null;
                }
                catch (Exception ex)
                {
                    return ex;
                }
            })).ToList();

            gate.SetResult();
            var outcomes = await Task.WhenAll(attempts);

            using var verify = _factory.Services.CreateScope();
            var verifyDb = verify.ServiceProvider.GetRequiredService<ApplicationDbContext>();
            var seatsTaken = await verifyDb.Students.CountAsync(s => s.ClassSectionId == targetId && s.Status == "Active");

            Assert.True(seatsTaken == 1, $"Round {round}: {seatsTaken} students were moved into a class with capacity 1.");
            Assert.Single(outcomes, o => o is null);
            Assert.Single(outcomes, o => o is ConflictException);
        }
    }
}
