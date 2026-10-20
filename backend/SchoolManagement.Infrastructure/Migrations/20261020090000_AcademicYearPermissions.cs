using Microsoft.EntityFrameworkCore.Infrastructure;
using Microsoft.EntityFrameworkCore.Migrations;
using SchoolManagement.Infrastructure.Persistence;

#nullable disable

namespace SchoolManagement.Infrastructure.Migrations
{
    /// <summary>
    /// Academic Years permissions. HAND-WRITTEN, data only: no table or column changes, so the model snapshot is
    /// untouched and there is no Designer file. Like the Timetable one it is a frozen copy keyed by NAME and never
    /// reads the C# constants.
    ///
    /// Up: adds the seven AcademicYears.* permissions (MERGE by name). NO role is granted any of them: Admin holds the
    /// whole catalog in code, and every other role keeps reading the year list through the plain signed-in endpoint.
    /// Down: removes the permissions (and any grant a custom role was given through the Roles page, by cascade).
    /// </summary>
    [DbContext(typeof(ApplicationDbContext))]
    [Migration("20261020090000_AcademicYearPermissions")]
    public partial class AcademicYearPermissions : Migration
    {
        private static readonly (string Name, string Module, string Description)[] Added =
        {
            ("AcademicYears.View", "AcademicYears", "Open the Academic Years page and see each year with its counts."),
            ("AcademicYears.Create", "AcademicYears", "Add an academic year."),
            ("AcademicYears.Edit", "AcademicYears", "Change an academic year's name and dates."),
            ("AcademicYears.SetCurrent", "AcademicYears", "Make an Upcoming year the current year. The old current year becomes Ended; teachers then see the new year's classes."),
            ("AcademicYears.Close", "AcademicYears", "Close an Ended academic year permanently (it cannot be reopened)."),
            ("AcademicYears.Delete", "AcademicYears", "Delete an Upcoming academic year that has no classes or periods."),
            ("AcademicYears.CopyPeriods", "AcademicYears", "Copy the timetable periods of one academic year into another."),
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
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.Sql($"DELETE FROM [Permissions] WHERE [Name] IN ({InList(Added.Select(a => a.Name))});");
        }

        // ── SQL helpers (same as the Timetable migration) ─────────────────

        private static string Q(string value) => "N'" + value.Replace("'", "''") + "'";

        private static string InList(IEnumerable<string> values) => string.Join(", ", values.Select(Q));

        private static string Values(IEnumerable<string[]> rows) =>
            string.Join(",\n", rows.Select(r => "    (" + string.Join(", ", r.Select(Q)) + ")"));
    }
}
