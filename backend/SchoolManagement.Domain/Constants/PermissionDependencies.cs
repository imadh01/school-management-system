namespace SchoolManagement.Domain.Constants;

/// <summary>
/// Permissions that only make sense together. A role that could save a student but not load the
/// class dropdown would get a broken form, so the Roles API refuses such combinations.
///
/// Two rules:
///   1. Every action except "View" needs its own module's View (Students.Edit needs Students.View).
///   2. Some actions also need another module's View, listed below (the screens load it).
/// </summary>
public static class PermissionDependencies
{
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

        // Linking a parent picks a student.
        [Permissions.ParentsEdit] = new[] { Permissions.StudentsView },

        // The attendance screen picks a class and (for subject-wise attendance) a subject.
        [Permissions.AttendanceMark] = new[] { Permissions.ClassSectionsView, Permissions.SubjectsView },
        [Permissions.AttendanceManage] = new[] { Permissions.ClassSectionsView, Permissions.SubjectsView },

        // The user form picks a role.
        [Permissions.UsersCreate] = new[] { Permissions.RolesView },
        [Permissions.UsersEdit] = new[] { Permissions.RolesView },
    };

    /// <summary>Everything <paramref name="permission"/> needs alongside it (empty for a View).</summary>
    public static IReadOnlyCollection<string> RequiredFor(string permission)
    {
        var required = new SortedSet<string>(StringComparer.Ordinal);

        var ownView = Permissions.ViewOf(Permissions.ModuleOf(permission));
        if (permission != ownView)
            required.Add(ownView);

        if (CrossModule.TryGetValue(permission, out var extra))
            required.UnionWith(extra);

        return required;
    }

    /// <summary>Each granted permission whose requirement is missing from the same grant set.</summary>
    public static IReadOnlyList<(string Permission, string Missing)> FindMissing(IEnumerable<string> granted)
    {
        var set = granted.ToHashSet(StringComparer.Ordinal);
        return set
            .OrderBy(p => p, StringComparer.Ordinal)
            .SelectMany(p => RequiredFor(p).Where(r => !set.Contains(r)).Select(r => (p, r)))
            .ToList();
    }
}
