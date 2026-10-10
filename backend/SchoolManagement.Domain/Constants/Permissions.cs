namespace SchoolManagement.Domain.Constants;

/// <summary>One entry of the permission catalog. <see cref="Name"/> ("Module.Action") is the key everywhere.</summary>
public sealed record PermissionDefinition(string Name, string Module, string Description);

/// <summary>
/// Single source of truth for every permission in the system (Batch E1).
///
/// - Controllers use these constants in [Authorize(Policy = Permissions.Xxx)]; the
///   PermissionPolicyProvider builds a policy for each name automatically.
/// - The Permissions TABLE mirrors <see cref="Definitions"/>, keyed by name. Each migration that
///   adds a permission inserts its row; a test checks that the table and this list are identical.
/// - Admin holds every permission here IN CODE (PermissionCacheService), never through table rows,
///   so a new permission can never lock Admin out.
/// - Only modules that exist get permissions. Future actions (Students.Transfer, Students.Leave,
///   Dashboard, Reports...) are added together with their endpoints.
/// </summary>
public static class Permissions
{
    // ── Users ──────────────────────────────────────────────────────────
    public const string UsersView = "Users.View";
    public const string UsersCreate = "Users.Create";
    public const string UsersEdit = "Users.Edit";
    public const string UsersDelete = "Users.Delete";
    /// <summary>Activate/deactivate/suspend and unlock. Separate from Edit: it ends sessions.</summary>
    public const string UsersChangeStatus = "Users.ChangeStatus";
    public const string UsersResetPassword = "Users.ResetPassword";

    // ── Roles ──────────────────────────────────────────────────────────
    public const string RolesView = "Roles.View";
    public const string RolesCreate = "Roles.Create";
    /// <summary>Rename/describe, change permissions, activate/deactivate. Roles are never deleted.</summary>
    public const string RolesEdit = "Roles.Edit";

    // ── Settings ───────────────────────────────────────────────────────
    public const string SettingsView = "Settings.View";
    public const string SettingsEdit = "Settings.Edit";

    // ── Class Sections ─────────────────────────────────────────────────
    public const string ClassSectionsView = "ClassSections.View";
    public const string ClassSectionsCreate = "ClassSections.Create";
    public const string ClassSectionsEdit = "ClassSections.Edit";
    public const string ClassSectionsDelete = "ClassSections.Delete";

    // ── Subjects ───────────────────────────────────────────────────────
    public const string SubjectsView = "Subjects.View";
    public const string SubjectsCreate = "Subjects.Create";
    public const string SubjectsEdit = "Subjects.Edit";
    public const string SubjectsDelete = "Subjects.Delete";

    // ── Teachers ───────────────────────────────────────────────────────
    public const string TeachersView = "Teachers.View";
    public const string TeachersCreate = "Teachers.Create";
    public const string TeachersEdit = "Teachers.Edit";
    public const string TeachersDelete = "Teachers.Delete";
    /// <summary>Assign subjects and class-teacher duty.</summary>
    public const string TeachersAssign = "Teachers.Assign";

    // ── Admissions ─────────────────────────────────────────────────────
    public const string AdmissionsView = "Admissions.View";
    public const string AdmissionsCreate = "Admissions.Create";
    /// <summary>Edit, confirm, reject, and look up guardian matches.</summary>
    public const string AdmissionsEdit = "Admissions.Edit";
    public const string AdmissionsEnroll = "Admissions.Enroll";
    public const string AdmissionsDelete = "Admissions.Delete";

    // ── Students ───────────────────────────────────────────────────────
    public const string StudentsView = "Students.View";
    public const string StudentsCreate = "Students.Create";
    public const string StudentsEdit = "Students.Edit";
    public const string StudentsDelete = "Students.Delete";
    public const string StudentsViewSensitive = "Students.ViewSensitive";

    // ── Parents ────────────────────────────────────────────────────────
    public const string ParentsView = "Parents.View";
    public const string ParentsCreate = "Parents.Create";
    /// <summary>Edit parents and link/unlink them to students (from either side).</summary>
    public const string ParentsEdit = "Parents.Edit";
    public const string ParentsDelete = "Parents.Delete";

    // ── Attendance ─────────────────────────────────────────────────────
    public const string AttendanceView = "Attendance.View";
    /// <summary>Mark attendance for your own class/subjects (scope checked in the service).</summary>
    public const string AttendanceMark = "Attendance.Mark";
    /// <summary>Mark attendance for any class.</summary>
    public const string AttendanceManage = "Attendance.Manage";

    /// <summary>The catalog, in display order (grouped by module).</summary>
    public static readonly IReadOnlyList<PermissionDefinition> Definitions = new PermissionDefinition[]
    {
        new(UsersView, "Users", "See the list of users and their details."),
        new(UsersCreate, "Users", "Create user accounts."),
        new(UsersEdit, "Users", "Edit user accounts and their role."),
        new(UsersDelete, "Users", "Delete (soft-delete) user accounts."),
        new(UsersChangeStatus, "Users", "Activate, deactivate, suspend and unlock accounts. Ends the user's sessions."),
        new(UsersResetPassword, "Users", "Set a temporary password that must be changed at next login."),

        new(RolesView, "Roles", "See roles and the permissions they grant."),
        new(RolesCreate, "Roles", "Create custom roles."),
        new(RolesEdit, "Roles", "Rename, change permissions of, and activate/deactivate roles."),

        new(SettingsView, "Settings", "See school settings."),
        new(SettingsEdit, "Settings", "Change school settings."),

        new(ClassSectionsView, "ClassSections", "See classes and sections."),
        new(ClassSectionsCreate, "ClassSections", "Create class sections."),
        new(ClassSectionsEdit, "ClassSections", "Edit and activate/deactivate class sections."),
        new(ClassSectionsDelete, "ClassSections", "Delete class sections."),

        new(SubjectsView, "Subjects", "See subjects."),
        new(SubjectsCreate, "Subjects", "Create subjects."),
        new(SubjectsEdit, "Subjects", "Edit subjects, including marks configuration."),
        new(SubjectsDelete, "Subjects", "Delete subjects."),

        new(TeachersView, "Teachers", "See teachers and their assignments."),
        new(TeachersCreate, "Teachers", "Create teachers."),
        new(TeachersEdit, "Teachers", "Edit and activate/deactivate teachers."),
        new(TeachersDelete, "Teachers", "Delete teachers."),
        new(TeachersAssign, "Teachers", "Assign subjects and class-teacher duty."),

        new(AdmissionsView, "Admissions", "See admission applications."),
        new(AdmissionsCreate, "Admissions", "Register new applications."),
        new(AdmissionsEdit, "Admissions", "Edit, confirm and reject applications."),
        new(AdmissionsEnroll, "Admissions", "Enrol admitted applicants as students."),
        new(AdmissionsDelete, "Admissions", "Delete applications."),

        new(StudentsView, "Students", "See students, their enrolments and guardians."),
        new(StudentsCreate, "Students", "Create students."),
        new(StudentsEdit, "Students", "Edit students."),
        new(StudentsDelete, "Students", "Delete students."),
        new(StudentsViewSensitive, "Students", "See and edit full Aadhaar, passport and visa details."),

        new(ParentsView, "Parents", "See parents and their linked children."),
        new(ParentsCreate, "Parents", "Create parents."),
        new(ParentsEdit, "Parents", "Edit parents and link/unlink them to students."),
        new(ParentsDelete, "Parents", "Delete parents."),

        new(AttendanceView, "Attendance", "See attendance rosters, histories and summaries."),
        new(AttendanceMark, "Attendance", "Mark attendance for own class or subjects."),
        new(AttendanceManage, "Attendance", "Mark and edit attendance for any class."),
    };

    /// <summary>Every permission name in the catalog. Admin holds all of these in code.</summary>
    public static readonly IReadOnlySet<string> All = Definitions.Select(d => d.Name).ToHashSet();

    /// <summary>"Students.Edit" → "Students".</summary>
    public static string ModuleOf(string permission) => permission[..permission.IndexOf('.')];

    /// <summary>"Students" → "Students.View".</summary>
    public static string ViewOf(string module) => module + ".View";
}
