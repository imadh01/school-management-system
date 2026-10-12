// File: backend/SchoolManagement.Tests/IntegrationTests/DataScopeTests.cs
//
// Batch F1: scoped reads ("ViewScoped") against a small, fixed school:
//
//   Current year            Class teacher   Subject teacher   Students
//   Section A               T1                                sA
//   Section B               T2                                sB, sA2
//   Section C               (none)          T1 (Math)         sC
//   Past year: Section P    T1                                sP
//
//   Guardians: pA → sA (primary) and sA2 (sibling in B)
//              pB → sB (primary), pX → sB (co-guardian), pT1 → sB (T1 is ALSO sB's parent)
//   T3: a teacher with no assignments.
//   ParentUser: logs in as pB, with a test-only role holding the three ViewScoped permissions.
//   StudentUser: logs in as sA, with a test-only role holding the three ViewScoped permissions.
//   (The real Parent/Student roles get nothing until the portal exists; decision F1-C.)
//
// What T1 must see: sA (class teacher), sC (subject teacher), sB (own child) — never sA2 or sP.

using System.Collections.Concurrent;
using System.Net;
using System.Net.Http.Headers;
using System.Net.Http.Json;
using System.Reflection;
using System.Runtime.CompilerServices;
using System.Text.Json;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.DependencyInjection;
using SchoolManagement.Application.DTOs.Auth;
using SchoolManagement.Application.Interfaces;
using SchoolManagement.Application.Services;
using SchoolManagement.Domain.Constants;
using SchoolManagement.Domain.Entities;
using SchoolManagement.Infrastructure.Persistence;
using SchoolManagement.Tests.Support;
using Xunit;

namespace SchoolManagement.Tests.IntegrationTests;

[Collection("LegacyApi")]
public class DataScopeTests
{
    private readonly CustomWebApplicationFactory _factory;
    private readonly HttpClient _client;

    public DataScopeTests(CustomWebApplicationFactory factory)
    {
        _factory = factory;
        _client = factory.CreateClient();
    }

    private Task<World> GetWorldAsync() => World.GetAsync(_factory);

    // ─────────────────────────────────────────────────────────────────
    // Students
    // ─────────────────────────────────────────────────────────────────

    [Fact]
    public async Task A_teacher_lists_exactly_the_students_they_teach_this_year_plus_their_own_child()
    {
        var w = await GetWorldAsync();

        var ids = await ListIdsAsync(w.T1, "/api/students");

        Assert.Equal(new[] { w.SA, w.SB, w.SC }.OrderBy(x => x), ids.OrderBy(x => x));
    }

    [Fact]
    public async Task Another_teacher_sees_only_their_own_class()
    {
        var w = await GetWorldAsync();

        var ids = await ListIdsAsync(w.T2, "/api/students");

        Assert.Equal(new[] { w.SA2, w.SB }.OrderBy(x => x), ids.OrderBy(x => x));
    }

    [Fact]
    public async Task An_unassigned_teacher_sees_no_students_and_no_roster()
    {
        // Behavior change (F1): before, an unassigned teacher got a read-only roster of any class.
        var w = await GetWorldAsync();

        Assert.Empty(await ListIdsAsync(w.T3, "/api/students"));
        Assert.Equal(HttpStatusCode.NotFound,
            (await GetAsync(w.T3, $"/api/attendance/roster?classSectionId={w.SecA}&date={Today:yyyy-MM-dd}")).StatusCode);
    }

    [Fact]
    public async Task Last_years_class_grants_nothing()
    {
        var w = await GetWorldAsync();

        Assert.Equal(HttpStatusCode.NotFound, (await GetAsync(w.T1, $"/api/students/{w.SP}")).StatusCode);
        Assert.Equal(HttpStatusCode.NotFound,
            (await GetAsync(w.T1, $"/api/attendance/roster?classSectionId={w.SecP}&date={Today:yyyy-MM-dd}")).StatusCode);
    }

    [Fact]
    public async Task Full_View_holders_are_not_scoped()
    {
        var w = await GetWorldAsync();

        var response = await GetAsync(Role(RoleNames.Supervisor), $"/api/students/{w.SP}");

        Assert.Equal(HttpStatusCode.OK, response.StatusCode);
        using var json = await JsonAsync(response);
        Assert.True(json.RootElement.TryGetProperty("identity", out _), "full View keeps the full shape");
    }

    [Fact]
    public async Task Scoped_list_rows_carry_no_contact_category_or_admission_fields()
    {
        var w = await GetWorldAsync();

        using var json = await JsonAsync(await GetAsync(w.T1, "/api/students"));
        var row = json.RootElement.EnumerateArray().First(e => e.GetProperty("id").GetInt32() == w.SA);

        foreach (var hidden in new[] { "mobile", "category", "nationality", "admissionRegNo", "admissionId", "admissionDate", "allergies" })
            Assert.False(row.TryGetProperty(hidden, out _), $"scoped list row must not contain '{hidden}'");
        Assert.True(row.GetProperty("hasAllergies").GetBoolean(), "teachers see that there are allergies, not what they are");
    }

    [Fact]
    public async Task Scoped_detail_has_no_identity_address_or_full_health_and_guardians_are_contacts()
    {
        var w = await GetWorldAsync();

        var response = await GetAsync(w.T1, $"/api/students/{w.SA}");
        Assert.Equal(HttpStatusCode.OK, response.StatusCode);
        using var json = await JsonAsync(response);
        var root = json.RootElement;

        foreach (var hidden in new[] { "identity", "addressLine", "religion", "category", "custodyArrangement",
                                       "feeConcessionPercent", "pickupPersons", "email", "mobile", "rowVersion" })
            Assert.False(root.TryGetProperty(hidden, out _), $"scoped detail must not contain '{hidden}'");

        var health = root.GetProperty("health");
        Assert.True(health.GetProperty("hasAllergies").GetBoolean());
        Assert.False(health.TryGetProperty("allergies", out _), "allergy text needs Students.ViewSensitive");
        Assert.False(health.TryGetProperty("medicalNotes", out _));

        var guardian = Assert.Single(root.GetProperty("guardians").EnumerateArray());
        Assert.Equal(w.PA, guardian.GetProperty("parentId").GetInt32());
        Assert.False(guardian.TryGetProperty("parentEmail", out _));
    }

    [Fact]
    public async Task Out_of_scope_and_missing_ids_get_the_same_404_body()
    {
        var w = await GetWorldAsync();
        const int missing = 987654321;

        var outOfScope = await GetAsync(w.T1, $"/api/students/{w.SA2}");
        var notThere = await GetAsync(w.T1, $"/api/students/{missing}");

        Assert.Equal(HttpStatusCode.NotFound, outOfScope.StatusCode);
        Assert.Equal(HttpStatusCode.NotFound, notThere.StatusCode);
        Assert.Equal(
            Normalize(await outOfScope.Content.ReadAsStringAsync(), w.SA2),
            Normalize(await notThere.Content.ReadAsStringAsync(), missing));

        // Same body apart from the id in the message and the per-request trace id.
        static string Normalize(string body, int id)
        {
            using var doc = JsonDocument.Parse(body);
            var props = doc.RootElement.EnumerateObject()
                .Where(p => p.Name != "traceId")
                .Select(p => $"{p.Name}={p.Value.ToString().Replace(id.ToString(), "{id}")}");
            return string.Join("|", props);
        }
    }

    // ─────────────────────────────────────────────────────────────────
    // Guardians and parents
    // ─────────────────────────────────────────────────────────────────

    [Fact]
    public async Task A_teacher_who_is_also_a_parent_sees_only_their_own_link_on_their_child()
    {
        // T1 reaches sB ONLY as a parent (T2 teaches B). Co-guardians pB and pX stay private.
        var w = await GetWorldAsync();

        var ids = await ListIdsAsync(w.T1, $"/api/students/{w.SB}/guardians", "parentId");

        Assert.Equal(new[] { w.PT1 }, ids);
    }

    [Fact]
    public async Task A_teacher_sees_every_guardian_of_a_student_they_teach()
    {
        var w = await GetWorldAsync();

        var ids = await ListIdsAsync(w.T2, $"/api/students/{w.SB}/guardians", "parentId");

        Assert.Equal(new[] { w.PB, w.PX, w.PT1 }.OrderBy(x => x), ids.OrderBy(x => x));
    }

    [Fact]
    public async Task A_teacher_lists_the_parents_of_their_students_and_their_own_record_only()
    {
        var w = await GetWorldAsync();

        using var json = await JsonAsync(await GetAsync(w.T1, "/api/parents"));
        var rows = json.RootElement.EnumerateArray().ToList();

        Assert.Equal(new[] { w.PA, w.PT1 }.OrderBy(x => x), rows.Select(r => r.GetProperty("id").GetInt32()).OrderBy(x => x));
        Assert.All(rows, r => Assert.Equal(new[] { "id", "mobile", "name" },
            r.EnumerateObject().Select(p => p.Name).OrderBy(n => n, StringComparer.Ordinal)));
    }

    [Fact]
    public async Task A_parents_children_are_filtered_to_the_callers_scope()
    {
        // pA has sA (taught by T1) and sA2 (class B): T1 must not learn about the sibling.
        var w = await GetWorldAsync();

        var ids = await ListIdsAsync(w.T1, $"/api/parents/{w.PA}/students", "studentId");

        Assert.Equal(new[] { w.SA }, ids);
    }

    // ─────────────────────────────────────────────────────────────────
    // Attendance
    // ─────────────────────────────────────────────────────────────────

    [Fact]
    public async Task A_subject_teacher_sees_only_their_subject_in_a_students_history()
    {
        var w = await GetWorldAsync();

        using var json = await JsonAsync(await GetAsync(w.T1, $"/api/attendance/students/{w.SC}"));
        var records = json.RootElement.GetProperty("records").EnumerateArray().ToList();

        var only = Assert.Single(records);
        Assert.Equal("Math", only.GetProperty("subjectName").GetString());
    }

    [Fact]
    public async Task A_parent_sees_all_of_their_childs_attendance()
    {
        var w = await GetWorldAsync();

        using var json = await JsonAsync(await GetAsync(w.ParentUser, $"/api/attendance/students/{w.SB}"));
        var only = Assert.Single(json.RootElement.GetProperty("records").EnumerateArray());
        Assert.Equal(AttendanceRules.Late, only.GetProperty("status").GetString());
    }

    [Fact]
    public async Task Rosters_open_only_for_your_own_class_or_subject()
    {
        var w = await GetWorldAsync();
        var date = Today.AddDays(-1).ToString("yyyy-MM-dd");

        Assert.Equal(HttpStatusCode.OK, (await GetAsync(w.T1, $"/api/attendance/roster?classSectionId={w.SecA}&date={date}")).StatusCode);
        Assert.Equal(HttpStatusCode.OK, (await GetAsync(w.T1, $"/api/attendance/roster?classSectionId={w.SecC}&date={date}&subjectId={w.MathC}")).StatusCode);
        // Subject teacher of C, but not its class teacher: no daily roster.
        Assert.Equal(HttpStatusCode.NotFound, (await GetAsync(w.T1, $"/api/attendance/roster?classSectionId={w.SecC}&date={date}")).StatusCode);
        Assert.Equal(HttpStatusCode.NotFound, (await GetAsync(w.T1, $"/api/attendance/summary?classSectionId={w.SecC}&from={date}&to={date}")).StatusCode);
        Assert.Equal(HttpStatusCode.OK, (await GetAsync(w.T1, $"/api/attendance/summary?classSectionId={w.SecA}&from={date}&to={date}")).StatusCode);
    }

    // ─────────────────────────────────────────────────────────────────
    // Parent and student scopes (test-only roles; not granted to the real roles yet)
    // ─────────────────────────────────────────────────────────────────

    [Fact]
    public async Task A_parent_sees_only_their_child_their_own_link_and_their_own_record()
    {
        var w = await GetWorldAsync();

        Assert.Equal(new[] { w.SB }, await ListIdsAsync(w.ParentUser, "/api/students"));
        Assert.Equal(new[] { w.PB }, await ListIdsAsync(w.ParentUser, $"/api/students/{w.SB}/guardians", "parentId"));
        Assert.Equal(new[] { w.PB }, await ListIdsAsync(w.ParentUser, "/api/parents"));
        Assert.Equal(HttpStatusCode.NotFound, (await GetAsync(w.ParentUser, $"/api/parents/{w.PX}")).StatusCode);
        Assert.Equal(HttpStatusCode.NotFound,
            (await GetAsync(w.ParentUser, $"/api/attendance/roster?classSectionId={w.SecB}&date={Today:yyyy-MM-dd}")).StatusCode);
    }

    [Fact]
    public async Task A_student_sees_only_themself_and_their_parents_contacts()
    {
        var w = await GetWorldAsync();

        Assert.Equal(new[] { w.SA }, await ListIdsAsync(w.StudentUser, "/api/students"));
        Assert.Equal(new[] { w.PA }, await ListIdsAsync(w.StudentUser, $"/api/students/{w.SA}/guardians", "parentId"));

        using var json = await JsonAsync(await GetAsync(w.StudentUser, $"/api/parents/{w.PA}"));
        Assert.False(json.RootElement.TryGetProperty("employer", out _), "a student must not see a parent's employer");
        Assert.False(json.RootElement.TryGetProperty("addressLine", out _), "a student must not see a parent's address");
    }

    // ─────────────────────────────────────────────────────────────────
    // Sensitive-read audit
    // ─────────────────────────────────────────────────────────────────

    [Fact]
    public async Task Opening_a_student_leaves_a_read_audit_row_for_full_and_scoped_callers()
    {
        var w = await GetWorldAsync();
        var admin = Role(RoleNames.Admin);

        Assert.Equal(HttpStatusCode.OK, (await GetAsync(admin, $"/api/students/{w.SA}")).StatusCode);
        Assert.Equal(HttpStatusCode.OK, (await GetAsync(w.T1, $"/api/students/{w.SA}")).StatusCode);

        using var scope = _factory.Services.CreateScope();
        var db = scope.ServiceProvider.GetRequiredService<ApplicationDbContext>();
        var reads = await db.AuditLogs.AsNoTracking()
            .Where(l => l.Action == AuditActions.Read && l.TableName == "Students" && l.RecordId == w.SA.ToString())
            .ToListAsync();

        Assert.Contains(reads, r => r.UserId == w.T1.UserId && r.Changes!.Contains("Health (allergy flag)"));
        Assert.Contains(reads, r => r.UserId != w.T1.UserId && r.Changes!.Contains("Identity"));
        Assert.All(reads, r => Assert.DoesNotContain("peanuts", r.Changes!)); // what was shown, never the values
    }

    // ─────────────────────────────────────────────────────────────────
    // Id enumeration: every scoped read endpoint, with ids T1 must not reach
    // ─────────────────────────────────────────────────────────────────

    /// <summary>
    /// One row per GET action behind a "X.Read" policy. Detail endpoints must answer 404 for an
    /// out-of-scope id; list endpoints must leave the out-of-scope record out.
    /// </summary>
    private static readonly (string Action, string Url, string? ListIdProperty, Func<World, int> Hidden)[] ScopedReads =
    {
        ("StudentsController.GetAll",            "/api/students",                     "id",        w => w.SA2),
        ("StudentsController.GetById",           "/api/students/{sA2}",               null,        _ => 0),
        ("StudentsController.GetEnrollments",    "/api/students/{sA2}/enrollments",   null,        _ => 0),
        ("StudentsController.GetGuardians",      "/api/students/{sA2}/guardians",     null,        _ => 0),
        ("ParentsController.GetAll",             "/api/parents",                      "id",        w => w.PB),
        ("ParentsController.GetById",            "/api/parents/{pB}",                 null,        _ => 0),
        ("ParentsController.GetLinkedStudents",  "/api/parents/{pB}/students",        null,        _ => 0),
        ("AttendanceController.GetRoster",       "/api/attendance/roster?classSectionId={secB}&date={today}", null, _ => 0),
        ("AttendanceController.GetStudentHistory", "/api/attendance/students/{sA2}",  null,        _ => 0),
        ("AttendanceController.GetSummary",      "/api/attendance/summary?classSectionId={secB}&from={today}&to={today}", null, _ => 0),
    };

    public static IEnumerable<object[]> ScopedReadCases() => ScopedReads.Select(r => new object[] { r.Action });

    [Theory]
    [MemberData(nameof(ScopedReadCases))]
    public async Task A_teacher_cannot_reach_another_teachers_records(string action)
    {
        var w = await GetWorldAsync();
        var row = ScopedReads.Single(r => r.Action == action);
        var url = row.Url
            .Replace("{sA2}", w.SA2.ToString()).Replace("{pB}", w.PB.ToString())
            .Replace("{secB}", w.SecB.ToString()).Replace("{today}", Today.ToString("yyyy-MM-dd"));

        var response = await GetAsync(w.T1, url);

        if (row.ListIdProperty is null)
        {
            Assert.True(response.StatusCode == HttpStatusCode.NotFound,
                $"{action}: expected 404 for an out-of-scope id, got {(int)response.StatusCode}");
        }
        else
        {
            Assert.Equal(HttpStatusCode.OK, response.StatusCode);
            using var json = await JsonAsync(response);
            var ids = json.RootElement.EnumerateArray().Select(e => e.GetProperty(row.ListIdProperty).GetInt32());
            Assert.DoesNotContain(row.Hidden(w), ids);
        }
    }

    [Fact]
    public void Every_scoped_read_endpoint_is_in_the_enumeration_table()
    {
        // Guard: a new endpoint behind a Read policy (i.e. reachable with only ViewScoped) must be
        // added to ScopedReads, so it is tested for leaks. This is the check that would have caught
        // a "forgot to apply the scope" bug, the way AccessMatrixTests catches Bug 0.
        var readPolicies = new HashSet<string>
        {
            CompositePolicies.StudentsRead, CompositePolicies.ParentsRead, CompositePolicies.AttendanceRead,
        };

        var actions = typeof(SchoolManagement.API.Controllers.AuthController).Assembly.GetTypes()
            .Where(t => typeof(ControllerBase).IsAssignableFrom(t) && !t.IsAbstract)
            .SelectMany(t => t.GetMethods(BindingFlags.Instance | BindingFlags.Public | BindingFlags.DeclaredOnly)
                .Where(m => !m.IsSpecialName)
                .Where(m => m.GetCustomAttributes<AuthorizeAttribute>().Any(a => a.Policy is not null && readPolicies.Contains(a.Policy)))
                .Select(m => $"{t.Name}.{m.Name}"))
            .OrderBy(a => a)
            .ToList();

        Assert.Equal(actions, ScopedReads.Select(r => r.Action).OrderBy(a => a).ToList());

        // And no endpoint that returns students/parents/attendance to readers is left on a plain View
        // policy, where a scoped caller would be refused instead of filtered (or where someone might
        // later "fix" that by adding ViewScoped without a scope check).
        var plainViews = new HashSet<string> { Permissions.StudentsView, Permissions.ParentsView, Permissions.AttendanceView };
        var stillPlain = typeof(SchoolManagement.API.Controllers.AuthController).Assembly.GetTypes()
            .Where(t => typeof(ControllerBase).IsAssignableFrom(t) && !t.IsAbstract)
            .SelectMany(t => t.GetMethods(BindingFlags.Instance | BindingFlags.Public | BindingFlags.DeclaredOnly)
                .Where(m => m.GetCustomAttributes<AuthorizeAttribute>().Any(a => a.Policy is not null && plainViews.Contains(a.Policy)))
                .Select(m => $"{t.Name}.{m.Name}"))
            .ToList();
        Assert.Empty(stillPlain);
    }

    // ─────────────────────────────────────────────────────────────────
    // Helpers
    // ─────────────────────────────────────────────────────────────────

    private static DateOnly Today => DateOnly.FromDateTime(DateTime.UtcNow);

    private Actor Role(string roleName) => new(0, RoleSessions.GetTokenAsync(_factory, roleName).GetAwaiter().GetResult());

    private async Task<HttpResponseMessage> GetAsync(Actor actor, string url)
    {
        using var request = new HttpRequestMessage(HttpMethod.Get, url);
        request.Headers.Authorization = new AuthenticationHeaderValue("Bearer", actor.Token);
        return await _client.SendAsync(request);
    }

    private static async Task<JsonDocument> JsonAsync(HttpResponseMessage response) =>
        JsonDocument.Parse(await response.Content.ReadAsStringAsync());

    private async Task<List<int>> ListIdsAsync(Actor actor, string url, string idProperty = "id")
    {
        var response = await GetAsync(actor, url);
        Assert.True(response.StatusCode == HttpStatusCode.OK,
            $"GET {url} → {(int)response.StatusCode}: {await response.Content.ReadAsStringAsync()}");
        using var json = await JsonAsync(response);
        return json.RootElement.EnumerateArray().Select(e => e.GetProperty(idProperty).GetInt32()).ToList();
    }

    public sealed record Actor(int UserId, string Token);

    /// <summary>The school described at the top of the file, built once per test run.</summary>
    public sealed class World
    {
        private static readonly ConcurrentDictionary<int, Lazy<Task<World>>> Cache = new();
        private const string Password = "Scope@12345";

        public Actor T1 = null!, T2 = null!, T3 = null!, ParentUser = null!, StudentUser = null!;
        public int SecA, SecB, SecC, SecP, MathC;
        public int SA, SA2, SB, SC, SP;
        public int PA, PB, PX, PT1;

        public static Task<World> GetAsync(CustomWebApplicationFactory factory) =>
            Cache.GetOrAdd(RuntimeHelpers.GetHashCode(factory),
                _ => new Lazy<Task<World>>(() => BuildAsync(factory))).Value;

        private static async Task<World> BuildAsync(CustomWebApplicationFactory factory)
        {
            var w = new World();
            int t1User, t2User, t3User, parentUser, studentUser;

            using (var scope = factory.Services.CreateScope())
            {
                var db = scope.ServiceProvider.GetRequiredService<ApplicationDbContext>();
                var hasher = scope.ServiceProvider.GetRequiredService<IPasswordHasher>();

                var teacherRole = await db.Roles.SingleAsync(r => r.Name == RoleNames.Teacher);
                var parentScopeRole = await CreateScopedRoleAsync(db, "ParentPortalTest");
                var studentScopeRole = await CreateScopedRoleAsync(db, "StudentPortalTest");

                t1User = await CreateUserAsync(db, hasher, teacherRole.Id, "t1");
                t2User = await CreateUserAsync(db, hasher, teacherRole.Id, "t2");
                t3User = await CreateUserAsync(db, hasher, teacherRole.Id, "t3");
                parentUser = await CreateUserAsync(db, hasher, parentScopeRole.Id, "par");
                studentUser = await CreateUserAsync(db, hasher, studentScopeRole.Id, "stu");

                var t1 = new Teacher { UserId = t1User, Name = "Teacher One" };
                var t2 = new Teacher { UserId = t2User, Name = "Teacher Two" };
                var t3 = new Teacher { UserId = t3User, Name = "Teacher Three" };
                db.Teachers.AddRange(t1, t2, t3);
                await db.SaveChangesAsync();

                var currentYear = await db.AcademicYears.SingleAsync(y => y.IsCurrent);
                ClassSection Section(string name, int yearId, Teacher? classTeacher) => new()
                {
                    AcademicYearId = yearId, Name = "Scope " + name, Section = TestData.Unique()[..5],
                    Status = "Active", ClassTeacherId = classTeacher?.Id,
                };
                var secA = Section("A", currentYear.Id, t1);
                var secB = Section("B", currentYear.Id, t2);
                var secC = Section("C", currentYear.Id, null);
                db.ClassSections.AddRange(secA, secB, secC);
                await db.SaveChangesAsync();

                var past = await TestData.CreateSectionAsync(db); // its year is not current
                past.ClassTeacherId = t1.Id;
                await db.SaveChangesAsync();

                var math = new Subject
                {
                    Name = "Math", Code = "M" + TestData.Unique()[..5], ClassSectionId = secC.Id, TeacherId = t1.Id,
                    Type = "Theory", MaxMarks = 100, PassMarks = 35,
                };
                db.Subjects.Add(math);

                var sA = TestData.NewStudent(secA, "1");
                sA.UserId = studentUser;
                sA.Health = new StudentHealth { Allergies = "Allergic to peanuts", MedicalNotes = "Asthma inhaler" };
                var sA2 = TestData.NewStudent(secB, "1");
                var sB = TestData.NewStudent(secB, "2");
                var sC = TestData.NewStudent(secC, "1");
                var sP = TestData.NewStudent(past, "1");
                db.Students.AddRange(sA, sA2, sB, sC, sP);
                await db.SaveChangesAsync();

                Parent NewParent(string name, int? userId) => new()
                {
                    Name = name, UserId = userId, Status = "Active",
                    Mobile = "9" + Random.Shared.NextInt64(100_000_000, 999_999_999),
                    Email = $"{name.ToLowerInvariant()}@test.local", Employer = "Acme", AddressLine = "1 Secret Lane",
                };
                var pA = NewParent("PA" + TestData.Unique()[..4], null);
                var pB = NewParent("PB" + TestData.Unique()[..4], parentUser);
                var pX = NewParent("PX" + TestData.Unique()[..4], null);
                var pT1 = NewParent("PT" + TestData.Unique()[..4], t1User);
                db.Parents.AddRange(pA, pB, pX, pT1);
                await db.SaveChangesAsync();

                db.StudentGuardians.AddRange(
                    new StudentGuardian { StudentId = sA.Id, ParentId = pA.Id, RelationType = "Father", IsPrimaryContact = true },
                    new StudentGuardian { StudentId = sA2.Id, ParentId = pA.Id, RelationType = "Father", IsPrimaryContact = true },
                    new StudentGuardian { StudentId = sB.Id, ParentId = pB.Id, RelationType = "Mother", IsPrimaryContact = true },
                    new StudentGuardian { StudentId = sB.Id, ParentId = pX.Id, RelationType = "Father" },
                    new StudentGuardian { StudentId = sB.Id, ParentId = pT1.Id, RelationType = "Guardian" });

                var day = Today.AddDays(-1);
                AttendanceSession Session(ClassSection section, int? subjectId, params (Student Student, string Status)[] records)
                {
                    var now = DateTime.UtcNow;
                    var session = new AttendanceSession
                    {
                        ClassSectionId = section.Id, Date = day, SubjectId = subjectId, TakenByUserId = t2User,
                        CreatedAt = now, UpdatedAt = now,
                    };
                    foreach (var (student, status) in records)
                        session.Records.Add(new AttendanceRecord { StudentId = student.Id, Status = status });
                    return session;
                }
                db.AttendanceSessions.AddRange(
                    Session(secA, null, (sA, AttendanceRules.Present)),
                    Session(secB, null, (sB, AttendanceRules.Late), (sA2, AttendanceRules.Present)),
                    Session(secC, null, (sC, AttendanceRules.Absent)),
                    Session(secC, math.Id, (sC, AttendanceRules.Present)));
                await db.SaveChangesAsync();

                (w.SecA, w.SecB, w.SecC, w.SecP, w.MathC) = (secA.Id, secB.Id, secC.Id, past.Id, math.Id);
                (w.SA, w.SA2, w.SB, w.SC, w.SP) = (sA.Id, sA2.Id, sB.Id, sC.Id, sP.Id);
                (w.PA, w.PB, w.PX, w.PT1) = (pA.Id, pB.Id, pX.Id, pT1.Id);
            }

            w.T1 = new Actor(t1User, await LoginAsync(factory, t1User));
            w.T2 = new Actor(t2User, await LoginAsync(factory, t2User));
            w.T3 = new Actor(t3User, await LoginAsync(factory, t3User));
            w.ParentUser = new Actor(parentUser, await LoginAsync(factory, parentUser));
            w.StudentUser = new Actor(studentUser, await LoginAsync(factory, studentUser));
            return w;
        }

        /// <summary>A test-only role holding the three ViewScoped permissions (the real Parent/Student roles hold nothing yet).</summary>
        private static async Task<Role> CreateScopedRoleAsync(ApplicationDbContext db, string prefix)
        {
            var permissions = await db.Permissions
                .Where(p => p.Name == Permissions.StudentsViewScoped
                            || p.Name == Permissions.ParentsViewScoped
                            || p.Name == Permissions.AttendanceViewScoped)
                .ToListAsync();
            Assert.Equal(3, permissions.Count);

            var role = new Role { Name = $"{prefix}{TestData.Unique()}", IsActive = true };
            foreach (var p in permissions)
                role.RolePermissions.Add(new RolePermission { Permission = p });
            db.Roles.Add(role);
            await db.SaveChangesAsync();
            return role;
        }

        private static readonly ConcurrentDictionary<int, string> Usernames = new();

        private static async Task<int> CreateUserAsync(ApplicationDbContext db, IPasswordHasher hasher, int roleId, string tag)
        {
            var username = $"scope_{tag}_{TestData.Unique()}";
            var user = new User
            {
                Username = username,
                Email = $"{username}@test.local",
                PasswordHash = hasher.HashPassword(Password),
                Status = "Active",
                SecurityStamp = Guid.NewGuid(),
            };
            user.UserRoles.Add(new UserRole { RoleId = roleId, AssignedAt = DateTime.UtcNow });
            db.Users.Add(user);
            await db.SaveChangesAsync();
            Usernames[user.Id] = username;
            return user.Id;
        }

        private static async Task<string> LoginAsync(CustomWebApplicationFactory factory, int userId)
        {
            using var client = factory.CreateClient();
            var response = await client.PostAsJsonAsync("/api/auth/login", new LoginRequest(Usernames[userId], Password));
            response.EnsureSuccessStatusCode();
            return (await response.Content.ReadFromJsonAsync<AuthResult>())!.Token;
        }
    }
}
