using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.DependencyInjection;
using SchoolManagement.Application.DTOs.Students;
using SchoolManagement.Application.Interfaces;
using SchoolManagement.Application.Validators;
using SchoolManagement.Domain.Entities;
using SchoolManagement.Infrastructure.Persistence;
using SchoolManagement.Tests.Support;
using Xunit;

namespace SchoolManagement.Tests.DatabaseRules;

/// <summary>
/// Fix 4. PUT /api/students/{id}/identity replaces all six identity values, so two people editing at once used to
/// overwrite each other silently. It now takes the student's rowVersion like the other edits: a stale version is
/// refused (409 CONCURRENCY_CONFLICT) and a successful save moves the version on, so the next save needs the new one.
/// (Before the fix the request had no RowVersion at all, so this file did not even compile against it.)
/// </summary>
[Collection("Database")]
public class StudentIdentityConcurrencyTests
{
    private readonly SqlServerWebApplicationFactory _factory;

    public StudentIdentityConcurrencyTests(SqlServerWebApplicationFactory factory) => _factory = factory;

    [Fact]
    public async Task Saving_identity_with_the_current_version_works_and_returns_a_new_version()
    {
        var (studentId, version) = await NewStudentAsync();
        var aadhaar = NewAadhaar();

        var saved = await UpdateIdentityAsync(studentId, Request(version, aadhaar: aadhaar));

        Assert.NotEqual(version, saved.RowVersion);
        Assert.Equal(aadhaar, saved.Identity.AadhaarNumber);
    }

    [Fact]
    public async Task A_second_identity_save_with_the_old_version_is_refused()
    {
        // Two people open the edit page at the same moment: both hold the same version.
        var (studentId, version) = await NewStudentAsync();
        var firstAadhaar = NewAadhaar();
        var secondAadhaar = NewAadhaar();
        await UpdateIdentityAsync(studentId, Request(version, aadhaar: firstAadhaar)); // first save wins

        await Assert.ThrowsAsync<DbUpdateConcurrencyException>(() =>
            UpdateIdentityAsync(studentId, Request(version, aadhaar: secondAadhaar)));

        Assert.Equal(firstAadhaar, await StoredAadhaarAsync(studentId)); // the second change did not overwrite the first
    }

    [Fact]
    public async Task The_next_identity_save_works_with_the_version_the_last_save_returned()
    {
        var (studentId, version) = await NewStudentAsync();
        var first = await UpdateIdentityAsync(studentId, Request(version, aadhaar: NewAadhaar()));
        var secondAadhaar = NewAadhaar();

        var second = await UpdateIdentityAsync(studentId, Request(first.RowVersion, aadhaar: secondAadhaar));

        Assert.Equal(secondAadhaar, second.Identity.AadhaarNumber);
    }

    [Fact]
    public async Task Saving_identity_after_the_main_student_form_changed_is_refused_until_reloaded()
    {
        // The edit page saves the student first and the identity second with the version the first save returned.
        // A different person saving the student form in between makes that version old.
        var (studentId, version) = await NewStudentAsync();
        await ChangeStudentFirstNameAsync(studentId, version); // somebody else saves the main form

        await Assert.ThrowsAsync<DbUpdateConcurrencyException>(() =>
            UpdateIdentityAsync(studentId, Request(version, aadhaar: NewAadhaar())));
    }

    [Theory]
    [InlineData("")]
    [InlineData("not base64!")]
    [InlineData("AAAA")] // valid base64 but not the 8 bytes of a rowversion
    public void A_missing_or_malformed_version_is_a_validation_error_on_rowVersion(string rowVersion)
    {
        var result = new UpdateStudentIdentityRequestValidator().Validate(Request(rowVersion, aadhaar: null));

        Assert.Contains(result.Errors, e => e.PropertyName == nameof(UpdateStudentIdentityRequest.RowVersion));
    }

    [Fact]
    public void A_valid_version_passes_validation()
    {
        var valid = Convert.ToBase64String(new byte[8]);

        var result = new UpdateStudentIdentityRequestValidator().Validate(Request(valid, aadhaar: null));

        Assert.True(result.IsValid);
    }

    // ------------------------------------------------------------------ helpers

    // The test database is shared by every test in a run and Aadhaar numbers are unique across students, so each
    // test must use a number nobody else has used (fixed literals made all but the first test hit the 409).
    private static long _aadhaarCounter = DateTime.UtcNow.Ticks % 100_000_000_000L;

    private static string NewAadhaar() => Interlocked.Increment(ref _aadhaarCounter).ToString("D12");

    private static UpdateStudentIdentityRequest Request(string rowVersion, string? aadhaar) =>
        new(AadhaarNumber: aadhaar, PassportNumber: null, PassportExpiry: null, VisaType: null, VisaExpiry: null,
            EmiratesIdNumber: null, RowVersion: rowVersion);

    private async Task<(int Id, string Version)> NewStudentAsync()
    {
        using var scope = _factory.Services.CreateScope();
        var db = scope.ServiceProvider.GetRequiredService<ApplicationDbContext>();
        var section = await TestData.CreateSectionAsync(db);
        var student = TestData.NewStudent(section, "1");
        db.Students.Add(student);
        await db.SaveChangesAsync();
        return (student.Id, Convert.ToBase64String(student.RowVersion));
    }

    private async Task<StudentResponse> UpdateIdentityAsync(int studentId, UpdateStudentIdentityRequest request)
    {
        using var scope = _factory.Services.CreateScope();
        var service = scope.ServiceProvider.GetRequiredService<IStudentService>();
        return await service.UpdateIdentityAsync(studentId, request, CancellationToken.None);
    }

    private async Task ChangeStudentFirstNameAsync(int studentId, string version)
    {
        using var scope = _factory.Services.CreateScope();
        var db = scope.ServiceProvider.GetRequiredService<ApplicationDbContext>();
        var current = await db.Students.AsNoTracking().SingleAsync(s => s.Id == studentId);
        var request = TestData.MoveRequest(current, current.ClassSectionId, current.RollNumber, version);
        var service = scope.ServiceProvider.GetRequiredService<IStudentService>();
        await service.UpdateAsync(studentId, request with { FirstName = "Changed" }, includeSensitive: false, CancellationToken.None);
    }

    private async Task<string?> StoredAadhaarAsync(int studentId)
    {
        using var scope = _factory.Services.CreateScope();
        var db = scope.ServiceProvider.GetRequiredService<ApplicationDbContext>();
        return await db.StudentIdentityDocuments.AsNoTracking()
            .Where(d => d.StudentId == studentId).Select(d => d.AadhaarNumber).SingleOrDefaultAsync();
    }
}
