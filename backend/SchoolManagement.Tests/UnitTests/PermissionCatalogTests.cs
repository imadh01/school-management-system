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
            foreach (var required in PermissionDependencies.RequiredFor(name))
                Assert.True(Permissions.All.Contains(required), $"{name} requires unknown {required}");
    }

    [Fact]
    public void A_View_needs_nothing_and_any_other_action_needs_its_own_View()
    {
        Assert.Empty(PermissionDependencies.RequiredFor(Permissions.StudentsView));
        Assert.Contains(Permissions.StudentsView, PermissionDependencies.RequiredFor(Permissions.StudentsDelete));
        Assert.Contains(Permissions.StudentsView, PermissionDependencies.RequiredFor(Permissions.StudentsViewSensitive));
    }

    [Fact]
    public void Cross_module_dependencies_cover_the_class_dropdown()
    {
        Assert.Contains(Permissions.ClassSectionsView, PermissionDependencies.RequiredFor(Permissions.StudentsCreate));
        Assert.Contains(Permissions.ClassSectionsView, PermissionDependencies.RequiredFor(Permissions.AdmissionsEnroll));
        Assert.Contains(Permissions.SubjectsView, PermissionDependencies.RequiredFor(Permissions.TeachersAssign));
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
