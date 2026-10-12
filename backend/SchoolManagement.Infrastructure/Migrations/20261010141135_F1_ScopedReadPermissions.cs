using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace SchoolManagement.Infrastructure.Migrations
{
    /// <summary>
    /// Batch F1: scoped read permissions and sensitive-read audit rows.
    ///
    /// Up:
    ///   1. Permissions: add Students.ViewScoped, Parents.ViewScoped, Attendance.ViewScoped (MERGE by name).
    ///   2. Teacher: Students.View, Parents.View and Attendance.View become their ViewScoped versions,
    ///      so a teacher reads only the students they teach this year (and their own children).
    ///      Student and Parent roles get nothing yet: the portal and its response shapes come later.
    ///   3. AuditLogs.Action may now be 'Read' (someone opened health/identity data).
    /// Down reverses it. It must delete the 'Read' audit rows first, or the old CHECK constraint
    /// cannot be re-created; that loses the sensitive-read history, which is why it is a Down only.
    ///
    /// Like E1, this is a frozen copy keyed by NAME: it never reads the C# constants.
    /// </summary>
    public partial class F1_ScopedReadPermissions : Migration
    {
        private static readonly (string Name, string Module, string Description)[] Added =
        {
            ("Students.ViewScoped", "Students",
                "See only related students, with reduced details: those you teach this year (class or subject teacher), your own children, or yourself."),
            ("Parents.ViewScoped", "Parents",
                "See only the name and mobile of parents of students you teach this year, and your own parent record."),
            ("Attendance.ViewScoped", "Attendance",
                "See attendance of your own classes and subjects this year, and all attendance of your own children or yourself."),
        };

        /// <summary>The Teacher role's full reads that become scoped: (full, scoped).</summary>
        private static readonly (string Full, string Scoped)[] TeacherSwap =
        {
            ("Students.View", "Students.ViewScoped"),
            ("Parents.View", "Parents.ViewScoped"),
            ("Attendance.View", "Attendance.ViewScoped"),
        };

        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            // ── 1. New permissions ────────────────────────────────────────
            migrationBuilder.Sql(
                "MERGE [Permissions] AS t\n" +
                $"USING (VALUES\n{Values(Added.Select(a => new[] { a.Name, a.Module, a.Description }))}\n) AS s ([Name], [Module], [Description])\n" +
                "ON t.[Name] = s.[Name]\n" +
                "WHEN MATCHED THEN UPDATE SET t.[Module] = s.[Module], t.[Description] = s.[Description]\n" +
                "WHEN NOT MATCHED THEN INSERT ([Name], [Module], [Description]) VALUES (s.[Name], s.[Module], s.[Description]);");

            // ── 2. Teacher: full reads → scoped reads ─────────────────────
            migrationBuilder.Sql(GrantToRole("Teacher", TeacherSwap.Select(s => s.Scoped)));
            migrationBuilder.Sql(RevokeFromRole("Teacher", TeacherSwap.Select(s => s.Full)));

            // ── 3. Audit log accepts 'Read' ───────────────────────────────
            migrationBuilder.DropCheckConstraint(
                name: "CK_AuditLogs_Action",
                table: "AuditLogs");

            migrationBuilder.AddCheckConstraint(
                name: "CK_AuditLogs_Action",
                table: "AuditLogs",
                sql: "[Action] IN ('Insert','Update','Delete','Read')");
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            // ── 3. Audit log: 'Read' rows cannot survive the old constraint ─
            migrationBuilder.DropCheckConstraint(
                name: "CK_AuditLogs_Action",
                table: "AuditLogs");

            migrationBuilder.Sql("DELETE FROM [AuditLogs] WHERE [Action] = N'Read';");

            migrationBuilder.AddCheckConstraint(
                name: "CK_AuditLogs_Action",
                table: "AuditLogs",
                sql: "[Action] IN ('Insert','Update','Delete')");

            // ── 2. Teacher gets its full reads back ───────────────────────
            migrationBuilder.Sql(GrantToRole("Teacher", TeacherSwap.Select(s => s.Full)));

            // Custom roles that were given only a scoped read keep reading (with the full View, as before F1).
            migrationBuilder.Sql(
                "INSERT INTO [RolePermissions] ([RoleId], [PermissionId])\n" +
                "SELECT DISTINCT rp.[RoleId], np.[Id]\n" +
                "FROM [RolePermissions] rp\n" +
                "JOIN [Permissions] op ON op.[Id] = rp.[PermissionId]\n" +
                $"JOIN (VALUES\n{Values(TeacherSwap.Select(s => new[] { s.Scoped, s.Full }))}\n) AS m ([FromName], [ToName]) ON m.[FromName] = op.[Name]\n" +
                "JOIN [Permissions] np ON np.[Name] = m.[ToName]\n" +
                "WHERE NOT EXISTS (SELECT 1 FROM [RolePermissions] x WHERE x.[RoleId] = rp.[RoleId] AND x.[PermissionId] = np.[Id]);");

            // ── 1. New permissions go (their grants cascade) ──────────────
            migrationBuilder.Sql($"DELETE FROM [Permissions] WHERE [Name] IN ({InList(Added.Select(a => a.Name))});");
        }

        // ── SQL helpers (same as E1) ──────────────────────────────────────

        /// <summary>N'text' with quotes doubled.</summary>
        private static string Q(string value) => "N'" + value.Replace("'", "''") + "'";

        private static string InList(IEnumerable<string> values) => string.Join(", ", values.Select(Q));

        private static string Values(IEnumerable<string[]> rows) =>
            string.Join(",\n", rows.Select(r => "    (" + string.Join(", ", r.Select(Q)) + ")"));

        /// <summary>Grant each named permission to the named role, unless it already holds it.</summary>
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
