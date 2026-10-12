namespace SchoolManagement.Domain.Constants;

/// <summary>
/// Authorization policies that pass when the caller holds ANY ONE of several permissions.
/// Single-permission policies need no entry: the PermissionPolicyProvider builds one for every
/// name in <see cref="Permissions.All"/>. These names deliberately are not permissions, so they
/// never appear in the role matrix.
/// </summary>
public static class CompositePolicies
{
    /// <summary>Save attendance: any class (Manage) or your own class/subject (Mark, checked in the service).</summary>
    public const string AttendanceWrite = "Attendance.Write";

    /// <summary>Read students: all of them (View) or only related ones (ViewScoped). The controller picks the scope.</summary>
    public const string StudentsRead = "Students.Read";

    /// <summary>Read parents: all of them (View) or only related ones (ViewScoped).</summary>
    public const string ParentsRead = "Parents.Read";

    /// <summary>Read attendance: everything (View) or your own classes/subjects/children (ViewScoped).</summary>
    public const string AttendanceRead = "Attendance.Read";

    public static readonly IReadOnlyDictionary<string, string[]> AnyOf = new Dictionary<string, string[]>
    {
        [AttendanceWrite] = new[] { Permissions.AttendanceManage, Permissions.AttendanceMark },
        [StudentsRead] = new[] { Permissions.StudentsView, Permissions.StudentsViewScoped },
        [ParentsRead] = new[] { Permissions.ParentsView, Permissions.ParentsViewScoped },
        [AttendanceRead] = new[] { Permissions.AttendanceView, Permissions.AttendanceViewScoped },
    };
}
