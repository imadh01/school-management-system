using System;
using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace SchoolManagement.Infrastructure.Migrations
{
    /// <summary>
    /// Batch E1: per-module permission catalog, role flags, and role grants as runtime data.
    ///
    /// Hand-edited after scaffolding. EF generated DeleteData for every seeded Role, Permission and
    /// RolePermission, because those left HasData (they are runtime data now). Those deletes were
    /// removed: the rows stay, and this migration manages them with SQL keyed by NAME, never by Id.
    ///
    /// Everything below is a frozen copy of the E1 catalog and matrix. Do not read the C#
    /// constants here: a migration must produce the same database no matter what the code says later.
    ///
    /// Up:
    ///   1. Roles: IsSystem, IsActive, audit columns, RowVersion; flag the six system roles.
    ///   2. Permissions: insert the E1 catalog (MERGE by name).
    ///   3. Old X.Manage holders get the equivalent new actions (plus what those actions depend on).
    ///   4. Explicit View grants for Supervisor, Clerk, Teacher. Teacher deliberately gets no
    ///      Admissions.View (applicant religion, category and medical notes); Student/Parent get
    ///      nothing until F1 adds ownership checks.
    ///   5. Delete the old X.Manage permissions (their grants cascade).
    ///   6. Delete Admin's grant rows: Admin holds the whole catalog in code.
    /// Down reverses all of it, restoring the original Ids, descriptions and grants.
    /// </summary>
    public partial class E1_PermissionCatalogAndRoles : Migration
    {
        // ── Frozen E1 catalog: (Name, Module, Description) ───────────────
        private static readonly (string Name, string Module, string Description)[] Catalog =
        {
            ("Users.View", "Users", "See the list of users and their details."),
            ("Users.Create", "Users", "Create user accounts."),
            ("Users.Edit", "Users", "Edit user accounts and their role."),
            ("Users.Delete", "Users", "Delete (soft-delete) user accounts."),
            ("Users.ChangeStatus", "Users", "Activate, deactivate, suspend and unlock accounts. Ends the user's sessions."),
            ("Users.ResetPassword", "Users", "Set a temporary password that must be changed at next login."),
            ("Roles.View", "Roles", "See roles and the permissions they grant."),
            ("Roles.Create", "Roles", "Create custom roles."),
            ("Roles.Edit", "Roles", "Rename, change permissions of, and activate/deactivate roles."),
            ("Settings.View", "Settings", "See school settings."),
            ("Settings.Edit", "Settings", "Change school settings."),
            ("ClassSections.View", "ClassSections", "See classes and sections."),
            ("ClassSections.Create", "ClassSections", "Create class sections."),
            ("ClassSections.Edit", "ClassSections", "Edit and activate/deactivate class sections."),
            ("ClassSections.Delete", "ClassSections", "Delete class sections."),
            ("Subjects.View", "Subjects", "See subjects."),
            ("Subjects.Create", "Subjects", "Create subjects."),
            ("Subjects.Edit", "Subjects", "Edit subjects, including marks configuration."),
            ("Subjects.Delete", "Subjects", "Delete subjects."),
            ("Teachers.View", "Teachers", "See teachers and their assignments."),
            ("Teachers.Create", "Teachers", "Create teachers."),
            ("Teachers.Edit", "Teachers", "Edit and activate/deactivate teachers."),
            ("Teachers.Delete", "Teachers", "Delete teachers."),
            ("Teachers.Assign", "Teachers", "Assign subjects and class-teacher duty."),
            ("Admissions.View", "Admissions", "See admission applications."),
            ("Admissions.Create", "Admissions", "Register new applications."),
            ("Admissions.Edit", "Admissions", "Edit, confirm and reject applications."),
            ("Admissions.Enroll", "Admissions", "Enrol admitted applicants as students."),
            ("Admissions.Delete", "Admissions", "Delete applications."),
            ("Students.View", "Students", "See students, their enrolments and guardians."),
            ("Students.Create", "Students", "Create students."),
            ("Students.Edit", "Students", "Edit students."),
            ("Students.Delete", "Students", "Delete students."),
            ("Students.ViewSensitive", "Students", "See and edit full Aadhaar, passport and visa details."),
            ("Parents.View", "Parents", "See parents and their linked children."),
            ("Parents.Create", "Parents", "Create parents."),
            ("Parents.Edit", "Parents", "Edit parents and link/unlink them to students."),
            ("Parents.Delete", "Parents", "Delete parents."),
            ("Attendance.View", "Attendance", "See attendance rosters, histories and summaries."),
            ("Attendance.Mark", "Attendance", "Mark attendance for own class or subjects."),
            ("Attendance.Manage", "Attendance", "Mark and edit attendance for any class."),
        };

        // ── Old permission → new permissions for whoever held it (Up, step 3) ──
        private static readonly (string Old, string New)[] UpMapping =
        {
            ("ClassSections.Manage", "ClassSections.View"), ("ClassSections.Manage", "ClassSections.Create"),
            ("ClassSections.Manage", "ClassSections.Edit"), ("ClassSections.Manage", "ClassSections.Delete"),
            ("ClassSections.Create", "ClassSections.View"),

            ("Subjects.Manage", "Subjects.View"), ("Subjects.Manage", "Subjects.Create"),
            ("Subjects.Manage", "Subjects.Edit"), ("Subjects.Manage", "Subjects.Delete"),
            ("Subjects.Manage", "ClassSections.View"),

            ("Teachers.Manage", "Teachers.View"), ("Teachers.Manage", "Teachers.Create"),
            ("Teachers.Manage", "Teachers.Edit"), ("Teachers.Manage", "Teachers.Delete"),
            ("Teachers.Manage", "Teachers.Assign"),
            ("Teachers.Manage", "Subjects.View"), ("Teachers.Manage", "ClassSections.View"),

            ("Admissions.Manage", "Admissions.View"), ("Admissions.Manage", "Admissions.Create"),
            ("Admissions.Manage", "Admissions.Edit"), ("Admissions.Manage", "Admissions.Enroll"),
            ("Admissions.Manage", "Admissions.Delete"), ("Admissions.Manage", "ClassSections.View"),

            ("Students.Manage", "Students.View"), ("Students.Manage", "Students.Create"),
            ("Students.Manage", "Students.Edit"), ("Students.Manage", "Students.Delete"),
            ("Students.Manage", "ClassSections.View"),

            ("Parents.Manage", "Parents.View"), ("Parents.Manage", "Parents.Create"),
            ("Parents.Manage", "Parents.Edit"), ("Parents.Manage", "Parents.Delete"),
            ("Parents.Manage", "Students.View"),

            // Kept permissions: add what they now depend on (unlock moved to ChangeStatus).
            ("Users.Create", "Users.View"), ("Users.Create", "Users.ChangeStatus"), ("Users.Create", "Roles.View"),
            ("Attendance.Manage", "Attendance.View"), ("Attendance.Manage", "ClassSections.View"), ("Attendance.Manage", "Subjects.View"),
            ("Attendance.Mark", "Attendance.View"), ("Attendance.Mark", "ClassSections.View"), ("Attendance.Mark", "Subjects.View"),
            ("Students.ViewSensitive", "Students.View"),
        };

        // ── Explicit reads per system role (Up, step 4) ──
        private static readonly (string Role, string Permission)[] ViewGrants =
        {
            ("Supervisor", "ClassSections.View"), ("Supervisor", "Subjects.View"), ("Supervisor", "Teachers.View"),
            ("Supervisor", "Students.View"), ("Supervisor", "Parents.View"), ("Supervisor", "Admissions.View"),
            ("Supervisor", "Attendance.View"),

            ("Clerk", "ClassSections.View"), ("Clerk", "Subjects.View"), ("Clerk", "Teachers.View"),
            ("Clerk", "Students.View"), ("Clerk", "Parents.View"), ("Clerk", "Admissions.View"),
            ("Clerk", "Attendance.View"),

            // Teacher: no Admissions.View (deliberate behavior change, E1 decision D).
            ("Teacher", "ClassSections.View"), ("Teacher", "Subjects.View"), ("Teacher", "Teachers.View"),
            ("Teacher", "Students.View"), ("Teacher", "Parents.View"), ("Teacher", "Attendance.View"),
        };

        private static readonly string[] RemovedPermissions =
        {
            "ClassSections.Manage", "Subjects.Manage", "Teachers.Manage",
            "Admissions.Manage", "Students.Manage", "Parents.Manage",
        };

        private static readonly string[] SystemRoles = { "Admin", "Supervisor", "Clerk", "Teacher", "Student", "Parent" };

        // ── The pre-E1 state, for Down: (Id, Name, Module, Description) ──
        private static readonly (int Id, string Name, string Module, string Description)[] OldCatalog =
        {
            (1, "Users.Create", "Users", "Create new user accounts."),
            (2, "ClassSections.Create", "ClassSections", "Create new class sections."),
            (3, "Admissions.Manage", "Admissions", "Create, edit, and progress admission applications through the pipeline."),
            (4, "Students.Manage", "Students", "Create and edit student records, including converting enrolled admissions."),
            (5, "Parents.Manage", "Parents", "Create and edit parent records, and link/unlink guardians to students."),
            (6, "Subjects.Manage", "Subjects", "Create and edit subjects, including their marks configuration."),
            (7, "ClassSections.Manage", "ClassSections", "Create, edit, activate/deactivate and delete class sections."),
            (8, "Teachers.Manage", "Teachers", "Create, edit and delete teachers; assign subjects and class teachers."),
            (9, "Attendance.Manage", "Attendance", "Mark and edit attendance for any class."),
            (10, "Attendance.Mark", "Attendance", "Mark attendance for own class or subjects (checked in the service)."),
            (11, "Students.ViewSensitive", "Students", "View and edit full Aadhaar, passport and visa details of students."),
        };

        /// <summary>Admin's pre-E1 grant rows (permission Ids). Admin never held Attendance.Mark (10).</summary>
        private static readonly int[] OldAdminGrants = { 1, 2, 3, 4, 5, 6, 7, 8, 9, 11 };

        // ── New write permissions → old X.Manage, for roles created/edited after E1 (Down) ──
        private static readonly (string New, string Old)[] DownMapping =
        {
            ("ClassSections.Edit", "ClassSections.Manage"), ("ClassSections.Delete", "ClassSections.Manage"),
            ("Subjects.Create", "Subjects.Manage"), ("Subjects.Edit", "Subjects.Manage"), ("Subjects.Delete", "Subjects.Manage"),
            ("Teachers.Create", "Teachers.Manage"), ("Teachers.Edit", "Teachers.Manage"),
            ("Teachers.Delete", "Teachers.Manage"), ("Teachers.Assign", "Teachers.Manage"),
            ("Admissions.Create", "Admissions.Manage"), ("Admissions.Edit", "Admissions.Manage"),
            ("Admissions.Enroll", "Admissions.Manage"), ("Admissions.Delete", "Admissions.Manage"),
            ("Students.Create", "Students.Manage"), ("Students.Edit", "Students.Manage"), ("Students.Delete", "Students.Manage"),
            ("Parents.Create", "Parents.Manage"), ("Parents.Edit", "Parents.Manage"), ("Parents.Delete", "Parents.Manage"),
        };

        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            // ── 1. Roles: new columns ─────────────────────────────────────
            migrationBuilder.AddColumn<DateTime>(
                name: "CreatedAt",
                table: "Roles",
                type: "datetime2",
                nullable: false,
                defaultValue: new DateTime(1, 1, 1, 0, 0, 0, 0, DateTimeKind.Unspecified));

            migrationBuilder.AddColumn<int>(
                name: "CreatedBy",
                table: "Roles",
                type: "int",
                nullable: true);

            migrationBuilder.AddColumn<bool>(
                name: "IsActive",
                table: "Roles",
                type: "bit",
                nullable: false,
                defaultValue: true);

            migrationBuilder.AddColumn<bool>(
                name: "IsSystem",
                table: "Roles",
                type: "bit",
                nullable: false,
                defaultValue: false);

            migrationBuilder.AddColumn<byte[]>(
                name: "RowVersion",
                table: "Roles",
                type: "rowversion",
                rowVersion: true,
                nullable: false,
                defaultValue: new byte[0]);

            migrationBuilder.AddColumn<DateTime>(
                name: "UpdatedAt",
                table: "Roles",
                type: "datetime2",
                nullable: false,
                defaultValue: new DateTime(1, 1, 1, 0, 0, 0, 0, DateTimeKind.Unspecified));

            migrationBuilder.AddColumn<int>(
                name: "UpdatedBy",
                table: "Roles",
                type: "int",
                nullable: true);

            migrationBuilder.Sql(
                "UPDATE [Roles] SET [CreatedAt] = SYSUTCDATETIME(), [UpdatedAt] = SYSUTCDATETIME();\n" +
                $"UPDATE [Roles] SET [IsSystem] = 1 WHERE [Name] IN ({InList(SystemRoles)});");

            // ── 2. Permission catalog, keyed by name ──────────────────────
            migrationBuilder.Sql(
                "MERGE [Permissions] AS t\n" +
                $"USING (VALUES\n{Values(Catalog.Select(c => new[] { c.Name, c.Module, c.Description }))}\n) AS s ([Name], [Module], [Description])\n" +
                "ON t.[Name] = s.[Name]\n" +
                "WHEN MATCHED THEN UPDATE SET t.[Module] = s.[Module], t.[Description] = s.[Description]\n" +
                "WHEN NOT MATCHED THEN INSERT ([Name], [Module], [Description]) VALUES (s.[Name], s.[Module], s.[Description]);");

            // ── 3. Whoever held an old permission gets its replacements ───
            migrationBuilder.Sql(GrantByMapping(UpMapping.Select(m => new[] { m.Old, m.New })));

            // ── 4. Explicit reads per system role ─────────────────────────
            migrationBuilder.Sql(GrantByRoleName(ViewGrants.Select(g => new[] { g.Role, g.Permission })));

            // ── 5. Old permissions go (RolePermissions cascade) ───────────
            migrationBuilder.Sql($"DELETE FROM [Permissions] WHERE [Name] IN ({InList(RemovedPermissions)});");

            // ── 6. Admin holds everything in code: no grant rows ──────────
            migrationBuilder.Sql(
                "DELETE rp FROM [RolePermissions] rp JOIN [Roles] r ON r.[Id] = rp.[RoleId] WHERE r.[Name] = N'Admin';");
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            // ── Old permissions back, with their original Ids and texts ───
            var oldMissing = OldCatalog.Where(o => RemovedPermissions.Contains(o.Name));
            migrationBuilder.Sql(
                "SET IDENTITY_INSERT [Permissions] ON;\n" +
                string.Join("\n", oldMissing.Select(o =>
                    $"IF NOT EXISTS (SELECT 1 FROM [Permissions] WHERE [Name] = {Q(o.Name)}) " +
                    $"INSERT INTO [Permissions] ([Id], [Name], [Module], [Description]) VALUES ({o.Id}, {Q(o.Name)}, {Q(o.Module)}, {Q(o.Description)});")) +
                "\nSET IDENTITY_INSERT [Permissions] OFF;");

            migrationBuilder.Sql(string.Join("\n", OldCatalog
                .Where(o => !RemovedPermissions.Contains(o.Name))
                .Select(o => $"UPDATE [Permissions] SET [Module] = {Q(o.Module)}, [Description] = {Q(o.Description)} WHERE [Name] = {Q(o.Name)};")));

            // ── Roles holding new write actions get the old X.Manage back ─
            migrationBuilder.Sql(GrantByMapping(DownMapping.Select(m => new[] { m.New, m.Old })));

            // ── Admin's original grant rows ───────────────────────────────
            migrationBuilder.Sql(
                "INSERT INTO [RolePermissions] ([RoleId], [PermissionId])\n" +
                "SELECT r.[Id], p.[Id] FROM [Roles] r CROSS JOIN [Permissions] p\n" +
                $"WHERE r.[Name] = N'Admin' AND p.[Id] IN ({string.Join(", ", OldAdminGrants)})\n" +
                "AND NOT EXISTS (SELECT 1 FROM [RolePermissions] x WHERE x.[RoleId] = r.[Id] AND x.[PermissionId] = p.[Id]);");

            // ── New permissions go (their grants cascade) ─────────────────
            var oldNames = OldCatalog.Select(o => o.Name).ToHashSet();
            migrationBuilder.Sql(
                $"DELETE FROM [Permissions] WHERE [Name] IN ({InList(Catalog.Select(c => c.Name).Where(n => !oldNames.Contains(n)))});");

            // ── Roles: drop the E1 columns ────────────────────────────────
            migrationBuilder.DropColumn(
                name: "CreatedAt",
                table: "Roles");

            migrationBuilder.DropColumn(
                name: "CreatedBy",
                table: "Roles");

            migrationBuilder.DropColumn(
                name: "IsActive",
                table: "Roles");

            migrationBuilder.DropColumn(
                name: "IsSystem",
                table: "Roles");

            migrationBuilder.DropColumn(
                name: "RowVersion",
                table: "Roles");

            migrationBuilder.DropColumn(
                name: "UpdatedAt",
                table: "Roles");

            migrationBuilder.DropColumn(
                name: "UpdatedBy",
                table: "Roles");
        }

        // ── SQL helpers ───────────────────────────────────────────────────

        /// <summary>N'text' with quotes doubled.</summary>
        private static string Q(string value) => "N'" + value.Replace("'", "''") + "'";

        private static string InList(IEnumerable<string> values) => string.Join(", ", values.Select(Q));

        private static string Values(IEnumerable<string[]> rows) =>
            string.Join(",\n", rows.Select(r => "    (" + string.Join(", ", r.Select(Q)) + ")"));

        /// <summary>For each (from, to) pair: every role holding "from" also gets "to".</summary>
        private static string GrantByMapping(IEnumerable<string[]> pairs) =>
            "INSERT INTO [RolePermissions] ([RoleId], [PermissionId])\n" +
            "SELECT DISTINCT rp.[RoleId], np.[Id]\n" +
            "FROM [RolePermissions] rp\n" +
            "JOIN [Permissions] op ON op.[Id] = rp.[PermissionId]\n" +
            $"JOIN (VALUES\n{Values(pairs)}\n) AS m ([FromName], [ToName]) ON m.[FromName] = op.[Name]\n" +
            "JOIN [Permissions] np ON np.[Name] = m.[ToName]\n" +
            "WHERE NOT EXISTS (SELECT 1 FROM [RolePermissions] x WHERE x.[RoleId] = rp.[RoleId] AND x.[PermissionId] = np.[Id]);";

        /// <summary>For each (role name, permission name): grant it if both exist and it isn't granted yet.</summary>
        private static string GrantByRoleName(IEnumerable<string[]> pairs) =>
            "INSERT INTO [RolePermissions] ([RoleId], [PermissionId])\n" +
            "SELECT DISTINCT r.[Id], p.[Id]\n" +
            $"FROM (VALUES\n{Values(pairs)}\n) AS g ([RoleName], [PermissionName])\n" +
            "JOIN [Roles] r ON r.[Name] = g.[RoleName]\n" +
            "JOIN [Permissions] p ON p.[Name] = g.[PermissionName]\n" +
            "WHERE NOT EXISTS (SELECT 1 FROM [RolePermissions] x WHERE x.[RoleId] = r.[Id] AND x.[PermissionId] = p.[Id]);";
    }
}
