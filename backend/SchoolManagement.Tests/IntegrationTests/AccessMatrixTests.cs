// File: backend/SchoolManagement.Tests/IntegrationTests/AccessMatrixTests.cs
//
// Role × endpoint → allowed or 403, generated from two hand-written tables:
//   RoleGrants  — which permissions each role holds (the seeded data);
//   Endpoints   — what each endpoint requires.
// Every combination becomes its own test case, so a failure names the exact role and endpoint.
//
// "Allowed" means the request got PAST authorization: any status except 401/403. The requests
// use ids that don't exist (999999) and empty bodies, so allowed requests stop at 404/400/422
// and never change data.
//
// When permissions change (Batch E1), update these two tables in the same commit. A table that
// disagrees with the code is a failing test, which is the point.

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
    // Table 1: what each role holds today (pre-E1). Admin holds the whole catalog in code.
    // ─────────────────────────────────────────────────────────────────
    private static readonly IReadOnlyDictionary<string, IReadOnlySet<string>> RoleGrants =
        new Dictionary<string, IReadOnlySet<string>>
        {
            [RoleNames.Admin] = Permissions.All,
            [RoleNames.Supervisor] = new HashSet<string> { Permissions.AttendanceManage, Permissions.StudentsViewSensitive },
            [RoleNames.Clerk] = new HashSet<string> { Permissions.AttendanceManage },
            [RoleNames.Teacher] = new HashSet<string> { Permissions.AttendanceMark },
            [RoleNames.Student] = new HashSet<string>(),
            [RoleNames.Parent] = new HashSet<string>(),
        };

    // ─────────────────────────────────────────────────────────────────
    // Table 2: every endpoint and its requirement.
    // Left out on purpose: login/refresh/logout (anonymous) and logout-all (it would end the
    // cached test session).
    // ─────────────────────────────────────────────────────────────────
    private static readonly Requirement SignedIn = Requirement.SignedIn;
    private static Requirement P(string permission) => Requirement.Permission(permission);

    private static readonly Endpoint[] Endpoints =
    {
        // Auth
        new("GET",    "/api/auth/me", SignedIn),
        new("POST",   "/api/auth/change-password", SignedIn, HasBody: true),
        new("POST",   $"/api/auth/unlock/{Missing}", P(Permissions.UsersCreate)),

        // Users
        new("POST",   "/api/users", P(Permissions.UsersCreate), HasBody: true),

        // Class sections
        new("GET",    "/api/class-sections", SignedIn),
        new("GET",    $"/api/class-sections/{Missing}", SignedIn),
        new("POST",   "/api/class-sections", P(Permissions.ClassSectionsManage), HasBody: true),
        new("PUT",    $"/api/class-sections/{Missing}", P(Permissions.ClassSectionsManage), HasBody: true),
        new("PATCH",  $"/api/class-sections/{Missing}/status", P(Permissions.ClassSectionsManage), HasBody: true),
        new("DELETE", $"/api/class-sections/{Missing}", P(Permissions.ClassSectionsManage)),

        // Subjects
        new("GET",    "/api/subjects", SignedIn),
        new("GET",    $"/api/subjects/{Missing}", SignedIn),
        new("POST",   "/api/subjects", P(Permissions.SubjectsManage), HasBody: true),
        new("PUT",    $"/api/subjects/{Missing}", P(Permissions.SubjectsManage), HasBody: true),
        new("DELETE", $"/api/subjects/{Missing}", P(Permissions.SubjectsManage)),

        // Teachers
        new("GET",    "/api/teachers", SignedIn),
        new("GET",    $"/api/teachers/{Missing}", SignedIn),
        new("GET",    $"/api/teachers/{Missing}/assignments", SignedIn),
        new("POST",   "/api/teachers", P(Permissions.TeachersManage), HasBody: true),
        new("PUT",    $"/api/teachers/{Missing}", P(Permissions.TeachersManage), HasBody: true),
        new("PATCH",  $"/api/teachers/{Missing}/status", P(Permissions.TeachersManage), HasBody: true),
        new("DELETE", $"/api/teachers/{Missing}", P(Permissions.TeachersManage)),
        new("POST",   $"/api/teachers/{Missing}/subjects", P(Permissions.TeachersManage), HasBody: true),
        new("DELETE", $"/api/teachers/{Missing}/subjects/{Missing}", P(Permissions.TeachersManage)),
        new("PUT",    $"/api/teachers/{Missing}/class-teacher", P(Permissions.TeachersManage), HasBody: true),
        new("DELETE", $"/api/teachers/{Missing}/class-teacher/{Missing}", P(Permissions.TeachersManage)),

        // Admissions
        new("GET",    "/api/admissions", SignedIn),
        new("GET",    $"/api/admissions/{Missing}", SignedIn),
        new("POST",   "/api/admissions", P(Permissions.AdmissionsManage), HasBody: true),
        new("PUT",    $"/api/admissions/{Missing}", P(Permissions.AdmissionsManage), HasBody: true),
        new("POST",   $"/api/admissions/{Missing}/confirm-admission", P(Permissions.AdmissionsManage), HasBody: true),
        new("GET",    $"/api/admissions/{Missing}/guardian-matches", P(Permissions.AdmissionsManage)),
        new("POST",   $"/api/admissions/{Missing}/enroll", P(Permissions.AdmissionsManage), HasBody: true),
        new("POST",   $"/api/admissions/{Missing}/reject", P(Permissions.AdmissionsManage), HasBody: true),
        new("DELETE", $"/api/admissions/{Missing}", P(Permissions.AdmissionsManage)),

        // Students
        new("GET",    "/api/students", SignedIn),
        new("GET",    $"/api/students/{Missing}", SignedIn),
        new("GET",    $"/api/students/{Missing}/enrollments", SignedIn),
        new("GET",    $"/api/students/{Missing}/guardians", SignedIn),
        new("POST",   "/api/students", P(Permissions.StudentsManage), HasBody: true),
        new("PUT",    $"/api/students/{Missing}", P(Permissions.StudentsManage), HasBody: true),
        new("PUT",    $"/api/students/{Missing}/identity",
                      Requirement.AllOf(Permissions.StudentsManage, Permissions.StudentsViewSensitive), HasBody: true),
        new("POST",   $"/api/students/{Missing}/guardians", P(Permissions.ParentsManage), HasBody: true),
        new("DELETE", $"/api/students/{Missing}/guardians/{Missing}", P(Permissions.ParentsManage)),
        new("DELETE", $"/api/students/{Missing}", P(Permissions.StudentsManage)),

        // Parents
        new("GET",    "/api/parents", SignedIn),
        new("GET",    $"/api/parents/{Missing}", SignedIn),
        new("GET",    $"/api/parents/{Missing}/students", SignedIn),
        new("POST",   "/api/parents", P(Permissions.ParentsManage), HasBody: true),
        new("PUT",    $"/api/parents/{Missing}", P(Permissions.ParentsManage), HasBody: true),
        new("DELETE", $"/api/parents/{Missing}", P(Permissions.ParentsManage)),
        new("POST",   $"/api/parents/{Missing}/students", P(Permissions.ParentsManage), HasBody: true),
        new("DELETE", $"/api/parents/{Missing}/students/{Missing}", P(Permissions.ParentsManage)),
        new("POST",   $"/api/parents/{Missing}/students/{Missing}/primary", P(Permissions.ParentsManage)),

        // Attendance
        new("GET",    $"/api/attendance/roster?classSectionId={Missing}&date=2026-01-05", SignedIn),
        new("PUT",    "/api/attendance",
                      Requirement.AnyOf(Permissions.AttendanceManage, Permissions.AttendanceMark), HasBody: true),
        new("GET",    $"/api/attendance/students/{Missing}", SignedIn),
        new("GET",    $"/api/attendance/summary?classSectionId={Missing}&from=2026-01-05&to=2026-01-06", SignedIn),
    };

    public static IEnumerable<object[]> Cases() =>
        from role in RoleGrants.Keys
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
        var expectAllowed = endpoint.Requires.IsMet(RoleGrants[role]);

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
