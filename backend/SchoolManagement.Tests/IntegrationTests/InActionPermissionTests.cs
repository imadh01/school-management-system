// File: backend/SchoolManagement.Tests/IntegrationTests/InActionPermissionTests.cs
//
// Permission decisions made INSIDE actions, which [Authorize] attributes and AccessMatrixTests
// can't see. Each of these was silently broken after D1 removed permissions/roles from the JWT
// (the code read User.HasClaim / User.IsInRole, which then always returned false):
//
//   1. Attendance.Manage holders (Admin, Supervisor, Clerk) may mark ANY class.
//   2. Only Admin may change attendance older than the edit window.
//   3. Only Students.ViewSensitive holders see full Aadhaar numbers.

using System.Net;
using System.Text.Json;
using Microsoft.Extensions.DependencyInjection;
using SchoolManagement.Application.DTOs.Attendance;
using SchoolManagement.Application.Services;
using SchoolManagement.Domain.Constants;
using SchoolManagement.Domain.Entities;
using SchoolManagement.Infrastructure.Persistence;
using SchoolManagement.Tests.Support;
using Xunit;

namespace SchoolManagement.Tests.IntegrationTests;

[Collection("LegacyApi")]
public class InActionPermissionTests
{
    private readonly CustomWebApplicationFactory _factory;
    private readonly HttpClient _client;

    public InActionPermissionTests(CustomWebApplicationFactory factory)
    {
        _factory = factory;
        _client = factory.CreateClient();
    }

    private static DateOnly Today => DateOnly.FromDateTime(DateTime.UtcNow);

    // ─────────────────────────────────────────────────────────────────
    // 1. Attendance scope
    // ─────────────────────────────────────────────────────────────────

    [Theory]
    [InlineData(RoleNames.Admin)]
    [InlineData(RoleNames.Supervisor)]
    [InlineData(RoleNames.Clerk)]
    public async Task Attendance_Manage_holders_can_mark_a_class_they_do_not_teach(string role)
    {
        var (section, student) = await CreateClassWithStudentAsync();

        var response = await SaveAttendanceAsync(role, section, student, Today);

        Assert.Equal(HttpStatusCode.OK, response.StatusCode);
    }

    [Fact]
    public async Task A_teacher_who_does_not_teach_the_class_is_still_refused()
    {
        // The Teacher role passes the Attendance.Write policy (it holds Attendance.Mark), but the
        // service limits it to its own classes. This user has no Teacher record, so: refused.
        var (section, student) = await CreateClassWithStudentAsync();

        var response = await SaveAttendanceAsync(RoleNames.Teacher, section, student, Today);

        Assert.Equal(HttpStatusCode.Forbidden, response.StatusCode);
    }

    // ─────────────────────────────────────────────────────────────────
    // 2. Edit window
    // ─────────────────────────────────────────────────────────────────

    [Fact]
    public async Task Admin_can_change_attendance_older_than_the_edit_window()
    {
        var (section, student) = await CreateClassWithStudentAsync();
        var old = Today.AddDays(-(AttendanceRules.EditWindowDays + 3));

        var response = await SaveAttendanceAsync(RoleNames.Admin, section, student, old);

        Assert.Equal(HttpStatusCode.OK, response.StatusCode);
    }

    [Fact]
    public async Task Supervisor_cannot_change_attendance_older_than_the_edit_window()
    {
        var (section, student) = await CreateClassWithStudentAsync();
        var old = Today.AddDays(-(AttendanceRules.EditWindowDays + 3));

        var response = await SaveAttendanceAsync(RoleNames.Supervisor, section, student, old);

        Assert.Equal(HttpStatusCode.UnprocessableEntity, response.StatusCode);
    }

    // ─────────────────────────────────────────────────────────────────
    // 3. Sensitive identity numbers
    // ─────────────────────────────────────────────────────────────────

    [Theory]
    [InlineData(RoleNames.Admin, false)]
    [InlineData(RoleNames.Supervisor, false)]
    [InlineData(RoleNames.Clerk, true)]
    [InlineData(RoleNames.Teacher, true)]
    public async Task Aadhaar_is_shown_in_full_only_to_Students_ViewSensitive_holders(string role, bool expectMasked)
    {
        var aadhaar = Random.Shared.NextInt64(100_000_000_000, 999_999_999_999).ToString();
        int studentId;
        using (var scope = _factory.Services.CreateScope())
        {
            var db = scope.ServiceProvider.GetRequiredService<ApplicationDbContext>();
            var section = await TestData.CreateSectionAsync(db);
            var student = TestData.NewStudent(section, "1");
            student.IdentityDocument = new StudentIdentityDocument { AadhaarNumber = aadhaar };
            db.Students.Add(student);
            await db.SaveChangesAsync();
            studentId = student.Id;
        }

        using var request = await RoleSessions.RequestAsAsync(_factory, role, HttpMethod.Get, $"/api/students/{studentId}");
        var response = await _client.SendAsync(request);

        Assert.Equal(HttpStatusCode.OK, response.StatusCode);
        using var json = JsonDocument.Parse(await response.Content.ReadAsStringAsync());
        var identity = json.RootElement.GetProperty("identity");
        Assert.Equal(expectMasked, identity.GetProperty("isMasked").GetBoolean());
        var shown = identity.GetProperty("aadhaarNumber").GetString();
        if (expectMasked)
            Assert.NotEqual(aadhaar, shown);
        else
            Assert.Equal(aadhaar, shown);
    }

    // ─────────────────────────────────────────────────────────────────
    // Helpers
    // ─────────────────────────────────────────────────────────────────

    /// <summary>A class with no class teacher and one active student.</summary>
    private async Task<(ClassSection Section, Student Student)> CreateClassWithStudentAsync()
    {
        using var scope = _factory.Services.CreateScope();
        var db = scope.ServiceProvider.GetRequiredService<ApplicationDbContext>();
        var section = await TestData.CreateSectionAsync(db);
        var student = TestData.NewStudent(section, "1");
        db.Students.Add(student);
        await db.SaveChangesAsync();
        return (section, student);
    }

    private async Task<HttpResponseMessage> SaveAttendanceAsync(
        string role, ClassSection section, Student student, DateOnly date)
    {
        var body = new SaveAttendanceRequest(
            section.Id, date, SubjectId: null,
            new List<AttendanceEntry> { new(student.Id, AttendanceRules.Present, null) });

        using var request = await RoleSessions.RequestAsAsync(_factory, role, HttpMethod.Put, "/api/attendance", body);
        return await _client.SendAsync(request);
    }
}
