// File: backend/SchoolManagement.Tests/IntegrationTests/ExpectedRoleMatrix.cs
//
// THE agreed role → permission matrix (Batch E1), written out by hand. Two tests read it:
//   - RoleMatrixSnapshotTests: the migrated database must contain exactly these grants;
//   - AccessMatrixTests: every role × endpoint must behave as these grants say.
// Changing who can do what = change this file, in the same commit, on purpose.

using SchoolManagement.Domain.Constants;

namespace SchoolManagement.Tests.IntegrationTests;

internal static class ExpectedRoleMatrix
{
    private static readonly string[] StaffReads =
    {
        Permissions.ClassSectionsView, Permissions.SubjectsView, Permissions.TeachersView,
        Permissions.StudentsView, Permissions.ParentsView, Permissions.AttendanceView,
    };

    /// <summary>Grant rows per system role. Admin has none: it holds the whole catalog in code.</summary>
    public static readonly IReadOnlyDictionary<string, IReadOnlySet<string>> SeededGrants =
        new Dictionary<string, IReadOnlySet<string>>
        {
            [RoleNames.Admin] = new HashSet<string>(),
            [RoleNames.Supervisor] = new HashSet<string>(StaffReads)
            {
                Permissions.AdmissionsView, Permissions.AttendanceManage, Permissions.StudentsViewSensitive,
            },
            [RoleNames.Clerk] = new HashSet<string>(StaffReads)
            {
                Permissions.AdmissionsView, Permissions.AttendanceManage,
            },
            // Deliberately no Admissions.View (applicant religion, category, medical notes).
            [RoleNames.Teacher] = new HashSet<string>(StaffReads)
            {
                Permissions.AttendanceMark,
            },
            // Nothing until F1 adds ownership checks ("own child", "own record").
            [RoleNames.Student] = new HashSet<string>(),
            [RoleNames.Parent] = new HashSet<string>(),
        };

    /// <summary>What each role effectively holds at runtime.</summary>
    public static IReadOnlySet<string> EffectivePermissions(string role) =>
        role == RoleNames.Admin ? Permissions.All : SeededGrants[role];
}
