namespace SchoolManagement.Domain.Constants;

/// <summary>
/// One thing a permission needs alongside it: ANY ONE of <see cref="AnyOf"/>.
/// Most requirements have a single option ("Students.View"); a few accept a scoped read too
/// ("Attendance.View or Attendance.ViewScoped").
/// </summary>
public sealed record PermissionRequirement(IReadOnlyList<string> AnyOf)
{
    public bool IsMetBy(IReadOnlySet<string> granted) => AnyOf.Any(granted.Contains);

    public override string ToString() => string.Join(" or ", AnyOf);
}

/// <summary>
/// Permissions that only make sense together. A role that could save a student but not load the
/// class dropdown would get a broken form, so the Roles API refuses such combinations.
///
/// Rules:
///   1. Reads (X.View, X.ViewScoped) need nothing.
///   2. Every other action needs its own module's FULL View (Students.Edit needs Students.View).
///      Writes are not scoped, so a scoped read is not enough: a "ViewScoped + Edit" role could
///      edit students it cannot even see. The exceptions are listed in <see cref="ScopedReadIsEnough"/>.
///   3. Some actions also need another module's View, listed in <see cref="CrossModule"/>.
/// </summary>
public static class PermissionDependencies
{
    /// <summary>
    /// Actions whose own scope check lives in the service, so a scoped read of their module is enough.
    /// Attendance.Mark: the service only lets you mark your own class or subject.
    /// </summary>
    private static readonly HashSet<string> ScopedReadIsEnough = new(StringComparer.Ordinal)
    {
        Permissions.AttendanceMark,
    };

    private static readonly IReadOnlyDictionary<string, string[]> CrossModule = new Dictionary<string, string[]>
    {
        // Forms with a class dropdown.
        [Permissions.StudentsCreate] = new[] { Permissions.ClassSectionsView },
        [Permissions.StudentsEdit] = new[] { Permissions.ClassSectionsView },
        [Permissions.AdmissionsCreate] = new[] { Permissions.ClassSectionsView },
        [Permissions.AdmissionsEdit] = new[] { Permissions.ClassSectionsView },
        [Permissions.AdmissionsEnroll] = new[] { Permissions.ClassSectionsView },
        [Permissions.SubjectsCreate] = new[] { Permissions.ClassSectionsView },
        [Permissions.SubjectsEdit] = new[] { Permissions.ClassSectionsView },

        // Assigning a teacher picks a subject and a class.
        [Permissions.TeachersAssign] = new[] { Permissions.SubjectsView, Permissions.ClassSectionsView },

        // Linking a parent picks a student (from the full list).
        [Permissions.ParentsEdit] = new[] { Permissions.StudentsView },

        // The attendance screen picks a class and (for subject-wise attendance) a subject.
        [Permissions.AttendanceMark] = new[] { Permissions.ClassSectionsView, Permissions.SubjectsView },
        [Permissions.AttendanceManage] = new[] { Permissions.ClassSectionsView, Permissions.SubjectsView },

        // The timetable grid picks a class and one of its subjects.
        [Permissions.TimetableEdit] = new[] { Permissions.ClassSectionsView, Permissions.SubjectsView },

        // The user form picks a role.
        [Permissions.UsersCreate] = new[] { Permissions.RolesView },
        [Permissions.UsersEdit] = new[] { Permissions.RolesView },
    };

    /// <summary>Everything <paramref name="permission"/> needs alongside it (empty for a read).</summary>
    public static IReadOnlyList<PermissionRequirement> RequiredFor(string permission)
    {
        var required = new List<PermissionRequirement>();
        if (Permissions.IsRead(permission))
            return required;

        var module = Permissions.ModuleOf(permission);
        var ownView = Permissions.ViewOf(module);
        var ownScoped = Permissions.ViewScopedOf(module);

        required.Add(ScopedReadIsEnough.Contains(permission) && Permissions.All.Contains(ownScoped)
            ? new PermissionRequirement(new[] { ownView, ownScoped })
            : new PermissionRequirement(new[] { ownView }));

        if (CrossModule.TryGetValue(permission, out var extra))
            required.AddRange(extra.Select(e => new PermissionRequirement(new[] { e })));

        return required;
    }

    /// <summary>Each granted permission whose requirement is missing from the same grant set.</summary>
    public static IReadOnlyList<(string Permission, string Missing)> FindMissing(IEnumerable<string> granted)
    {
        var set = granted.ToHashSet(StringComparer.Ordinal);
        return set
            .OrderBy(p => p, StringComparer.Ordinal)
            .SelectMany(p => RequiredFor(p).Where(r => !r.IsMetBy(set)).Select(r => (p, r.ToString())))
            .ToList();
    }
}
