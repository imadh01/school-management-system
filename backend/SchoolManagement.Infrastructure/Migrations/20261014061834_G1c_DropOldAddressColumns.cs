using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace SchoolManagement.Infrastructure.Migrations
{
    /// <summary>
    /// G1c (contract step of G1): drops the old single-address columns AddressLine, City, State and
    /// Pincode from Students, Parents and Admissions. Since G1a nothing reads or writes them; the
    /// addresses live in [Addresses]. HAND-EDITED after scaffolding:
    ///   - Up first REFUSES to run (and changes nothing) if any row still has an old address that was
    ///     not copied into [Addresses] - the same test as database/scripts/g1a-address-copy-check.sql.
    ///     On the development database that check came back clean on 2026-10-13.
    ///   - Down re-creates the columns and fills them from each owner's PRIMARY address, so a rollback
    ///     gives the old screens their data back (shortened to the old column widths where needed).
    /// Back up before applying (database/scripts/g1a-backup-before-migration.sql, @label = 'G1c').
    /// </summary>
    public partial class G1c_DropOldAddressColumns : Migration
    {
        private static readonly (string Table, string OwnerColumn)[] Owners =
        {
            ("Students", "StudentId"),
            ("Parents", "ParentId"),
            ("Admissions", "AdmissionId"),
        };

        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            // Safety net: never drop an address that only exists in the old columns.
            foreach (var (table, owner) in Owners)
            {
                migrationBuilder.Sql(
                    "IF EXISTS (\n" +
                    $"    SELECT 1 FROM [{table}] o\n" +
                    "    WHERE (NULLIF(LTRIM(RTRIM(o.[AddressLine])), N'') IS NOT NULL OR NULLIF(LTRIM(RTRIM(o.[City])), N'') IS NOT NULL\n" +
                    "        OR NULLIF(LTRIM(RTRIM(o.[State])), N'') IS NOT NULL OR NULLIF(LTRIM(RTRIM(o.[Pincode])), N'') IS NOT NULL)\n" +
                    $"      AND NOT EXISTS (SELECT 1 FROM [Addresses] a WHERE a.[{owner}] = o.[Id]))\n" +
                    $"    THROW 50001, N'G1c stopped: some {table} rows have an old address that is not in [Addresses]. " +
                    "Run database/scripts/g1a-address-copy-check.sql, enter those addresses on their forms, then apply G1c again.', 1;");
            }

            migrationBuilder.DropColumn(
                name: "AddressLine",
                table: "Students");

            migrationBuilder.DropColumn(
                name: "City",
                table: "Students");

            migrationBuilder.DropColumn(
                name: "Pincode",
                table: "Students");

            migrationBuilder.DropColumn(
                name: "State",
                table: "Students");

            migrationBuilder.DropColumn(
                name: "AddressLine",
                table: "Parents");

            migrationBuilder.DropColumn(
                name: "City",
                table: "Parents");

            migrationBuilder.DropColumn(
                name: "Pincode",
                table: "Parents");

            migrationBuilder.DropColumn(
                name: "State",
                table: "Parents");

            migrationBuilder.DropColumn(
                name: "AddressLine",
                table: "Admissions");

            migrationBuilder.DropColumn(
                name: "City",
                table: "Admissions");

            migrationBuilder.DropColumn(
                name: "Pincode",
                table: "Admissions");

            migrationBuilder.DropColumn(
                name: "State",
                table: "Admissions");
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.AddColumn<string>(
                name: "AddressLine",
                table: "Students",
                type: "nvarchar(200)",
                maxLength: 200,
                nullable: true);

            migrationBuilder.AddColumn<string>(
                name: "City",
                table: "Students",
                type: "nvarchar(50)",
                maxLength: 50,
                nullable: true);

            migrationBuilder.AddColumn<string>(
                name: "Pincode",
                table: "Students",
                type: "nvarchar(10)",
                maxLength: 10,
                nullable: true);

            migrationBuilder.AddColumn<string>(
                name: "State",
                table: "Students",
                type: "nvarchar(50)",
                maxLength: 50,
                nullable: true);

            migrationBuilder.AddColumn<string>(
                name: "AddressLine",
                table: "Parents",
                type: "nvarchar(200)",
                maxLength: 200,
                nullable: true);

            migrationBuilder.AddColumn<string>(
                name: "City",
                table: "Parents",
                type: "nvarchar(50)",
                maxLength: 50,
                nullable: true);

            migrationBuilder.AddColumn<string>(
                name: "Pincode",
                table: "Parents",
                type: "nvarchar(10)",
                maxLength: 10,
                nullable: true);

            migrationBuilder.AddColumn<string>(
                name: "State",
                table: "Parents",
                type: "nvarchar(50)",
                maxLength: 50,
                nullable: true);

            migrationBuilder.AddColumn<string>(
                name: "AddressLine",
                table: "Admissions",
                type: "nvarchar(200)",
                maxLength: 200,
                nullable: true);

            migrationBuilder.AddColumn<string>(
                name: "City",
                table: "Admissions",
                type: "nvarchar(50)",
                maxLength: 50,
                nullable: true);

            migrationBuilder.AddColumn<string>(
                name: "Pincode",
                table: "Admissions",
                type: "nvarchar(10)",
                maxLength: 10,
                nullable: true);

            migrationBuilder.AddColumn<string>(
                name: "State",
                table: "Admissions",
                type: "nvarchar(50)",
                maxLength: 50,
                nullable: true);

            // Give the old columns their values back from each owner's primary address.
            foreach (var (table, owner) in Owners)
            {
                migrationBuilder.Sql(
                    "UPDATE o SET\n" +
                    "    o.[AddressLine] = LEFT(a.[Line1] + COALESCE(N', ' + a.[Line2], N''), 200),\n" +
                    "    o.[City] = LEFT(a.[City], 50),\n" +
                    "    o.[State] = LEFT(r.[Name], 50),\n" +
                    "    o.[Pincode] = LEFT(a.[PostalCode], 10)\n" +
                    $"FROM [{table}] o\n" +
                    $"JOIN [Addresses] a ON a.[{owner}] = o.[Id] AND a.[IsPrimary] = 1\n" +
                    "LEFT JOIN [CountryRegions] r ON r.[Id] = a.[RegionId];");
            }
        }
    }
}
