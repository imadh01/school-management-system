// Batch E1: the permission catalog and its dependency table must be internally consistent.
// (The database side is checked by RoleMatrixSnapshotTests.)

using SchoolManagement.Domain.Constants;
using Xunit;

namespace SchoolManagement.Tests.UnitTests;

public class PermissionCatalogTests
{
    [Fact]
    public void Names_are_unique()
    {
        var duplicates = Permissions.Definitions.GroupBy(d => d.Name).Where(g => g.Count() > 1).Select(g => g.Key);
        Assert.Empty(duplicates);
    }

    [Fact]
    public void Every_name_is_Module_dot_Action_and_matches_its_module()
    {
        foreach (var d in Permissions.Definitions)
        {
            Assert.Matches(@"^[A-Z][A-Za-z]+\.[A-Z][A-Za-z]+$", d.Name);
            Assert.Equal(d.Module, Permissions.ModuleOf(d.Name));
            Assert.False(string.IsNullOrWhiteSpace(d.Description), $"{d.Name} has no description");
            Assert.True(d.Description.Length <= 200, $"{d.Name}: description longer than the column (200)");
        }
    }

    [Fact]
    public void Every_module_has_a_View_permission()
    {
        foreach (var module in Permissions.Definitions.Select(d => d.Module).Distinct())
            Assert.Contains(Permissions.ViewOf(module), Permissions.All);
    }

    [Fact]
    public void Every_dependency_points_at_a_permission_in_the_catalog()
    {
        foreach (var name in Permissions.All)
            foreach (var requirement in PermissionDependencies.RequiredFor(name))
                foreach (var option in requirement.AnyOf)
                    Assert.True(Permissions.All.Contains(option), $"{name} requires unknown {option}");
    }

    /// <summary>The single-option requirements of a permission, flattened (most requirements have one option).</summary>
    private static IEnumerable<string> Requires(string permission) =>
        PermissionDependencies.RequiredFor(permission).Where(r => r.AnyOf.Count == 1).Select(r => r.AnyOf[0]);

    [Fact]
    public void A_View_needs_nothing_and_any_other_action_needs_its_own_View()
    {
        Assert.Empty(PermissionDependencies.RequiredFor(Permissions.StudentsView));
        Assert.Contains(Permissions.StudentsView, Requires(Permissions.StudentsDelete));
        Assert.Contains(Permissions.StudentsView, Requires(Permissions.StudentsViewSensitive));
    }

    [Fact]
    public void Cross_module_dependencies_cover_the_class_dropdown()
    {
        Assert.Contains(Permissions.ClassSectionsView, Requires(Permissions.StudentsCreate));
        Assert.Contains(Permissions.ClassSectionsView, Requires(Permissions.AdmissionsEnroll));
        Assert.Contains(Permissions.SubjectsView, Requires(Permissions.TeachersAssign));
    }

    // ── F1: scoped reads ────────────────────────────────────────────────

    [Theory]
    [InlineData(Permissions.StudentsViewScoped)]
    [InlineData(Permissions.ParentsViewScoped)]
    [InlineData(Permissions.AttendanceViewScoped)]
    public void A_scoped_read_needs_nothing(string permission)
    {
        Assert.Empty(PermissionDependencies.RequiredFor(permission));
        Assert.Contains(permission, Permissions.All);
    }

    [Fact]
    public void Writes_are_not_scoped_so_a_scoped_read_is_not_enough_for_them()
    {
        // A "Students.ViewScoped + Students.Edit" role could edit students it cannot see.
        var missing = PermissionDependencies.FindMissing(new[]
        {
            Permissions.StudentsViewScoped, Permissions.StudentsEdit, Permissions.ClassSectionsView,
        });

        Assert.Contains((Permissions.StudentsEdit, Permissions.StudentsView), missing);
    }

    [Fact]
    public void Attendance_Mark_accepts_a_scoped_attendance_read()
    {
        // The service already limits Mark to your own class/subject, so the scoped read is enough.
        var teacherLike = new[]
        {
            Permissions.AttendanceViewScoped, Permissions.AttendanceMark,
            Permissions.ClassSectionsView, Permissions.SubjectsView,
        };
        Assert.Empty(PermissionDependencies.FindMissing(teacherLike));

        var withoutAnyRead = new[] { Permissions.AttendanceMark, Permissions.ClassSectionsView, Permissions.SubjectsView };
        Assert.Contains((Permissions.AttendanceMark, "Attendance.View or Attendance.ViewScoped"),
            PermissionDependencies.FindMissing(withoutAnyRead));
    }

    [Fact]
    public void Every_composite_policy_lists_real_permissions_and_is_not_itself_a_permission()
    {
        foreach (var (name, anyOf) in CompositePolicies.AnyOf)
        {
            Assert.DoesNotContain(name, Permissions.All);
            Assert.All(anyOf, p => Assert.Contains(p, Permissions.All));
        }
    }

    [Fact]
    public void FindMissing_reports_each_gap()
    {
        var missing = PermissionDependencies.FindMissing(new[] { Permissions.StudentsEdit });

        Assert.Contains((Permissions.StudentsEdit, Permissions.StudentsView), missing);
        Assert.Contains((Permissions.StudentsEdit, Permissions.ClassSectionsView), missing);
    }

    [Fact]
    public void Kept_permission_names_did_not_change()
    {
        // Renaming these would silently drop existing grants and break the D1 hotfix checks.
        Assert.Equal("Attendance.Manage", Permissions.AttendanceManage);
        Assert.Equal("Attendance.Mark", Permissions.AttendanceMark);
        Assert.Equal("Students.ViewSensitive", Permissions.StudentsViewSensitive);
        Assert.Equal("Users.Create", Permissions.UsersCreate);
        Assert.Equal("ClassSections.Create", Permissions.ClassSectionsCreate);
    }
}
