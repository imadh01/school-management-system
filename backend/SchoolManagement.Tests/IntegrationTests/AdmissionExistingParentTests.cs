// G2: picking an EXISTING parent while registering an application, end-to-end through the real API and SQL Server:
//   - the search shows mobile numbers and children only to users who hold Parents.View (and the right to register);
//   - the server copies the parent's name / mobile / email itself and ignores what the form sent for them;
//   - an unknown or deleted parent is 404, the same parent twice is 422, linking without Parents.View is 403
//     while keeping a link the application already has is allowed;
//   - the matches endpoint suggests the linked parent first, or ParentRemoved when it was deleted;
//   - enrolling reuses the linked parent (no duplicate), an override is written to the audit log, and a deleted
//     parent stops the enrolment with a clear message and creates nothing.

using System.Net;
using System.Net.Http.Headers;
using System.Net.Http.Json;
using System.Text.Json;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.DependencyInjection;
using SchoolManagement.Application.DTOs.Admissions;
using SchoolManagement.Application.DTOs.Auth;
using SchoolManagement.Application.Interfaces;
using SchoolManagement.Domain.Constants;
using SchoolManagement.Domain.Entities;
using SchoolManagement.Infrastructure.Persistence;
using SchoolManagement.Tests.Support;
using Xunit;

namespace SchoolManagement.Tests.IntegrationTests;

[Collection("LegacyApi")]
public class AdmissionExistingParentTests
{
    private const string Password = "Parent@12345";
    private static readonly JsonSerializerOptions Json = new(JsonSerializerDefaults.Web);

    private readonly CustomWebApplicationFactory _factory;
    private readonly HttpClient _client;

    public AdmissionExistingParentTests(CustomWebApplicationFactory factory)
    {
        _factory = factory;
        _client = factory.CreateClient();
    }

    // ─────────────────────────────────────────────────────────────────
    // Search: who may see what
    // ─────────────────────────────────────────────────────────────────

    [Fact]
    public async Task Search_returns_mobile_and_children_to_a_user_with_Parents_View()
    {
        var (parentId, mobile, childName) = await CreateParentWithChildAsync();
        var staff = await CreateUserAsync(Permissions.ParentsView, Permissions.AdmissionsCreate);

        var response = await GetAsync(staff, $"/api/admissions/parent-search?q={mobile[^6..]}");

        Assert.Equal(HttpStatusCode.OK, response.StatusCode);
        var hits = await ReadAsync<List<ParentCandidateResponse>>(response);
        var hit = Assert.Single(hits, h => h.ParentId == parentId);
        Assert.Equal(mobile, hit.Mobile);
        Assert.Contains(childName, hit.LinkedChildren);
    }

    [Fact]
    public async Task Search_is_refused_without_Parents_View_even_for_a_user_who_can_see_scoped_parents()
    {
        var (_, mobile, _) = await CreateParentWithChildAsync();
        // Parents.ViewScoped (a teacher's "my own pupils' parents") is NOT enough: it never reveals other families.
        var staff = await CreateUserAsync(Permissions.ParentsViewScoped, Permissions.AdmissionsCreate);

        var response = await GetAsync(staff, $"/api/admissions/parent-search?q={mobile[^6..]}");

        Assert.Equal(HttpStatusCode.Forbidden, response.StatusCode);
        Assert.DoesNotContain(mobile, await response.Content.ReadAsStringAsync());
    }

    [Fact]
    public async Task Search_is_refused_to_someone_who_cannot_register_or_edit_applications()
    {
        var staff = await CreateUserAsync(Permissions.ParentsView);

        var response = await GetAsync(staff, "/api/admissions/parent-search?q=abcdef");

        Assert.Equal(HttpStatusCode.Forbidden, response.StatusCode);
    }

    [Fact]
    public async Task Search_needs_two_letters_or_three_digits_so_one_keystroke_does_not_list_everybody()
    {
        await CreateParentWithChildAsync();
        var staff = await CreateUserAsync(Permissions.ParentsView, Permissions.AdmissionsEdit);

        foreach (var q in new[] { "", "a", "9", " " })
        {
            var response = await GetAsync(staff, $"/api/admissions/parent-search?q={Uri.EscapeDataString(q)}");
            Assert.Equal(HttpStatusCode.OK, response.StatusCode);
            Assert.Empty(await ReadAsync<List<ParentCandidateResponse>>(response));
        }
    }

    [Fact]
    public async Task Search_never_returns_a_deleted_parent()
    {
        var (parentId, mobile, _) = await CreateParentWithChildAsync();
        await SoftDeleteParentAsync(parentId);
        var staff = await CreateUserAsync(Permissions.ParentsView, Permissions.AdmissionsCreate);

        var hits = await ReadAsync<List<ParentCandidateResponse>>(await GetAsync(staff, $"/api/admissions/parent-search?q={mobile[^6..]}"));

        Assert.DoesNotContain(hits, h => h.ParentId == parentId);
    }

    [Fact]
    public async Task The_application_itself_never_carries_children_only_the_parent_id()
    {
        var (parentId, _, childName) = await CreateParentWithChildAsync();
        var admin = await CreateUserAsync(RoleNames.Admin, isRole: true);
        var section = await CreateSectionAsync();

        var created = await PostAsync(admin, "/api/admissions", NewApplication(section.Id, Linked(parentId)));
        var text = await created.Content.ReadAsStringAsync();

        Assert.Equal(HttpStatusCode.Created, created.StatusCode);
        Assert.DoesNotContain(childName, text);
        Assert.DoesNotContain("linkedChildren", text, StringComparison.OrdinalIgnoreCase);
        Assert.Contains($"\"parentId\":{parentId}", text);
    }

    // ─────────────────────────────────────────────────────────────────
    // Create / update: the server copies the parent
    // ─────────────────────────────────────────────────────────────────

    [Fact]
    public async Task Linking_a_parent_copies_the_parents_details_and_ignores_the_form_values()
    {
        var (parentId, mobile, _) = await CreateParentWithChildAsync();
        var admin = await CreateUserAsync(RoleNames.Admin, isRole: true);
        var section = await CreateSectionAsync();

        // The form claims a different name, mobile and e-mail (and even an invalid mobile): all of it is ignored.
        var guardian = new AdmissionGuardianRequest("Father", "Somebody Else", "not-a-number", "other@example.com", true, parentId);
        var response = await PostAsync(admin, "/api/admissions", NewApplication(section.Id, guardian));

        Assert.Equal(HttpStatusCode.Created, response.StatusCode);
        var created = await ReadAsync<AdmissionResponse>(response);
        var saved = Assert.Single(created.Guardians);
        Assert.Equal(parentId, saved.ParentId);
        Assert.Equal(mobile, saved.Mobile);
        Assert.Equal(await ParentNameAsync(parentId), saved.Name);
        Assert.NotEqual("other@example.com", saved.Email);
    }

    [Fact]
    public async Task An_unknown_parent_is_404_and_nothing_is_created()
    {
        var admin = await CreateUserAsync(RoleNames.Admin, isRole: true);
        var section = await CreateSectionAsync();
        var before = await CountAdmissionsAsync(section.Id);

        var response = await PostAsync(admin, "/api/admissions", NewApplication(section.Id, Linked(987_654_321)));

        Assert.Equal(HttpStatusCode.NotFound, response.StatusCode);
        Assert.Equal(before, await CountAdmissionsAsync(section.Id));
    }

    [Fact]
    public async Task A_deleted_parent_cannot_be_linked()
    {
        var (parentId, _, _) = await CreateParentWithChildAsync();
        await SoftDeleteParentAsync(parentId);
        var admin = await CreateUserAsync(RoleNames.Admin, isRole: true);
        var section = await CreateSectionAsync();

        var response = await PostAsync(admin, "/api/admissions", NewApplication(section.Id, Linked(parentId)));

        Assert.Equal(HttpStatusCode.NotFound, response.StatusCode);
    }

    [Fact]
    public async Task The_same_parent_twice_on_one_application_is_422()
    {
        var (parentId, _, _) = await CreateParentWithChildAsync();
        var admin = await CreateUserAsync(RoleNames.Admin, isRole: true);
        var section = await CreateSectionAsync();

        var father = new AdmissionGuardianRequest("Father", "x", null, null, true, parentId);
        var mother = new AdmissionGuardianRequest("Mother", "y", null, null, false, parentId);
        var response = await PostAsync(admin, "/api/admissions", NewApplication(section.Id, father, mother));

        Assert.Equal(HttpStatusCode.UnprocessableEntity, response.StatusCode);
        Assert.Contains("twice", await response.Content.ReadAsStringAsync());
    }

    [Fact]
    public async Task Linking_without_Parents_View_is_403_and_nothing_is_created()
    {
        var (parentId, _, _) = await CreateParentWithChildAsync();
        var clerk = await CreateUserAsync(Permissions.AdmissionsCreate, Permissions.AdmissionsView);
        var section = await CreateSectionAsync();
        var before = await CountAdmissionsAsync(section.Id);

        var response = await PostAsync(clerk, "/api/admissions", NewApplication(section.Id, Linked(parentId)));

        Assert.Equal(HttpStatusCode.Forbidden, response.StatusCode);
        Assert.Equal(before, await CountAdmissionsAsync(section.Id));
    }

    [Fact]
    public async Task A_user_without_Parents_View_can_keep_an_existing_link_but_cannot_add_a_new_one()
    {
        var (linkedParentId, _, _) = await CreateParentWithChildAsync();
        var (otherParentId, _, _) = await CreateParentWithChildAsync();
        var admin = await CreateUserAsync(RoleNames.Admin, isRole: true);
        var clerk = await CreateUserAsync(Permissions.AdmissionsEdit, Permissions.AdmissionsView);
        var section = await CreateSectionAsync();
        var created = await ReadAsync<AdmissionResponse>(
            await PostAsync(admin, "/api/admissions", NewApplication(section.Id, Linked(linkedParentId))));

        // Keeping the same link: fine, and the clerk's edited remarks are saved.
        var keep = await PutAsync(clerk, $"/api/admissions/{created.Id}",
            UpdateOf(created, "edited by clerk", Linked(linkedParentId)));
        Assert.Equal(HttpStatusCode.OK, keep.StatusCode);
        Assert.Equal(linkedParentId, (await ReadAsync<AdmissionResponse>(keep)).Guardians.Single().ParentId);

        // Adding a different parent: refused.
        var current = await ReadAsync<AdmissionResponse>(await GetAsync(admin, $"/api/admissions/{created.Id}"));
        var add = await PutAsync(clerk, $"/api/admissions/{created.Id}",
            UpdateOf(current, "again", Linked(linkedParentId), new AdmissionGuardianRequest("Mother", "m", null, null, false, otherParentId)));
        Assert.Equal(HttpStatusCode.Forbidden, add.StatusCode);
    }

    // ─────────────────────────────────────────────────────────────────
    // Matches
    // ─────────────────────────────────────────────────────────────────

    [Fact]
    public async Task Matches_suggest_the_linked_parent_first_even_when_the_number_on_the_form_is_different()
    {
        var (parentId, _, _) = await CreateParentWithChildAsync();
        var (admission, _, _) = await CreateAdmittedWithLinkAsync(parentId, guardianMobile: "9000000001");
        var admin = await CreateUserAsync(RoleNames.Admin, isRole: true);

        var matches = await ReadAsync<GuardianMatchesResponse>(await GetAsync(admin, $"/api/admissions/{admission}/guardian-matches"));

        var match = Assert.Single(matches.Guardians);
        Assert.Equal(GuardianActions.UseExisting, match.SuggestedAction);
        Assert.Equal(parentId, match.LinkedParentId);
        Assert.Equal(parentId, match.Candidates[0].ParentId);
    }

    [Fact]
    public async Task Matches_say_ParentRemoved_when_the_linked_parent_was_deleted()
    {
        var (parentId, _, _) = await CreateParentWithChildAsync();
        var (admission, _, _) = await CreateAdmittedWithLinkAsync(parentId);
        await SoftDeleteParentAsync(parentId);
        var admin = await CreateUserAsync(RoleNames.Admin, isRole: true);

        var matches = await ReadAsync<GuardianMatchesResponse>(await GetAsync(admin, $"/api/admissions/{admission}/guardian-matches"));

        var match = Assert.Single(matches.Guardians);
        Assert.Equal(GuardianActions.ParentRemoved, match.SuggestedAction);
        Assert.DoesNotContain(match.Candidates, c => c.ParentId == parentId);
    }

    // ─────────────────────────────────────────────────────────────────
    // Enrolment
    // ─────────────────────────────────────────────────────────────────

    [Fact]
    public async Task Enrolling_with_the_linked_parent_reuses_it_creates_no_duplicate_and_writes_no_override_audit()
    {
        var (parentId, _, _) = await CreateParentWithChildAsync();
        var (admissionId, guardianId, section) = await CreateAdmittedWithLinkAsync(parentId);
        var admin = await CreateUserAsync(RoleNames.Admin, isRole: true);
        var parentsBefore = await CountParentsAsync();

        var response = await PostAsync(admin, $"/api/admissions/{admissionId}/enroll",
            EnrolBody(section.Id, new GuardianDecision(guardianId, GuardianActions.UseExisting, parentId)));

        Assert.Equal(HttpStatusCode.OK, response.StatusCode);
        Assert.Equal(parentsBefore, await CountParentsAsync());
        Assert.True(await StudentIsLinkedToAsync(admissionId, parentId));
        Assert.Equal(0, await CountGuardianParentAuditRowsAsync(guardianId));
    }

    [Fact]
    public async Task Overriding_the_linked_parent_at_enrolment_is_audited_with_old_new_and_the_user()
    {
        var (linkedId, _, _) = await CreateParentWithChildAsync();
        var (otherId, _, _) = await CreateParentWithChildAsync();
        var (admissionId, guardianId, section) = await CreateAdmittedWithLinkAsync(linkedId);
        var admin = await CreateUserAsync(RoleNames.Admin, isRole: true);

        var response = await PostAsync(admin, $"/api/admissions/{admissionId}/enroll",
            EnrolBody(section.Id, new GuardianDecision(guardianId, GuardianActions.UseExisting, otherId)));

        Assert.Equal(HttpStatusCode.OK, response.StatusCode);
        Assert.True(await StudentIsLinkedToAsync(admissionId, otherId));
        Assert.False(await StudentIsLinkedToAsync(admissionId, linkedId));

        using var scope = _factory.Services.CreateScope();
        var db = scope.ServiceProvider.GetRequiredService<ApplicationDbContext>();
        var log = await db.AuditLogs.SingleAsync(l =>
            l.TableName == "AdmissionGuardians" && l.RecordId == guardianId.ToString() &&
            l.Action == AuditActions.Update && l.Changes!.Contains("parentId"));
        Assert.Contains($"\"old\":{linkedId}", log.Changes!);
        Assert.Contains($"\"new\":{otherId}", log.Changes!);
        Assert.Equal(admin.Username, log.UserName);
        Assert.Equal(admin.UserId, log.UserId);
    }

    [Fact]
    public async Task Choosing_to_create_a_new_parent_instead_of_the_linked_one_is_audited_too()
    {
        var (linkedId, _, _) = await CreateParentWithChildAsync();
        var (admissionId, guardianId, section) = await CreateAdmittedWithLinkAsync(linkedId);
        var admin = await CreateUserAsync(RoleNames.Admin, isRole: true);

        var response = await PostAsync(admin, $"/api/admissions/{admissionId}/enroll",
            EnrolBody(section.Id, new GuardianDecision(guardianId, GuardianActions.CreateNew, null)));

        Assert.Equal(HttpStatusCode.OK, response.StatusCode);
        Assert.False(await StudentIsLinkedToAsync(admissionId, linkedId));
        Assert.Equal(1, await CountGuardianParentAuditRowsAsync(guardianId));
    }

    [Fact]
    public async Task Two_guardians_swapping_their_registered_parents_at_enrolment_succeeds()
    {
        var (firstId, _, _) = await CreateParentWithChildAsync();
        var (secondId, _, _) = await CreateParentWithChildAsync();
        int admissionId, fatherId, motherId;
        ClassSection section;
        using (var scope = _factory.Services.CreateScope())
        {
            var db = scope.ServiceProvider.GetRequiredService<ApplicationDbContext>();
            section = await TestData.CreateSectionAsync(db, capacity: 5);
            var (admission, father) = await TestData.CreateAdmittedAdmissionAsync(db, section);
            var mother = new AdmissionGuardian { AdmissionId = admission.Id, RelationType = "Mother", Name = "M", Mobile = "9" + Random.Shared.NextInt64(100_000_000, 999_999_999) };
            father.ParentId = firstId;
            mother.ParentId = secondId;
            db.AdmissionGuardians.Add(mother);
            await db.SaveChangesAsync();
            (admissionId, fatherId, motherId) = (admission.Id, father.Id, mother.Id);
        }
        var admin = await CreateUserAsync(RoleNames.Admin, isRole: true);

        // Staff swap them: the father becomes the second parent and the mother the first.
        var response = await PostAsync(admin, $"/api/admissions/{admissionId}/enroll", EnrolBody(section.Id,
            new GuardianDecision(fatherId, GuardianActions.UseExisting, secondId),
            new GuardianDecision(motherId, GuardianActions.UseExisting, firstId)));

        Assert.True(response.StatusCode == HttpStatusCode.OK, await response.Content.ReadAsStringAsync());
        using var verify = _factory.Services.CreateScope();
        var verifyDb = verify.ServiceProvider.GetRequiredService<ApplicationDbContext>();
        Assert.Equal(secondId, (await verifyDb.AdmissionGuardians.SingleAsync(g => g.Id == fatherId)).ParentId);
        Assert.Equal(firstId, (await verifyDb.AdmissionGuardians.SingleAsync(g => g.Id == motherId)).ParentId);
    }

    [Fact]
    public async Task A_deleted_linked_parent_stops_the_enrolment_with_a_clear_message_and_creates_nothing()
    {
        var (parentId, _, _) = await CreateParentWithChildAsync();
        var (admissionId, guardianId, section) = await CreateAdmittedWithLinkAsync(parentId);
        await SoftDeleteParentAsync(parentId);
        var admin = await CreateUserAsync(RoleNames.Admin, isRole: true);
        var parentsBefore = await CountParentsAsync();

        // Whatever staff decided - even "create a new parent" - the enrolment is refused rather than quietly duplicating.
        foreach (var decision in new[]
        {
            new GuardianDecision(guardianId, GuardianActions.CreateNew, null),
            new GuardianDecision(guardianId, GuardianActions.UseExisting, parentId),
        })
        {
            var response = await PostAsync(admin, $"/api/admissions/{admissionId}/enroll", EnrolBody(section.Id, decision));

            Assert.Equal(HttpStatusCode.UnprocessableEntity, response.StatusCode);
            var message = await response.Content.ReadAsStringAsync();
            Assert.Contains("has been deleted", message);
            Assert.Contains("Use a different person", message);
        }

        Assert.Equal(parentsBefore, await CountParentsAsync());
        using var scope = _factory.Services.CreateScope();
        var db = scope.ServiceProvider.GetRequiredService<ApplicationDbContext>();
        Assert.False(await db.Students.AnyAsync(s => s.AdmissionId == admissionId));
        Assert.Equal("Admitted", (await db.Admissions.SingleAsync(a => a.Id == admissionId)).Status);
    }

    // ─────────────────────────────────────────────────────────────────
    // Helpers
    // ─────────────────────────────────────────────────────────────────

    private sealed record Session(int UserId, string Username, string Token);

    private static AdmissionGuardianRequest Linked(int parentId) =>
        new("Father", "ignored", null, null, true, parentId);

    private static CreateAdmissionRequest NewApplication(int sectionId, params AdmissionGuardianRequest[] guardians) =>
        new("Test", null, "Applicant", "Female", new DateOnly(2018, 5, 5), sectionId, "New", null,
            "9000000000", null, null, null, guardians.ToList());

    private static UpdateAdmissionRequest UpdateOf(
        AdmissionResponse current, string remarks, params AdmissionGuardianRequest[] guardians) =>
        new(current.FirstName, current.MiddleName, current.LastName, current.Gender, current.DateOfBirth,
            current.AppliedForClassSectionId, current.AdmissionType, current.PreviousSchool, current.Phone, current.Email,
            null, remarks, guardians.ToList(), current.RowVersion);

    private static EnrollAdmissionRequest EnrolBody(int sectionId, params GuardianDecision[] decisions) =>
        new("R" + TestData.Unique()[..6], "A" + TestData.Unique(), new DateOnly(2026, 6, 1), null, false, sectionId,
            null, null, null, null, null, null, decisions.ToList());

    private async Task<ClassSection> CreateSectionAsync()
    {
        using var scope = _factory.Services.CreateScope();
        var db = scope.ServiceProvider.GetRequiredService<ApplicationDbContext>();
        return await TestData.CreateSectionAsync(db, capacity: 10);
    }

    /// <summary>A parent with one child. Returns the parent's id, its mobile number and the child's full name.</summary>
    private async Task<(int ParentId, string Mobile, string ChildName)> CreateParentWithChildAsync()
    {
        using var scope = _factory.Services.CreateScope();
        var db = scope.ServiceProvider.GetRequiredService<ApplicationDbContext>();
        var section = await TestData.CreateSectionAsync(db, capacity: 10);
        var student = TestData.NewStudent(section, "1");
        var parent = new Parent
        {
            Name = "Parent " + TestData.Unique(),
            Mobile = "9" + Random.Shared.NextInt64(100_000_000, 999_999_999),
            Email = $"p{TestData.Unique()}@example.com",
        };
        db.Students.Add(student);
        db.Parents.Add(parent);
        await db.SaveChangesAsync();
        db.StudentGuardians.Add(new StudentGuardian
        {
            StudentId = student.Id, ParentId = parent.Id, RelationType = "Father", IsPrimaryContact = true,
        });
        await db.SaveChangesAsync();
        return (parent.Id, parent.Mobile, $"{student.FirstName} {student.LastName}");
    }

    /// <summary>An Admitted application whose only guardian is linked to <paramref name="parentId"/>.</summary>
    private async Task<(int AdmissionId, int GuardianId, ClassSection Section)> CreateAdmittedWithLinkAsync(
        int parentId, string? guardianMobile = null)
    {
        using var scope = _factory.Services.CreateScope();
        var db = scope.ServiceProvider.GetRequiredService<ApplicationDbContext>();
        var section = await TestData.CreateSectionAsync(db, capacity: 10);
        var (admission, guardian) = await TestData.CreateAdmittedAdmissionAsync(db, section);
        guardian.ParentId = parentId;
        if (guardianMobile is not null) guardian.Mobile = guardianMobile;
        await db.SaveChangesAsync();

        // Linking above is itself audited (ParentId null -> parentId). The tests below count audit rows written by
        // the enrolment, so drop the set-up row to start from a clean baseline.
        var recordId = guardian.Id.ToString();
        await db.AuditLogs.Where(l => l.TableName == "AdmissionGuardians" && l.RecordId == recordId)
            .ExecuteDeleteAsync();
        return (admission.Id, guardian.Id, section);
    }

    private async Task SoftDeleteParentAsync(int parentId)
    {
        using var scope = _factory.Services.CreateScope();
        var db = scope.ServiceProvider.GetRequiredService<ApplicationDbContext>();
        var parent = await db.Parents.SingleAsync(p => p.Id == parentId);
        parent.IsDeleted = true;
        parent.DeletedAt = DateTime.UtcNow;
        await db.SaveChangesAsync();
    }

    private async Task<string> ParentNameAsync(int parentId)
    {
        using var scope = _factory.Services.CreateScope();
        var db = scope.ServiceProvider.GetRequiredService<ApplicationDbContext>();
        return await db.Parents.Where(p => p.Id == parentId).Select(p => p.Name).SingleAsync();
    }

    private async Task<int> CountParentsAsync()
    {
        using var scope = _factory.Services.CreateScope();
        var db = scope.ServiceProvider.GetRequiredService<ApplicationDbContext>();
        return await db.Parents.IgnoreQueryFilters().CountAsync();
    }

    private async Task<int> CountAdmissionsAsync(int sectionId)
    {
        using var scope = _factory.Services.CreateScope();
        var db = scope.ServiceProvider.GetRequiredService<ApplicationDbContext>();
        return await db.Admissions.IgnoreQueryFilters().CountAsync(a => a.AppliedForClassSectionId == sectionId);
    }

    private async Task<bool> StudentIsLinkedToAsync(int admissionId, int parentId)
    {
        using var scope = _factory.Services.CreateScope();
        var db = scope.ServiceProvider.GetRequiredService<ApplicationDbContext>();
        return await db.StudentGuardians.AnyAsync(sg => sg.Student.AdmissionId == admissionId && sg.ParentId == parentId);
    }

    private async Task<int> CountGuardianParentAuditRowsAsync(int guardianId)
    {
        using var scope = _factory.Services.CreateScope();
        var db = scope.ServiceProvider.GetRequiredService<ApplicationDbContext>();
        return await db.AuditLogs.CountAsync(l =>
            l.TableName == "AdmissionGuardians" && l.RecordId == guardianId.ToString() &&
            l.Action == AuditActions.Update && l.Changes!.Contains("parentId"));
    }

    /// <summary>A user holding exactly the given permissions (a fresh role), or the named built-in role when isRole is true.</summary>
    private async Task<Session> CreateUserAsync(string first, params string[] more) => await CreateUserAsync(first, false, more);

    private async Task<Session> CreateUserAsync(string first, bool isRole, params string[] more)
    {
        var roleName = isRole ? first : await CreateRoleAsync(more.Prepend(first).ToArray());
        var username = "gp_" + TestData.Unique();
        int userId;
        using (var scope = _factory.Services.CreateScope())
        {
            var db = scope.ServiceProvider.GetRequiredService<ApplicationDbContext>();
            var hasher = scope.ServiceProvider.GetRequiredService<IPasswordHasher>();
            var role = await db.Roles.SingleAsync(r => r.Name == roleName);
            var user = new User
            {
                Username = username,
                Email = $"{username}@test.local",
                PasswordHash = hasher.HashPassword(Password),
                Status = "Active",
                SecurityStamp = Guid.NewGuid(),
            };
            user.UserRoles.Add(new UserRole { RoleId = role.Id, AssignedAt = DateTime.UtcNow });
            db.Users.Add(user);
            await db.SaveChangesAsync();
            userId = user.Id;
        }

        var login = await _client.PostAsJsonAsync("/api/auth/login", new LoginRequest(username, Password));
        login.EnsureSuccessStatusCode();
        return new Session(userId, username, (await login.Content.ReadFromJsonAsync<AuthResult>())!.Token);
    }

    private async Task<string> CreateRoleAsync(string[] permissionNames)
    {
        using var scope = _factory.Services.CreateScope();
        var db = scope.ServiceProvider.GetRequiredService<ApplicationDbContext>();
        var role = new Role { Name = "gp_" + TestData.Unique(), IsActive = true };
        foreach (var permission in await db.Permissions.Where(p => permissionNames.Contains(p.Name)).ToListAsync())
            role.RolePermissions.Add(new RolePermission { Permission = permission });
        db.Roles.Add(role);
        await db.SaveChangesAsync();
        return role.Name;
    }

    private async Task<HttpResponseMessage> SendAsync(Session session, HttpMethod method, string url, object? body = null)
    {
        using var request = new HttpRequestMessage(method, url);
        request.Headers.Authorization = new AuthenticationHeaderValue("Bearer", session.Token);
        if (body is not null) request.Content = JsonContent.Create(body);
        return await _client.SendAsync(request);
    }

    private Task<HttpResponseMessage> GetAsync(Session s, string url) => SendAsync(s, HttpMethod.Get, url);
    private Task<HttpResponseMessage> PostAsync(Session s, string url, object body) => SendAsync(s, HttpMethod.Post, url, body);
    private Task<HttpResponseMessage> PutAsync(Session s, string url, object body) => SendAsync(s, HttpMethod.Put, url, body);

    private static async Task<T> ReadAsync<T>(HttpResponseMessage response) =>
        (await response.Content.ReadFromJsonAsync<T>(Json))!;
}
