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
    /// <summary>
    /// Read only the students related to you (Batch F1): the ones you teach this academic year
    /// (class teacher or subject teacher of their current class), your own children (as their
    /// parent) and yourself (as a student). Fields are reduced; see DataScope.
    /// Students.View wins when both are held.
    /// </summary>
    public const string StudentsViewScoped = "Students.ViewScoped";

    // ── Parents ────────────────────────────────────────────────────────
    public const string ParentsView = "Parents.View";
    public const string ParentsCreate = "Parents.Create";
    /// <summary>Edit parents and link/unlink them to students (from either side).</summary>
    public const string ParentsEdit = "Parents.Edit";
    public const string ParentsDelete = "Parents.Delete";
    /// <summary>Name and mobile of the parents of students you teach this year, plus your own parent record (F1).</summary>
    public const string ParentsViewScoped = "Parents.ViewScoped";

    // ── Attendance ─────────────────────────────────────────────────────
    public const string AttendanceView = "Attendance.View";
    /// <summary>Mark attendance for your own class/subjects (scope checked in the service).</summary>
    public const string AttendanceMark = "Attendance.Mark";
    /// <summary>Mark attendance for any class.</summary>
    public const string AttendanceManage = "Attendance.Manage";
    /// <summary>
    /// Attendance of your own classes (class teacher: every session of the class) and your own
    /// subjects this year, plus all attendance of your own children or yourself (F1).
    /// </summary>
    public const string AttendanceViewScoped = "Attendance.ViewScoped";

    // ── Documents (G1b) ────────────────────────────────────────────────
    /// <summary>See the list of documents and their details, and download non-sensitive files.
    /// Identity and medical files also need Students.ViewSensitive.</summary>
    public const string DocumentsView = "Documents.View";
    public const string DocumentsUpload = "Documents.Upload";
    /// <summary>Verify, reject and unverify. Not your own upload, unless you are an Admin (checked in the service).</summary>
    public const string DocumentsVerify = "Documents.Verify";
    /// <summary>Delete (soft) a document that is not verified. The file is kept for the retention period.</summary>
    public const string DocumentsDelete = "Documents.Delete";

    // ── Timetable ──────────────────────────────────────────────────────
    /// <summary>See every class's timetable, the school's periods and any teacher's week.</summary>
    public const string TimetableView = "Timetable.View";
    /// <summary>See only your own teaching week (a teacher). Not other teachers' weeks and not class grids.</summary>
    public const string TimetableViewScoped = "Timetable.ViewScoped";
    /// <summary>Put lessons on a class's grid, clear them, and copy one class's timetable to another.</summary>
    public const string TimetableEdit = "Timetable.Edit";
    /// <summary>Create, change and delete the periods (time slots) of the school day.</summary>
    public const string TimetableManagePeriods = "Timetable.ManagePeriods";

    // ── Academic years ─────────────────────────────────────────────────
    // The year LIST is reference data for every dropdown, so (like Countries) any signed-in user may read it.
    // These permissions guard the Academic Years page and every change. Only Admin holds them by default.
    /// <summary>Open the Academic Years page and see one year with its counts.</summary>
    public const string AcademicYearsView = "AcademicYears.View";
    /// <summary>Add an academic year.</summary>
    public const string AcademicYearsCreate = "AcademicYears.Create";
    /// <summary>Change a year's name and dates (within the rules of its status).</summary>
    public const string AcademicYearsEdit = "AcademicYears.Edit";
    /// <summary>Make an Upcoming year current; the old current year becomes Ended. Changes what teachers see.</summary>
    public const string AcademicYearsSetCurrent = "AcademicYears.SetCurrent";
    /// <summary>Close an Ended year for good. It cannot be reopened.</summary>
    public const string AcademicYearsClose = "AcademicYears.Close";
    /// <summary>Delete an Upcoming year that has no classes or periods.</summary>
    public const string AcademicYearsDelete = "AcademicYears.Delete";
    /// <summary>Copy the timetable periods of one year into another.</summary>
    public const string AcademicYearsCopyPeriods = "AcademicYears.CopyPeriods";

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
        new(StudentsViewScoped, "Students",
            "See only related students, with reduced details: those you teach this year (class or subject teacher), your own children, or yourself."),

        new(ParentsView, "Parents", "See parents and their linked children."),
        new(ParentsCreate, "Parents", "Create parents."),
        new(ParentsEdit, "Parents", "Edit parents and link/unlink them to students."),
        new(ParentsDelete, "Parents", "Delete parents."),
        new(ParentsViewScoped, "Parents",
            "See only the name and mobile of parents of students you teach this year, and your own parent record."),

        new(AttendanceView, "Attendance", "See attendance rosters, histories and summaries."),
        new(AttendanceMark, "Attendance", "Mark attendance for own class or subjects."),
        new(AttendanceManage, "Attendance", "Mark and edit attendance for any class."),
        new(AttendanceViewScoped, "Attendance",
            "See attendance of your own classes and subjects this year, and all attendance of your own children or yourself."),

        new(DocumentsView, "Documents",
            "See documents of students and applicants and download them (identity and medical files also need Students.ViewSensitive)."),
        new(DocumentsUpload, "Documents", "Upload documents for students and applicants."),
        new(DocumentsVerify, "Documents", "Verify, reject and unverify documents uploaded by someone else."),
        new(DocumentsDelete, "Documents", "Delete documents that are not verified."),

        new(TimetableView, "Timetable", "See every class's timetable, the school's periods and any teacher's week."),
        new(TimetableViewScoped, "Timetable", "See only your own teaching timetable."),
        new(TimetableEdit, "Timetable", "Add, change and clear lessons on a class timetable, and copy a timetable from another class."),
        new(TimetableManagePeriods, "Timetable", "Create, change and delete the periods (time slots) of the school day."),

        new(AcademicYearsView, "AcademicYears", "Open the Academic Years page and see each year with its counts."),
        new(AcademicYearsCreate, "AcademicYears", "Add an academic year."),
        new(AcademicYearsEdit, "AcademicYears", "Change an academic year's name and dates."),
        new(AcademicYearsSetCurrent, "AcademicYears",
            "Make an Upcoming year the current year. The old current year becomes Ended; teachers then see the new year's classes."),
        new(AcademicYearsClose, "AcademicYears", "Close an Ended academic year permanently (it cannot be reopened)."),
        new(AcademicYearsDelete, "AcademicYears", "Delete an Upcoming academic year that has no classes or periods."),
        new(AcademicYearsCopyPeriods, "AcademicYears", "Copy the timetable periods of one academic year into another."),
    };

    /// <summary>Every permission name in the catalog. Admin holds all of these in code.</summary>
    public static readonly IReadOnlySet<string> All = Definitions.Select(d => d.Name).ToHashSet();

    /// <summary>"Students.Edit" → "Students".</summary>
    public static string ModuleOf(string permission) => permission[..permission.IndexOf('.')];

    /// <summary>"Students" → "Students.View".</summary>
    public static string ViewOf(string module) => module + ".View";

    /// <summary>"Students" → "Students.ViewScoped" (only some modules have one).</summary>
    public static string ViewScopedOf(string module) => module + ".ViewScoped";

    /// <summary>True for the read permissions: X.View and X.ViewScoped need nothing alongside them.</summary>
    public static bool IsRead(string permission) =>
        permission.EndsWith(".View", StringComparison.Ordinal) ||
        permission.EndsWith(".ViewScoped", StringComparison.Ordinal);
}
