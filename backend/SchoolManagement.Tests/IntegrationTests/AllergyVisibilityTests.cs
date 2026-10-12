// File: backend/SchoolManagement.Tests/IntegrationTests/AllergyVisibilityTests.cs
//
// U1 decision: everyone who can read a student sees WHETHER they have allergies (a safety flag);
// the allergy TEXT needs Students.ViewSensitive. A caller who could not see the text must not be
// able to wipe it by saving the edit form.

using System.Net;
using System.Text.Json;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.DependencyInjection;
using SchoolManagement.Application.DTOs.Students;
using SchoolManagement.Domain.Constants;
using SchoolManagement.Domain.Entities;
using SchoolManagement.Infrastructure.Persistence;
using SchoolManagement.Tests.Support;
using Xunit;

namespace SchoolManagement.Tests.IntegrationTests;

[Collection("LegacyApi")]
public class AllergyVisibilityTests
{
    private const string Allergy = "Allergic to penicillin";

    private readonly CustomWebApplicationFactory _factory;
    private readonly HttpClient _client;

    public AllergyVisibilityTests(CustomWebApplicationFactory factory)
    {
        _factory = factory;
        _client = factory.CreateClient();
    }

    [Theory]
    [InlineData(RoleNames.Clerk, false)]       // Students.View only
    [InlineData(RoleNames.Supervisor, true)]   // + Students.ViewSensitive
    [InlineData(RoleNames.Admin, true)]
    public async Task List_shows_the_flag_to_everyone_and_the_text_only_with_ViewSensitive(string role, bool seesText)
    {
        var studentId = await CreateStudentWithAllergyAsync();

        using var json = await GetJsonAsync(role, "/api/students");
        var row = json.RootElement.EnumerateArray().Single(e => e.GetProperty("id").GetInt32() == studentId);

        Assert.True(row.GetProperty("hasAllergies").GetBoolean());
        var text = row.GetProperty("allergies");
        if (seesText) Assert.Equal(Allergy, text.GetString());
        else Assert.Equal(JsonValueKind.Null, text.ValueKind);
    }

    [Theory]
    [InlineData(RoleNames.Clerk, false)]
    [InlineData(RoleNames.Supervisor, true)]
    public async Task Detail_shows_the_flag_to_everyone_and_the_text_only_with_ViewSensitive(string role, bool seesText)
    {
        var studentId = await CreateStudentWithAllergyAsync();

        using var json = await GetJsonAsync(role, $"/api/students/{studentId}");

        Assert.True(json.RootElement.GetProperty("hasAllergies").GetBoolean());
        var text = json.RootElement.GetProperty("health").GetProperty("allergies");
        if (seesText) Assert.Equal(Allergy, text.GetString());
        else Assert.Equal(JsonValueKind.Null, text.ValueKind);
    }

    [Fact]
    public async Task Saving_without_ViewSensitive_keeps_the_allergy_text_it_could_not_see()
    {
        // A custom role that may edit students but not see sensitive data.
        var roleName = await CreateRoleAsync("AllergyEditor",
            Permissions.StudentsView, Permissions.StudentsEdit, Permissions.ClassSectionsView);
        var studentId = await CreateStudentWithAllergyAsync();

        Student loaded;
        using (var scope = _factory.Services.CreateScope())
        {
            var db = scope.ServiceProvider.GetRequiredService<ApplicationDbContext>();
            loaded = await db.Students.AsNoTracking().SingleAsync(s => s.Id == studentId);
        }

        // What the edit form sends back: the health block with allergies empty (it was never shown).
        var body = TestData.MoveRequest(loaded, loaded.ClassSectionId, loaded.RollNumber) with
        {
            Health = new StudentHealthDto("O+", null, null, "Updated notes", null, null, null),
        };
        using var request = await RoleSessions.RequestAsAsync(_factory, roleName, HttpMethod.Put, $"/api/students/{studentId}", body);
        var response = await _client.SendAsync(request);
        Assert.Equal(HttpStatusCode.OK, response.StatusCode);

        using var scope2 = _factory.Services.CreateScope();
        var db2 = scope2.ServiceProvider.GetRequiredService<ApplicationDbContext>();
        var health = await db2.StudentHealth.AsNoTracking().SingleAsync(h => h.StudentId == studentId);
        Assert.Equal(Allergy, health.Allergies);          // kept
        Assert.Equal("Updated notes", health.MedicalNotes); // the rest was saved
    }

    // ------------------------------------------------------------------ helpers

    private async Task<int> CreateStudentWithAllergyAsync()
    {
        using var scope = _factory.Services.CreateScope();
        var db = scope.ServiceProvider.GetRequiredService<ApplicationDbContext>();
        var section = await TestData.CreateSectionAsync(db);
        var student = TestData.NewStudent(section, "1");
        student.Health = new StudentHealth { Allergies = Allergy };
        db.Students.Add(student);
        await db.SaveChangesAsync();
        return student.Id;
    }

    private async Task<string> CreateRoleAsync(string prefix, params string[] permissionNames)
    {
        using var scope = _factory.Services.CreateScope();
        var db = scope.ServiceProvider.GetRequiredService<ApplicationDbContext>();
        var permissions = await db.Permissions.Where(p => permissionNames.Contains(p.Name)).ToListAsync();
        var role = new Role { Name = prefix + TestData.Unique(), IsActive = true };
        foreach (var p in permissions)
            role.RolePermissions.Add(new RolePermission { Permission = p });
        db.Roles.Add(role);
        await db.SaveChangesAsync();
        return role.Name;
    }

    private async Task<JsonDocument> GetJsonAsync(string role, string url)
    {
        using var request = await RoleSessions.RequestAsAsync(_factory, role, HttpMethod.Get, url);
        var response = await _client.SendAsync(request);
        Assert.Equal(HttpStatusCode.OK, response.StatusCode);
        return JsonDocument.Parse(await response.Content.ReadAsStringAsync());
    }
}
