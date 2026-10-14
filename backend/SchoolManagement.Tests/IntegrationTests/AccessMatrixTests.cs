// File: backend/SchoolManagement.Tests/IntegrationTests/AccessMatrixTests.cs
//
// Role × endpoint → allowed or 403, generated from two hand-written tables:
//   ExpectedRoleMatrix — which permissions each role holds (the agreed E1 matrix);
//   Endpoints          — what each endpoint requires.
// Every combination becomes its own test case, so a failure names the exact role and endpoint.
//
// "Allowed" means the request got PAST authorization: any status except 401/403. The requests
// use ids that don't exist (999999) and empty bodies, so allowed requests stop at 404/400/422
// and never change data.
//
// When permissions change, update these tables in the same commit. A table that disagrees with
// the code is a failing test, which is the point.

using System.Net;
using SchoolManagement.Domain.Constants;
using Xunit;

namespace SchoolManagement.Tests.IntegrationTests;

[Collection("LegacyApi")]
public class AccessMatrixTests
{
    private const string Missing = "999999";

    /// <summary>What an endpoint demands of the caller.</summary>
    public sealed record Requirement(string Description, Func<IReadOnlySet<string>, bool> IsMet)
    {
        public static readonly Requirement SignedIn = new("signed in", _ => true);
        public static Requirement Permission(string p) => new(p, held => held.Contains(p));
        public static Requirement AllOf(params string[] ps) => new(string.Join(" + ", ps), held => ps.All(held.Contains));
        public static Requirement AnyOf(params string[] ps) => new(string.Join(" | ", ps), held => ps.Any(held.Contains));
        public override string ToString() => Description;
    }

    public sealed record Endpoint(string Method, string Url, Requirement Requires, bool HasBody = false);

    // ─────────────────────────────────────────────────────────────────
    // Table 1: what each role holds — the agreed E1 matrix (see ExpectedRoleMatrix).
    // ─────────────────────────────────────────────────────────────────
    private static readonly string[] Roles = RoleNames.All.ToArray();

    // ─────────────────────────────────────────────────────────────────
    // Table 2: every endpoint and its requirement.
    // Left out on purpose: login/refresh/logout (anonymous) and logout-all (it would end the
    // cached test session).
    // ─────────────────────────────────────────────────────────────────
    private static readonly Requirement SignedIn = Requirement.SignedIn;
    private static Requirement P(string permission) => Requirement.Permission(permission);

    // F1: the "X.Read" policies (full View OR ViewScoped).
    private static readonly Requirement StudentsRead = Requirement.AnyOf(Permissions.StudentsView, Permissions.StudentsViewScoped);
    private static readonly Requirement ParentsRead = Requirement.AnyOf(Permissions.ParentsView, Permissions.ParentsViewScoped);
    private static readonly Requirement ParentSearch = new(
        "Parents.View + (Admissions.Create | Admissions.Edit)",
        held => held.Contains(Permissions.ParentsView) &&
                (held.Contains(Permissions.AdmissionsCreate) || held.Contains(Permissions.AdmissionsEdit)));
    private static readonly Requirement AttendanceRead = Requirement.AnyOf(Permissions.AttendanceView, Permissions.AttendanceViewScoped);

    private static readonly Endpoint[] Endpoints =
    {
        // Auth
        new("GET",    "/api/auth/me", SignedIn),

        // Countries for address forms (G1a): reference data, any signed-in user
        new("GET",    "/api/countries", SignedIn),
        new("POST",   "/api/auth/change-password", SignedIn, HasBody: true),

        // Users (E2)
        new("GET",    "/api/users", P(Permissions.UsersView)),
        new("GET",    $"/api/users/{Missing}", P(Permissions.UsersView)),
        new("POST",   "/api/users", P(Permissions.UsersCreate), HasBody: true),
        new("PUT",    $"/api/users/{Missing}", P(Permissions.UsersEdit), HasBody: true),
        new("PATCH",  $"/api/users/{Missing}/status", P(Permissions.UsersChangeStatus), HasBody: true),
        new("POST",   $"/api/users/{Missing}/reset-password", P(Permissions.UsersResetPassword), HasBody: true),
        new("POST",   $"/api/users/{Missing}/unlock", P(Permissions.UsersChangeStatus)),
        new("DELETE", $"/api/users/{Missing}", P(Permissions.UsersDelete)),

        // My Account (E2): your own profile, any signed-in user
        new("GET",    "/api/me/profile", SignedIn),
        new("PUT",    "/api/me/profile", SignedIn, HasBody: true),

        // School settings (E2)
        new("GET",    "/api/settings/school", P(Permissions.SettingsView)),
        new("PUT",    "/api/settings/school", P(Permissions.SettingsEdit), HasBody: true),
        new("GET",    "/api/settings/time-zones", P(Permissions.SettingsView)),
        new("GET",    "/api/settings/basics", SignedIn),

        // Roles + permission catalog (E1)
        new("GET",    "/api/permissions", P(Permissions.RolesView)),
        new("GET",    "/api/roles", P(Permissions.RolesView)),
        new("GET",    $"/api/roles/{Missing}", P(Permissions.RolesView)),
        new("POST",   "/api/roles", P(Permissions.RolesCreate), HasBody: true),
        new("PUT",    $"/api/roles/{Missing}", P(Permissions.RolesEdit), HasBody: true),
        new("PUT",    $"/api/roles/{Missing}/permissions", P(Permissions.RolesEdit), HasBody: true),
        new("PATCH",  $"/api/roles/{Missing}/status", P(Permissions.RolesEdit), HasBody: true),

        // Class sections
        new("GET",    "/api/class-sections", P(Permissions.ClassSectionsView)),
        new("GET",    $"/api/class-sections/{Missing}", P(Permissions.ClassSectionsView)),
        new("POST",   "/api/class-sections", P(Permissions.ClassSectionsCreate), HasBody: true),
        new("PUT",    $"/api/class-sections/{Missing}", P(Permissions.ClassSectionsEdit), HasBody: true),
        new("PATCH",  $"/api/class-sections/{Missing}/status", P(Permissions.ClassSectionsEdit), HasBody: true),
        new("DELETE", $"/api/class-sections/{Missing}", P(Permissions.ClassSectionsDelete)),

        // Subjects
        new("GET",    "/api/subjects", P(Permissions.SubjectsView)),
        new("GET",    $"/api/subjects/{Missing}", P(Permissions.SubjectsView)),
        new("POST",   "/api/subjects", P(Permissions.SubjectsCreate), HasBody: true),
        new("PUT",    $"/api/subjects/{Missing}", P(Permissions.SubjectsEdit), HasBody: true),
        new("DELETE", $"/api/subjects/{Missing}", P(Permissions.SubjectsDelete)),

        // Teachers
        new("GET",    "/api/teachers", P(Permissions.TeachersView)),
        new("GET",    $"/api/teachers/{Missing}", P(Permissions.TeachersView)),
        new("GET",    $"/api/teachers/{Missing}/assignments", P(Permissions.TeachersView)),
        new("PUT",    $"/api/teachers/{Missing}", P(Permissions.TeachersEdit), HasBody: true),
        new("DELETE", $"/api/teachers/{Missing}", P(Permissions.TeachersDelete)),
        new("POST",   $"/api/teachers/{Missing}/subjects", P(Permissions.TeachersAssign), HasBody: true),
        new("DELETE", $"/api/teachers/{Missing}/subjects/{Missing}", P(Permissions.TeachersAssign)),
        new("PUT",    $"/api/teachers/{Missing}/class-teacher", P(Permissions.TeachersAssign), HasBody: true),
        new("DELETE", $"/api/teachers/{Missing}/class-teacher/{Missing}", P(Permissions.TeachersAssign)),

        // Admissions
        new("GET",    "/api/admissions", P(Permissions.AdmissionsView)),
        new("GET",    $"/api/admissions/{Missing}", P(Permissions.AdmissionsView)),
        new("POST",   "/api/admissions", P(Permissions.AdmissionsCreate), HasBody: true),
        new("PUT",    $"/api/admissions/{Missing}", P(Permissions.AdmissionsEdit), HasBody: true),
        new("POST",   $"/api/admissions/{Missing}/confirm-admission", P(Permissions.AdmissionsEdit), HasBody: true),
        new("GET",    $"/api/admissions/{Missing}/guardian-matches", P(Permissions.AdmissionsEdit)),
        // G2: Parents.View AND the right to register or edit applications.
        new("GET",    "/api/admissions/parent-search?q=zz", ParentSearch),
        new("POST",   $"/api/admissions/{Missing}/enroll", P(Permissions.AdmissionsEnroll), HasBody: true),
        new("POST",   $"/api/admissions/{Missing}/reject", P(Permissions.AdmissionsEdit), HasBody: true),
        new("DELETE", $"/api/admissions/{Missing}", P(Permissions.AdmissionsDelete)),

        // Students
        new("GET",    "/api/students", StudentsRead),
        new("GET",    $"/api/students/{Missing}", StudentsRead),
        new("GET",    $"/api/students/{Missing}/enrollments", StudentsRead),
        new("GET",    $"/api/students/{Missing}/guardians", StudentsRead),
        new("POST",   "/api/students", P(Permissions.StudentsCreate), HasBody: true),
        new("PUT",    $"/api/students/{Missing}", P(Permissions.StudentsEdit), HasBody: true),
        new("PUT",    $"/api/students/{Missing}/identity",
                      Requirement.AllOf(Permissions.StudentsEdit, Permissions.StudentsViewSensitive), HasBody: true),
        new("POST",   $"/api/students/{Missing}/guardians", P(Permissions.ParentsEdit), HasBody: true),
        new("DELETE", $"/api/students/{Missing}/guardians/{Missing}", P(Permissions.ParentsEdit)),
        new("DELETE", $"/api/students/{Missing}", P(Permissions.StudentsDelete)),

        // Parents
        new("GET",    "/api/parents", ParentsRead),
        new("GET",    $"/api/parents/{Missing}", ParentsRead),
        new("GET",    $"/api/parents/{Missing}/students", ParentsRead),
        new("POST",   "/api/parents", P(Permissions.ParentsCreate), HasBody: true),
        new("PUT",    $"/api/parents/{Missing}", P(Permissions.ParentsEdit), HasBody: true),
        new("DELETE", $"/api/parents/{Missing}", P(Permissions.ParentsDelete)),
        new("POST",   $"/api/parents/{Missing}/students", P(Permissions.ParentsEdit), HasBody: true),
        new("DELETE", $"/api/parents/{Missing}/students/{Missing}", P(Permissions.ParentsEdit)),
        new("POST",   $"/api/parents/{Missing}/students/{Missing}/primary", P(Permissions.ParentsEdit)),

        // Attendance
        new("GET",    $"/api/attendance/roster?classSectionId={Missing}&date=2026-01-05", AttendanceRead),
        new("PUT",    "/api/attendance",
                      Requirement.AnyOf(Permissions.AttendanceManage, Permissions.AttendanceMark), HasBody: true),
        new("GET",    $"/api/attendance/students/{Missing}", AttendanceRead),
        new("GET",    $"/api/attendance/summary?classSectionId={Missing}&from=2026-01-05&to=2026-01-06", AttendanceRead),

        // Documents (G1b). Uploads get a JSON body here, so an allowed caller stops at 400 ("Choose a file").
        // A refused caller must get 403, never 415: authorization comes before any content-type check.
        new("GET",    "/api/documents/options", P(Permissions.DocumentsView)),
        new("GET",    "/api/documents", P(Permissions.DocumentsView)),
        new("GET",    $"/api/students/{Missing}/documents", P(Permissions.DocumentsView)),
        new("GET",    $"/api/admissions/{Missing}/documents", P(Permissions.DocumentsView)),
        new("GET",    $"/api/documents/{Missing}/file", P(Permissions.DocumentsView)),
        new("POST",   $"/api/students/{Missing}/documents", P(Permissions.DocumentsUpload), HasBody: true),
        new("POST",   $"/api/admissions/{Missing}/documents", P(Permissions.DocumentsUpload), HasBody: true),
        new("POST",   $"/api/documents/{Missing}/verify", P(Permissions.DocumentsVerify), HasBody: true),
        new("POST",   $"/api/documents/{Missing}/reject", P(Permissions.DocumentsVerify), HasBody: true),
        new("POST",   $"/api/documents/{Missing}/unverify", P(Permissions.DocumentsVerify), HasBody: true),
        new("DELETE", $"/api/documents/{Missing}", P(Permissions.DocumentsDelete)),
    };

    public static IEnumerable<object[]> Cases() =>
        from role in Roles
        from endpoint in Endpoints
        select new object[] { role, endpoint.Method, endpoint.Url };

    private readonly CustomWebApplicationFactory _factory;
    private readonly HttpClient _client;

    public AccessMatrixTests(CustomWebApplicationFactory factory)
    {
        _factory = factory;
        _client = factory.CreateClient();
    }

    [Theory]
    [MemberData(nameof(Cases))]
    public async Task Role_gets_exactly_the_access_the_tables_say(string role, string method, string url)
    {
        var endpoint = Endpoints.Single(e => e.Method == method && e.Url == url);
        var expectAllowed = endpoint.Requires.IsMet(ExpectedRoleMatrix.EffectivePermissions(role));

        using var request = await RoleSessions.RequestAsAsync(
            _factory, role, new HttpMethod(method), url, endpoint.HasBody ? new { } : null);
        var response = await _client.SendAsync(request);

        if (expectAllowed)
        {
            Assert.True(
                response.StatusCode is not (HttpStatusCode.Unauthorized or HttpStatusCode.Forbidden),
                $"{role} should pass authorization for {method} {url} (requires {endpoint.Requires}) " +
                $"but got {(int)response.StatusCode}: {await response.Content.ReadAsStringAsync()}");
        }
        else
        {
            Assert.True(
                response.StatusCode == HttpStatusCode.Forbidden,
                $"{role} should be refused (403) for {method} {url} (requires {endpoint.Requires}) " +
                $"but got {(int)response.StatusCode}");
        }
    }

    [Fact]
    public void Every_controller_action_is_in_the_endpoint_table()
    {
        // Guards the table itself: a new endpoint must be added here, or this fails.
        var actions = typeof(SchoolManagement.API.Controllers.AuthController).Assembly.GetTypes()
            .Where(t => typeof(Microsoft.AspNetCore.Mvc.ControllerBase).IsAssignableFrom(t) && !t.IsAbstract)
            .SelectMany(t => t.GetMethods(System.Reflection.BindingFlags.Instance |
                                          System.Reflection.BindingFlags.Public |
                                          System.Reflection.BindingFlags.DeclaredOnly)
                .Where(m => !m.IsSpecialName)
                .Select(m => $"{t.Name}.{m.Name}"))
            .ToList();

        // Actions deliberately not in the table (see the comment above Endpoints).
        var excluded = new HashSet<string>
        {
            "AuthController.Login", "AuthController.Refresh", "AuthController.Logout", "AuthController.LogoutAll",
        };

        var expectedCount = actions.Count(a => !excluded.Contains(a));
        Assert.True(Endpoints.Length == expectedCount,
            $"The endpoint table has {Endpoints.Length} rows but the controllers expose {expectedCount} actions " +
            "(excluding anonymous/session ones). Add the new endpoint to AccessMatrixTests.Endpoints.");
    }
}
