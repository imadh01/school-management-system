namespace SchoolManagement.Domain.Constants;

/// <summary>
/// Single source of truth for every permission name in the system.
/// Controllers reference these via [Authorize(Policy = Permissions.XxxYyy)].
/// The <see cref="PermissionPolicyProvider"/> auto-generates policies for each.
/// The seed migration inserts rows into the Permissions table matching these values.
/// </summary>
public static class Permissions
{
    // ── Users ──────────────────────────────────────────────────────────
    public const string UsersCreate = "Users.Create";

    // ── Class Sections ─────────────────────────────────────────────────
    public const string ClassSectionsCreate = "ClassSections.Create";
    public const string ClassSectionsManage = "ClassSections.Manage";

    // ── Admissions ─────────────────────────────────────────────────────
    public const string AdmissionsManage = "Admissions.Manage";

    // ── Students ───────────────────────────────────────────────────────
    public const string StudentsManage = "Students.Manage";
    public const string StudentsViewSensitive = "Students.ViewSensitive";

    // ── Parents ────────────────────────────────────────────────────────
    public const string ParentsManage = "Parents.Manage";

    // ── Subjects ───────────────────────────────────────────────────────
    public const string SubjectsManage = "Subjects.Manage";

    // ── Teachers ───────────────────────────────────────────────────────
    public const string TeachersManage = "Teachers.Manage";

    // ── Attendance ─────────────────────────────────────────────────────
    public const string AttendanceManage = "Attendance.Manage";
    public const string AttendanceMark = "Attendance.Mark";

    /// <summary>
    /// Every permission name in the catalog. Admin is treated as holding
    /// all of these in code, so new permissions added here are immediately
    /// available to Admin without a migration.
    /// </summary>
    public static readonly IReadOnlySet<string> All = new HashSet<string>
    {
        UsersCreate,
        ClassSectionsCreate,
        ClassSectionsManage,
        AdmissionsManage,
        StudentsManage,
        StudentsViewSensitive,
        ParentsManage,
        SubjectsManage,
        TeachersManage,
        AttendanceManage,
        AttendanceMark,
    };
}
