using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace SchoolManagement.Infrastructure.Migrations
{
    /// <summary>
    /// E2c: drops Teachers.Name and Teachers.Phone. Since E2a the staff profile (FullName, Mobile) is the one
    /// copy; the teacher's name and phone are read from it. HAND-EDITED after scaffolding (guard + Down SQL).
    /// Do NOT regenerate.
    ///
    /// Guard: the migration stops, changing nothing, if a teacher has no staff profile, or if the teacher's
    /// copy differs from the profile (it would be lost). Run database/scripts/e2c-precheck.sql first: it
    /// shows the counts and the rows.
    /// Down puts the two columns back and refills them from the profiles.
    /// Back up first: g1a-backup-before-migration.sql with @label = 'E2c'.
    /// </summary>
    public partial class E2c_DropTeacherNameAndPhone : Migration
    {
        /// <summary>Public so a test can run it against teachers it created.</summary>
        public const string GuardSql =
            "IF EXISTS (\n" +
            "    SELECT 1 FROM [Teachers] t\n" +
            "    LEFT JOIN [StaffProfiles] sp ON sp.[UserId] = t.[UserId] AND sp.[IsDeleted] = 0\n" +
            "    WHERE t.[IsDeleted] = 0\n" +
            "      AND (sp.[Id] IS NULL\n" +
            "           OR sp.[FullName] <> t.[Name]\n" +
            "           OR ISNULL(sp.[Mobile], N'') <> ISNULL(t.[Phone], N'')))\n" +
            "    THROW 50003, N'E2c stopped: a teacher has no staff profile, or the teacher name/phone differs from the profile. Run e2c-precheck.sql. Nothing was changed.', 1;";

        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.Sql(GuardSql);

            migrationBuilder.DropColumn(
                name: "Name",
                table: "Teachers");

            migrationBuilder.DropColumn(
                name: "Phone",
                table: "Teachers");
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.AddColumn<string>(
                name: "Name",
                table: "Teachers",
                type: "nvarchar(150)",
                maxLength: 150,
                nullable: false,
                defaultValue: "");

            migrationBuilder.AddColumn<string>(
                name: "Phone",
                table: "Teachers",
                type: "nvarchar(30)",
                maxLength: 30,
                nullable: true);

            // Refill from the profiles (the username where a teacher has none).
            migrationBuilder.Sql(
                "UPDATE t SET t.[Name] = COALESCE(sp.[FullName], u.[Username]), t.[Phone] = sp.[Mobile]\n" +
                "FROM [Teachers] t\n" +
                "JOIN [Users] u ON u.[Id] = t.[UserId]\n" +
                "LEFT JOIN [StaffProfiles] sp ON sp.[UserId] = t.[UserId];");
        }
    }
}
