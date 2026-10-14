using System;
using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace SchoolManagement.Infrastructure.Migrations
{
    /// <summary>
    /// E2a: StaffProfiles (the person behind each staff login), SchoolSettings (one row), Users.RowVersion,
    /// and staff as a fourth owner of Addresses. HAND-EDITED after scaffolding (seed, copy and guard SQL
    /// below). Do NOT regenerate.
    ///
    /// Copy (expand step, decision 2): one profile per non-deleted STAFF user (not a student or parent
    /// login): FullName = the teacher's name, else the username (flagged "needs name", decision 3);
    /// Mobile = the teacher's phone; JoiningDate = the date the account was created.
    /// Teachers.Name / Teachers.Phone stay and are kept in step until E2c drops them after a count check.
    /// Guard: the migration stops (and changes nothing) if any teacher ends up without a profile.
    ///
    /// SchoolSettings seed: PLACEHOLDERS (decision 5) until the real values are given.
    /// Down drops the new tables and column; staff addresses are deleted first (they have no other owner).
    /// Back up first: g1a-backup-before-migration.sql with @label = 'E2a'.
    /// </summary>
    public partial class E2a_StaffProfilesAndSchoolSettings : Migration
    {
        /// <summary>
        /// One profile per staff user that has none. Public so a test can run it against users it created
        /// (the migration ran when the test database was created). Safe to run twice.
        /// </summary>
        public const string CopyProfilesSql =
            "INSERT INTO [StaffProfiles] ([UserId], [FullName], [Mobile], [JoiningDate], [CreatedAt], [UpdatedAt], [IsDeleted])\n" +
            "SELECT u.[Id],\n" +
            "       COALESCE(NULLIF(LTRIM(RTRIM(t.[Name])), N''), u.[Username]),\n" +
            "       NULLIF(LTRIM(RTRIM(t.[Phone])), N''),\n" +
            "       CAST(u.[CreatedAt] AS date), SYSUTCDATETIME(), SYSUTCDATETIME(), 0\n" +
            "FROM [Users] u\n" +
            "LEFT JOIN [Teachers] t ON t.[UserId] = u.[Id] AND t.[IsDeleted] = 0\n" +
            "WHERE u.[IsDeleted] = 0\n" +
            "  AND NOT EXISTS (SELECT 1 FROM [StaffProfiles] sp WHERE sp.[UserId] = u.[Id] AND sp.[IsDeleted] = 0)\n" +
            "  AND NOT EXISTS (SELECT 1 FROM [Students] st WHERE st.[UserId] = u.[Id])\n" +
            "  AND NOT EXISTS (SELECT 1 FROM [Parents] pa WHERE pa.[UserId] = u.[Id])\n" +
            "  AND NOT EXISTS (SELECT 1 FROM [UserRoles] ur JOIN [Roles] r ON r.[Id] = ur.[RoleId]\n" +
            "                  WHERE ur.[UserId] = u.[Id] AND r.[Name] IN (N'Student', N'Parent'));";

        /// <summary>Stops the migration when a (not deleted) teacher has no profile after the copy.</summary>
        public const string GuardTeachersHaveProfilesSql =
            "IF EXISTS (\n" +
            "    SELECT 1 FROM [Teachers] t JOIN [Users] u ON u.[Id] = t.[UserId]\n" +
            "    WHERE t.[IsDeleted] = 0 AND u.[IsDeleted] = 0\n" +
            "      AND NOT EXISTS (SELECT 1 FROM [StaffProfiles] sp WHERE sp.[UserId] = t.[UserId] AND sp.[IsDeleted] = 0))\n" +
            "    THROW 50002, N'E2a stopped: a teacher has no staff profile after the copy. Nothing was changed.', 1;";

        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropCheckConstraint(
                name: "CK_Addresses_ExactlyOneOwner",
                table: "Addresses");

            // App-managed version (binary(8), not rowversion: a sign-in must not make an edit form stale).
            // Existing users start from a random value; new ones get theirs from the application.
            migrationBuilder.AddColumn<byte[]>(
                name: "RowVersion",
                table: "Users",
                type: "binary(8)",
                nullable: false,
                defaultValue: new byte[8]);

            migrationBuilder.Sql("UPDATE [Users] SET [RowVersion] = CAST(NEWID() AS binary(8));");

            migrationBuilder.AddColumn<int>(
                name: "StaffProfileId",
                table: "Addresses",
                type: "int",
                nullable: true);

            migrationBuilder.CreateTable(
                name: "SchoolSettings",
                columns: table => new
                {
                    Id = table.Column<int>(type: "int", nullable: false),
                    SchoolName = table.Column<string>(type: "nvarchar(150)", maxLength: 150, nullable: false),
                    ShortName = table.Column<string>(type: "nvarchar(30)", maxLength: 30, nullable: true),
                    Email = table.Column<string>(type: "nvarchar(100)", maxLength: 100, nullable: true),
                    Phone = table.Column<string>(type: "nvarchar(20)", maxLength: 20, nullable: true),
                    Website = table.Column<string>(type: "nvarchar(200)", maxLength: 200, nullable: true),
                    AddressText = table.Column<string>(type: "nvarchar(300)", maxLength: 300, nullable: true),
                    CountryId = table.Column<int>(type: "int", nullable: true),
                    TimeZoneId = table.Column<string>(type: "nvarchar(64)", maxLength: 64, nullable: false),
                    CurrencyCode = table.Column<string>(type: "nchar(3)", fixedLength: true, maxLength: 3, nullable: false),
                    WorkingDays = table.Column<byte>(type: "tinyint", nullable: false),
                    AboutTagline = table.Column<string>(type: "nvarchar(300)", maxLength: 300, nullable: true),
                    CreatedAt = table.Column<DateTime>(type: "datetime2", nullable: false, defaultValueSql: "SYSUTCDATETIME()"),
                    UpdatedAt = table.Column<DateTime>(type: "datetime2", nullable: false, defaultValueSql: "SYSUTCDATETIME()"),
                    CreatedBy = table.Column<int>(type: "int", nullable: true),
                    UpdatedBy = table.Column<int>(type: "int", nullable: true),
                    RowVersion = table.Column<byte[]>(type: "rowversion", rowVersion: true, nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_SchoolSettings", x => x.Id);
                    table.CheckConstraint("CK_SchoolSettings_CurrencyCode", "LEN([CurrencyCode]) = 3 AND [CurrencyCode] COLLATE Latin1_General_BIN2 NOT LIKE '%[^A-Z]%'");
                    table.CheckConstraint("CK_SchoolSettings_SchoolNameNotBlank", "LEN(LTRIM(RTRIM([SchoolName]))) > 0");
                    table.CheckConstraint("CK_SchoolSettings_SingleRow", "[Id] = 1");
                    table.CheckConstraint("CK_SchoolSettings_TimeZoneNotBlank", "LEN(LTRIM(RTRIM([TimeZoneId]))) > 0");
                    table.CheckConstraint("CK_SchoolSettings_WorkingDays", "[WorkingDays] BETWEEN 1 AND 127");
                    table.ForeignKey(
                        name: "FK_SchoolSettings_Countries_CountryId",
                        column: x => x.CountryId,
                        principalTable: "Countries",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Restrict);
                });

            migrationBuilder.CreateTable(
                name: "StaffProfiles",
                columns: table => new
                {
                    Id = table.Column<int>(type: "int", nullable: false)
                        .Annotation("SqlServer:Identity", "1, 1"),
                    UserId = table.Column<int>(type: "int", nullable: false),
                    FullName = table.Column<string>(type: "nvarchar(150)", maxLength: 150, nullable: false),
                    Gender = table.Column<string>(type: "nvarchar(10)", maxLength: 10, nullable: true),
                    DateOfBirth = table.Column<DateOnly>(type: "date", nullable: true),
                    Mobile = table.Column<string>(type: "nvarchar(30)", maxLength: 30, nullable: true),
                    JoiningDate = table.Column<DateOnly>(type: "date", nullable: true),
                    Qualification = table.Column<string>(type: "nvarchar(150)", maxLength: 150, nullable: true),
                    EmployeeNumber = table.Column<string>(type: "nvarchar(30)", maxLength: 30, nullable: true),
                    BloodGroup = table.Column<string>(type: "nvarchar(10)", maxLength: 10, nullable: true),
                    EmergencyContactName = table.Column<string>(type: "nvarchar(150)", maxLength: 150, nullable: true),
                    EmergencyContactPhone = table.Column<string>(type: "nvarchar(20)", maxLength: 20, nullable: true),
                    CreatedAt = table.Column<DateTime>(type: "datetime2", nullable: false, defaultValueSql: "SYSUTCDATETIME()"),
                    UpdatedAt = table.Column<DateTime>(type: "datetime2", nullable: false, defaultValueSql: "SYSUTCDATETIME()"),
                    CreatedBy = table.Column<int>(type: "int", nullable: true),
                    UpdatedBy = table.Column<int>(type: "int", nullable: true),
                    IsDeleted = table.Column<bool>(type: "bit", nullable: false, defaultValue: false),
                    DeletedAt = table.Column<DateTime>(type: "datetime2", nullable: true),
                    DeletedBy = table.Column<int>(type: "int", nullable: true)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_StaffProfiles", x => x.Id);
                    table.CheckConstraint("CK_StaffProfiles_EmployeeNumberNotBlank", "[EmployeeNumber] IS NULL OR LEN(LTRIM(RTRIM([EmployeeNumber]))) > 0");
                    table.CheckConstraint("CK_StaffProfiles_FullNameNotBlank", "LEN(LTRIM(RTRIM([FullName]))) > 0");
                    table.CheckConstraint("CK_StaffProfiles_Gender", "[Gender] IS NULL OR [Gender] IN ('Male','Female')");
                    table.ForeignKey(
                        name: "FK_StaffProfiles_Users_UserId",
                        column: x => x.UserId,
                        principalTable: "Users",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Restrict);
                });

            migrationBuilder.CreateIndex(
                name: "UX_Addresses_Staff_Primary",
                table: "Addresses",
                column: "StaffProfileId",
                unique: true,
                filter: "[StaffProfileId] IS NOT NULL AND [IsPrimary] = 1");

            migrationBuilder.CreateIndex(
                name: "UX_Addresses_Staff_Type",
                table: "Addresses",
                columns: new[] { "StaffProfileId", "AddressType" },
                unique: true,
                filter: "[StaffProfileId] IS NOT NULL");

            migrationBuilder.AddCheckConstraint(
                name: "CK_Addresses_ExactlyOneOwner",
                table: "Addresses",
                sql: "(CASE WHEN [StudentId] IS NULL THEN 0 ELSE 1 END) + (CASE WHEN [ParentId] IS NULL THEN 0 ELSE 1 END) + (CASE WHEN [AdmissionId] IS NULL THEN 0 ELSE 1 END) + (CASE WHEN [StaffProfileId] IS NULL THEN 0 ELSE 1 END) = 1");

            migrationBuilder.CreateIndex(
                name: "IX_SchoolSettings_CountryId",
                table: "SchoolSettings",
                column: "CountryId");

            migrationBuilder.CreateIndex(
                name: "UX_StaffProfiles_EmployeeNumber",
                table: "StaffProfiles",
                column: "EmployeeNumber",
                unique: true,
                filter: "[EmployeeNumber] IS NOT NULL AND [IsDeleted] = 0");

            migrationBuilder.CreateIndex(
                name: "UX_StaffProfiles_UserId",
                table: "StaffProfiles",
                column: "UserId",
                unique: true,
                filter: "[IsDeleted] = 0");

            migrationBuilder.AddForeignKey(
                name: "FK_Addresses_StaffProfiles_StaffProfileId",
                table: "Addresses",
                column: "StaffProfileId",
                principalTable: "StaffProfiles",
                principalColumn: "Id",
                onDelete: ReferentialAction.Cascade);

            // ── Seed: the one SchoolSettings row (placeholders, decision 5) ──
            migrationBuilder.Sql(
                "INSERT INTO [SchoolSettings] ([Id], [SchoolName], [TimeZoneId], [CurrencyCode], [WorkingDays], [CreatedAt], [UpdatedAt])\n" +
                "VALUES (1, N'School', N'Asia/Dubai', N'AED', 31, SYSUTCDATETIME(), SYSUTCDATETIME());"); // 31 = Mon-Fri

            // ── Copy: one profile per staff user, then check every teacher has one ──
            migrationBuilder.Sql(CopyProfilesSql);
            migrationBuilder.Sql(GuardTeachersHaveProfilesSql);
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            // Staff addresses have no other owner: they cannot survive the old three-owner CHECK.
            migrationBuilder.Sql("DELETE FROM [Addresses] WHERE [StaffProfileId] IS NOT NULL;");

            migrationBuilder.DropForeignKey(
                name: "FK_Addresses_StaffProfiles_StaffProfileId",
                table: "Addresses");

            migrationBuilder.DropTable(
                name: "SchoolSettings");

            migrationBuilder.DropTable(
                name: "StaffProfiles");

            migrationBuilder.DropIndex(
                name: "UX_Addresses_Staff_Primary",
                table: "Addresses");

            migrationBuilder.DropIndex(
                name: "UX_Addresses_Staff_Type",
                table: "Addresses");

            migrationBuilder.DropCheckConstraint(
                name: "CK_Addresses_ExactlyOneOwner",
                table: "Addresses");

            migrationBuilder.DropColumn(
                name: "RowVersion",
                table: "Users");

            migrationBuilder.DropColumn(
                name: "StaffProfileId",
                table: "Addresses");

            migrationBuilder.AddCheckConstraint(
                name: "CK_Addresses_ExactlyOneOwner",
                table: "Addresses",
                sql: "(CASE WHEN [StudentId] IS NULL THEN 0 ELSE 1 END) + (CASE WHEN [ParentId] IS NULL THEN 0 ELSE 1 END) + (CASE WHEN [AdmissionId] IS NULL THEN 0 ELSE 1 END) = 1");
        }
    }
}
