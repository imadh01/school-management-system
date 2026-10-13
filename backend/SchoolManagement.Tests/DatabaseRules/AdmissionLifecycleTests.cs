using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.DependencyInjection;
using SchoolManagement.Application.DTOs.Admissions;
using SchoolManagement.Application.Interfaces;
using SchoolManagement.Domain.Entities;
using SchoolManagement.Domain.Exceptions;
using SchoolManagement.Infrastructure.Persistence;
using SchoolManagement.Tests.Support;
using Xunit;

namespace SchoolManagement.Tests.DatabaseRules;

/// <summary>
/// U2 (Admissions): an application can be edited only while it is in progress, and one application
/// can become at most one student, even when two people press "Enrol" at the same moment.
/// </summary>
[Collection("Database")]
public class AdmissionLifecycleTests
{
    private const int Rounds = 10;
    private readonly SqlServerWebApplicationFactory _factory;

    public AdmissionLifecycleTests(SqlServerWebApplicationFactory factory) => _factory = factory;

    [Theory]
    [InlineData("Enrolled")]
    [InlineData("Rejected")]
    public async Task Editing_a_closed_application_is_refused_and_changes_nothing(string status)
    {
        var (admissionId, request) = await CreateApplicationAsync(status);

        using (var scope = _factory.Services.CreateScope())
        {
            var service = scope.ServiceProvider.GetRequiredService<IAdmissionService>();
            var ex = await Assert.ThrowsAsync<ConflictException>(() =>
                service.UpdateAsync(admissionId, request with { FirstName = "Changed" }, CancellationToken.None));
            Assert.Contains("can no longer be edited", ex.Message);
        }

        using var verify = _factory.Services.CreateScope();
        var db = verify.ServiceProvider.GetRequiredService<ApplicationDbContext>();
        var stored = await db.Admissions.AsNoTracking().SingleAsync(a => a.Id == admissionId);
        Assert.Equal("Test", stored.FirstName);
        Assert.Equal(status, stored.Status);
    }

    [Theory]
    [InlineData("Registered")]
    [InlineData("Admitted")]
    public async Task Editing_an_application_in_progress_still_works(string status)
    {
        var (admissionId, request) = await CreateApplicationAsync(status);

        using var scope = _factory.Services.CreateScope();
        var service = scope.ServiceProvider.GetRequiredService<IAdmissionService>();
        var saved = await service.UpdateAsync(admissionId, request with { FirstName = "Changed" }, CancellationToken.None);

        Assert.Equal("Changed", saved.FirstName);
        Assert.Equal(status, saved.Status);
    }

    [Fact]
    public async Task Deleting_an_enrolled_application_is_refused_and_keeps_it()
    {
        var (admissionId, _) = await CreateApplicationAsync("Enrolled");

        using (var scope = _factory.Services.CreateScope())
        {
            var service = scope.ServiceProvider.GetRequiredService<IAdmissionService>();
            var ex = await Assert.ThrowsAsync<ConflictException>(() => service.DeleteAsync(admissionId, CancellationToken.None));
            Assert.Contains("cannot be deleted", ex.Message);
        }

        using var verify = _factory.Services.CreateScope();
        var db = verify.ServiceProvider.GetRequiredService<ApplicationDbContext>();
        Assert.False(await db.Admissions.IgnoreQueryFilters().AsNoTracking()
            .Where(a => a.Id == admissionId).Select(a => a.IsDeleted).SingleAsync());
    }

    [Theory]
    [InlineData("Registered")]
    [InlineData("Admitted")]
    [InlineData("Rejected")]
    public async Task Deleting_an_application_that_is_not_enrolled_still_works(string status)
    {
        var (admissionId, _) = await CreateApplicationAsync(status);

        using (var scope = _factory.Services.CreateScope())
            await scope.ServiceProvider.GetRequiredService<IAdmissionService>().DeleteAsync(admissionId, CancellationToken.None);

        using var verify = _factory.Services.CreateScope();
        var db = verify.ServiceProvider.GetRequiredService<ApplicationDbContext>();
        Assert.True(await db.Admissions.IgnoreQueryFilters().AsNoTracking()
            .Where(a => a.Id == admissionId).Select(a => a.IsDeleted).SingleAsync());
    }

    [Fact]
    public async Task Enrolling_the_same_application_twice_is_refused_and_leaves_one_student()
    {
        int admissionId;
        EnrollAdmissionRequest first, second;
        using (var scope = _factory.Services.CreateScope())
        {
            var db = scope.ServiceProvider.GetRequiredService<ApplicationDbContext>();
            var section = await TestData.CreateSectionAsync(db);
            var (admission, guardian) = await TestData.CreateAdmittedAdmissionAsync(db, section);
            admissionId = admission.Id;
            first = TestData.EnrolRequest(section, guardian, rollNumber: "1");
            second = TestData.EnrolRequest(section, guardian, rollNumber: "2"); // different numbers: only the status stops it
        }

        using (var scope = _factory.Services.CreateScope())
            await scope.ServiceProvider.GetRequiredService<IAdmissionService>()
                .EnrollAsync(admissionId, first, CancellationToken.None);

        using (var scope = _factory.Services.CreateScope())
        {
            var ex = await Assert.ThrowsAsync<ConflictException>(() =>
                scope.ServiceProvider.GetRequiredService<IAdmissionService>()
                    .EnrollAsync(admissionId, second, CancellationToken.None));
            Assert.Contains("Enrolled", ex.Message);
        }

        Assert.Equal(1, await CountStudentsFromAsync(admissionId));
    }

    [Fact]
    public async Task Two_simultaneous_enrolments_of_the_same_application_create_exactly_one_student()
    {
        for (var round = 1; round <= Rounds; round++)
        {
            int admissionId;
            var requests = new List<EnrollAdmissionRequest>();
            using (var scope = _factory.Services.CreateScope())
            {
                var db = scope.ServiceProvider.GetRequiredService<ApplicationDbContext>();
                var sectionA = await TestData.CreateSectionAsync(db);
                var sectionB = await TestData.CreateSectionAsync(db);
                var (admission, guardian) = await TestData.CreateAdmittedAdmissionAsync(db, sectionA);
                admissionId = admission.Id;
                // Two staff, two different classes and admission numbers: no seat lock or number clash
                // is shared between them, so only the per-application rule can stop the second one.
                requests.Add(TestData.EnrolRequest(sectionA, guardian, rollNumber: "1"));
                requests.Add(TestData.EnrolRequest(sectionB, guardian, rollNumber: "1"));
            }

            var gate = new TaskCompletionSource();
            var attempts = requests.Select(request => Task.Run(async () =>
            {
                await gate.Task;
                using var scope = _factory.Services.CreateScope();
                try
                {
                    await scope.ServiceProvider.GetRequiredService<IAdmissionService>()
                        .EnrollAsync(admissionId, request, CancellationToken.None);
                    return (Exception?)null;
                }
                catch (Exception ex)
                {
                    return ex;
                }
            })).ToList();

            gate.SetResult();
            var outcomes = await Task.WhenAll(attempts);

            Assert.True(await CountStudentsFromAsync(admissionId) == 1, $"Round {round}: expected exactly one student.");
            Assert.Single(outcomes, o => o is null);
            Assert.Single(outcomes, o => o is ConflictException);
        }
    }

    // ------------------------------------------------------------------ helpers

    /// <summary>An application with one guardian in the given status, and an edit request matching it.</summary>
    private async Task<(int AdmissionId, UpdateAdmissionRequest Request)> CreateApplicationAsync(string status)
    {
        ClassSection section;
        Admission admission;
        AdmissionGuardian guardian;
        using (var setup = _factory.Services.CreateScope())
        {
            var setupDb = setup.ServiceProvider.GetRequiredService<ApplicationDbContext>();
            section = await TestData.CreateSectionAsync(setupDb);
            (admission, guardian) = await TestData.CreateAdmittedAdmissionAsync(setupDb, section);

            // Reach the status the real way: the database's CHECK rules require a rejection reason,
            // and the enrolment numbers for an enrolled application.
            var service = setup.ServiceProvider.GetRequiredService<IAdmissionService>();
            if (status == "Rejected")
                await service.RejectAsync(admission.Id, new RejectAdmissionRequest("Test rejection"), CancellationToken.None);
            else if (status == "Enrolled")
                await service.EnrollAsync(admission.Id, TestData.EnrolRequest(section, guardian, rollNumber: "1"), CancellationToken.None);
            else if (status == "Registered")
            {
                admission.Status = "Registered";
                await setupDb.SaveChangesAsync();
            }
        }

        using var scope = _factory.Services.CreateScope();
        var db = scope.ServiceProvider.GetRequiredService<ApplicationDbContext>();
        admission = await db.Admissions.AsNoTracking().SingleAsync(a => a.Id == admission.Id); // fresh row version
        Assert.Equal(status, admission.Status);

        var request = new UpdateAdmissionRequest(
            FirstName: admission.FirstName, MiddleName: null, LastName: admission.LastName,
            Gender: admission.Gender, DateOfBirth: admission.DateOfBirth,
            AppliedForClassSectionId: section.Id,
            AdmissionType: admission.AdmissionType, PreviousSchool: null,
            Phone: admission.Phone, Email: null,
            Addresses: null,
            Remarks: null,
            Guardians: new List<AdmissionGuardianRequest>
            {
                new(guardian.RelationType, guardian.Name, guardian.Mobile, null, true),
            },
            RowVersion: Convert.ToBase64String(admission.RowVersion));
        return (admission.Id, request);
    }

    private async Task<int> CountStudentsFromAsync(int admissionId)
    {
        using var scope = _factory.Services.CreateScope();
        var db = scope.ServiceProvider.GetRequiredService<ApplicationDbContext>();
        return await db.Students.IgnoreQueryFilters().CountAsync(s => s.AdmissionId == admissionId);
    }
}
