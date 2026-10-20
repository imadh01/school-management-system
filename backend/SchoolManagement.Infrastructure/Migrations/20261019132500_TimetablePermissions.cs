using Microsoft.EntityFrameworkCore.Infrastructure;
using Microsoft.EntityFrameworkCore.Migrations;
using SchoolManagement.Infrastructure.Persistence;

#nullable disable

namespace SchoolManagement.Infrastructure.Migrations
{
    /// <summary>
    /// Timetable permissions. HAND-WRITTEN, data only: no table or column changes, so the model snapshot is untouched
    /// and there is no Designer file. Like E1/F1/G1b it is a frozen copy keyed by NAME and never reads the C# constants.
    ///
    /// Up:
    ///   1. Adds Timetable.View, Timetable.ViewScoped, Timetable.Edit, Timetable.ManagePeriods (MERGE by name).
    ///   2. Grants (the approved matrix): Supervisor View + Edit; Clerk View; Teacher ViewScoped.
    ///      Admin holds the whole catalog in code. Student and Parent get nothing.
    ///      ManagePeriods is Admin only (nobody is granted it here).
    /// Down removes the grants and then the permissions. Custom roles that were given a Timetable permission
    /// through the Roles page lose it too (the foreign key cascades), which is the intended effect of removing a permission.
    /// </summary>
    [DbContext(typeof(ApplicationDbContext))]
    [Migration("20261019132500_TimetablePermissions")]
    public partial class TimetablePermissions : Migration
    {
        private static readonly (string Name, string Module, string Description)[] Added =
        {
            ("Timetable.View", "Timetable", "See every class's timetable, the school's periods and any teacher's week."),
            ("Timetable.ViewScoped", "Timetable", "See only your own teaching timetable."),
            ("Timetable.Edit", "Timetable", "Add, change and clear lessons on a class timetable, and copy a timetable from another class."),
            ("Timetable.ManagePeriods", "Timetable", "Create, change and delete the periods (time slots) of the school day."),
        };

        private static readonly (string Role, string[] Permissions)[] Grants =
        {
            ("Supervisor", new[] { "Timetable.View", "Timetable.Edit" }),
            ("Clerk", new[] { "Timetable.View" }),
            ("Teacher", new[] { "Timetable.ViewScoped" }),
        };

        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.Sql(
                "MERGE [Permissions] AS t\n" +
                $"USING (VALUES\n{Values(Added.Select(a => new[] { a.Name, a.Module, a.Description }))}\n) AS s ([Name], [Module], [Description])\n" +
                "ON t.[Name] = s.[Name]\n" +
                "WHEN MATCHED THEN UPDATE SET t.[Module] = s.[Module], t.[Description] = s.[Description]\n" +
                "WHEN NOT MATCHED THEN INSERT ([Name], [Module], [Description]) VALUES (s.[Name], s.[Module], s.[Description]);");

            foreach (var (role, permissions) in Grants)
                migrationBuilder.Sql(GrantToRole(role, permissions));
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            foreach (var (role, permissions) in Grants)
                migrationBuilder.Sql(RevokeFromRole(role, permissions));

            migrationBuilder.Sql($"DELETE FROM [Permissions] WHERE [Name] IN ({InList(Added.Select(a => a.Name))});");
        }

        // ── SQL helpers (same as E1/F1) ───────────────────────────────────

        private static string Q(string value) => "N'" + value.Replace("'", "''") + "'";

        private static string InList(IEnumerable<string> values) => string.Join(", ", values.Select(Q));

        private static string Values(IEnumerable<string[]> rows) =>
            string.Join(",\n", rows.Select(r => "    (" + string.Join(", ", r.Select(Q)) + ")"));

        private static string GrantToRole(string role, IEnumerable<string> permissions) =>
            "INSERT INTO [RolePermissions] ([RoleId], [PermissionId])\n" +
            "SELECT r.[Id], p.[Id] FROM [Roles] r JOIN [Permissions] p ON 1 = 1\n" +
            $"WHERE r.[Name] = {Q(role)} AND p.[Name] IN ({InList(permissions)})\n" +
            "AND NOT EXISTS (SELECT 1 FROM [RolePermissions] x WHERE x.[RoleId] = r.[Id] AND x.[PermissionId] = p.[Id]);";

        private static string RevokeFromRole(string role, IEnumerable<string> permissions) =>
            "DELETE rp FROM [RolePermissions] rp\n" +
            "JOIN [Roles] r ON r.[Id] = rp.[RoleId]\n" +
            "JOIN [Permissions] p ON p.[Id] = rp.[PermissionId]\n" +
            $"WHERE r.[Name] = {Q(role)} AND p.[Name] IN ({InList(permissions)});";
    }
}
