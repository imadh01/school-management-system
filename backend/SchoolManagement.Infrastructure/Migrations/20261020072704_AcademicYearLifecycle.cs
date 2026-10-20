using System;
using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace SchoolManagement.Infrastructure.Migrations
{
    /// <inheritdoc />
    public partial class AcademicYearLifecycle : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropIndex(
                name: "IX_AcademicYears_Name",
                table: "AcademicYears");

            migrationBuilder.AddColumn<DateTime>(
                name: "ClosedAt",
                table: "AcademicYears",
                type: "datetime2",
                nullable: true);

            migrationBuilder.AddColumn<int>(
                name: "ClosedBy",
                table: "AcademicYears",
                type: "int",
                nullable: true);

            migrationBuilder.AddColumn<DateTime>(
                name: "DeletedAt",
                table: "AcademicYears",
                type: "datetime2",
                nullable: true);

            migrationBuilder.AddColumn<int>(
                name: "DeletedBy",
                table: "AcademicYears",
                type: "int",
                nullable: true);

            migrationBuilder.AddColumn<bool>(
                name: "IsDeleted",
                table: "AcademicYears",
                type: "bit",
                nullable: false,
                defaultValue: false);

            migrationBuilder.AddColumn<byte[]>(
                name: "RowVersion",
                table: "AcademicYears",
                type: "rowversion",
                rowVersion: true,
                nullable: false,
                defaultValue: new byte[0]);

            // Pre-check: the design needs exactly one current year. Stop before changing anything if not.
            migrationBuilder.Sql(
                "IF (SELECT COUNT(*) FROM AcademicYears WHERE IsCurrent = 1) <> 1 " +
                "THROW 50001, 'AcademicYearLifecycle: expected exactly one AcademicYears row with IsCurrent = 1.', 1;");

            // Map the old free-text Status (the seed used 'Active') to the new vocabulary BEFORE the CHECK
            // constraints below are created: the current row becomes Current, every other row Upcoming.
            migrationBuilder.Sql(
                "UPDATE AcademicYears SET Status = CASE WHEN IsCurrent = 1 THEN 'Current' ELSE 'Upcoming' END;");

            migrationBuilder.UpdateData(
                table: "AcademicYears",
                keyColumn: "Id",
                keyValue: 1,
                columns: new[] { "ClosedAt", "ClosedBy", "DeletedAt", "DeletedBy", "Status" },
                values: new object[] { null, null, null, null, "Current" });

            migrationBuilder.CreateIndex(
                name: "UX_AcademicYears_Name",
                table: "AcademicYears",
                column: "Name",
                unique: true,
                filter: "[IsDeleted] = 0");

            migrationBuilder.AddCheckConstraint(
                name: "CK_AcademicYears_CurrentMirror",
                table: "AcademicYears",
                sql: "([IsCurrent] = 1 AND [Status] = 'Current') OR ([IsCurrent] = 0 AND [Status] <> 'Current')");

            migrationBuilder.AddCheckConstraint(
                name: "CK_AcademicYears_Status",
                table: "AcademicYears",
                sql: "[Status] IN ('Upcoming','Current','Ended','Closed')");
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropIndex(
                name: "UX_AcademicYears_Name",
                table: "AcademicYears");

            migrationBuilder.DropCheckConstraint(
                name: "CK_AcademicYears_CurrentMirror",
                table: "AcademicYears");

            migrationBuilder.DropCheckConstraint(
                name: "CK_AcademicYears_Status",
                table: "AcademicYears");

            migrationBuilder.DropColumn(
                name: "ClosedAt",
                table: "AcademicYears");

            migrationBuilder.DropColumn(
                name: "ClosedBy",
                table: "AcademicYears");

            migrationBuilder.DropColumn(
                name: "DeletedAt",
                table: "AcademicYears");

            migrationBuilder.DropColumn(
                name: "DeletedBy",
                table: "AcademicYears");

            migrationBuilder.DropColumn(
                name: "IsDeleted",
                table: "AcademicYears");

            migrationBuilder.DropColumn(
                name: "RowVersion",
                table: "AcademicYears");

            // Back to the old vocabulary: the current row was 'Active', the others kept the default 'Upcoming'.
            // Ended and Closed did not exist before, so those rows become Upcoming. (The CHECKs are already dropped.)
            migrationBuilder.Sql(
                "UPDATE AcademicYears SET Status = CASE WHEN IsCurrent = 1 THEN 'Active' ELSE 'Upcoming' END;");

            migrationBuilder.CreateIndex(
                name: "IX_AcademicYears_Name",
                table: "AcademicYears",
                column: "Name",
                unique: true);
        }
    }
}
