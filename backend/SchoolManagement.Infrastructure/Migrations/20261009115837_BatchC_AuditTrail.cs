using System;
using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace SchoolManagement.Infrastructure.Migrations
{
    /// <inheritdoc />
    public partial class BatchC_AuditTrail : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.AddColumn<int>(
                name: "CreatedBy",
                table: "StudentPickupPersons",
                type: "int",
                nullable: true);

            migrationBuilder.AddColumn<int>(
                name: "UpdatedBy",
                table: "StudentPickupPersons",
                type: "int",
                nullable: true);

            migrationBuilder.AddColumn<int>(
                name: "CreatedBy",
                table: "StudentIdentityDocuments",
                type: "int",
                nullable: true);

            migrationBuilder.AddColumn<int>(
                name: "UpdatedBy",
                table: "StudentIdentityDocuments",
                type: "int",
                nullable: true);

            migrationBuilder.AddColumn<int>(
                name: "CreatedBy",
                table: "StudentHealth",
                type: "int",
                nullable: true);

            migrationBuilder.AddColumn<int>(
                name: "UpdatedBy",
                table: "StudentHealth",
                type: "int",
                nullable: true);

            migrationBuilder.AddColumn<DateTime>(
                name: "CreatedAt",
                table: "StudentGuardians",
                type: "datetime2",
                nullable: false,
                defaultValueSql: "SYSUTCDATETIME()");

            migrationBuilder.AddColumn<int>(
                name: "CreatedBy",
                table: "StudentGuardians",
                type: "int",
                nullable: true);

            migrationBuilder.AddColumn<DateTime>(
                name: "UpdatedAt",
                table: "StudentGuardians",
                type: "datetime2",
                nullable: false,
                defaultValueSql: "SYSUTCDATETIME()");

            migrationBuilder.AddColumn<int>(
                name: "UpdatedBy",
                table: "StudentGuardians",
                type: "int",
                nullable: true);

            migrationBuilder.AddColumn<DateTime>(
                name: "CreatedAt",
                table: "AttendanceRecords",
                type: "datetime2",
                nullable: false,
                defaultValueSql: "SYSUTCDATETIME()");

            migrationBuilder.AddColumn<int>(
                name: "CreatedBy",
                table: "AttendanceRecords",
                type: "int",
                nullable: true);

            migrationBuilder.AddColumn<DateTime>(
                name: "UpdatedAt",
                table: "AttendanceRecords",
                type: "datetime2",
                nullable: false,
                defaultValueSql: "SYSUTCDATETIME()");

            migrationBuilder.AddColumn<int>(
                name: "UpdatedBy",
                table: "AttendanceRecords",
                type: "int",
                nullable: true);

            migrationBuilder.AddColumn<int>(
                name: "CreatedBy",
                table: "AdmissionGuardians",
                type: "int",
                nullable: true);

            migrationBuilder.AddColumn<int>(
                name: "UpdatedBy",
                table: "AdmissionGuardians",
                type: "int",
                nullable: true);

            migrationBuilder.CreateTable(
                name: "AuditLogs",
                columns: table => new
                {
                    Id = table.Column<long>(type: "bigint", nullable: false)
                        .Annotation("SqlServer:Identity", "1, 1"),
                    OccurredAt = table.Column<DateTime>(type: "datetime2", nullable: false),
                    UserId = table.Column<int>(type: "int", nullable: true),
                    UserName = table.Column<string>(type: "nvarchar(256)", maxLength: 256, nullable: true),
                    Action = table.Column<string>(type: "nvarchar(10)", maxLength: 10, nullable: false),
                    TableName = table.Column<string>(type: "nvarchar(100)", maxLength: 100, nullable: false),
                    RecordId = table.Column<string>(type: "nvarchar(100)", maxLength: 100, nullable: false),
                    Changes = table.Column<string>(type: "nvarchar(max)", nullable: true),
                    TraceId = table.Column<string>(type: "nvarchar(64)", maxLength: 64, nullable: true)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_AuditLogs", x => x.Id);
                    table.CheckConstraint("CK_AuditLogs_Action", "[Action] IN ('Insert','Update','Delete')");
                });

            migrationBuilder.CreateIndex(
                name: "IX_AuditLogs_OccurredAt",
                table: "AuditLogs",
                column: "OccurredAt");

            migrationBuilder.CreateIndex(
                name: "IX_AuditLogs_Table_Record",
                table: "AuditLogs",
                columns: new[] { "TableName", "RecordId", "OccurredAt" });

            migrationBuilder.CreateIndex(
                name: "IX_AuditLogs_User_Time",
                table: "AuditLogs",
                columns: new[] { "UserId", "OccurredAt" });
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropTable(
                name: "AuditLogs");

            migrationBuilder.DropColumn(
                name: "CreatedBy",
                table: "StudentPickupPersons");

            migrationBuilder.DropColumn(
                name: "UpdatedBy",
                table: "StudentPickupPersons");

            migrationBuilder.DropColumn(
                name: "CreatedBy",
                table: "StudentIdentityDocuments");

            migrationBuilder.DropColumn(
                name: "UpdatedBy",
                table: "StudentIdentityDocuments");

            migrationBuilder.DropColumn(
                name: "CreatedBy",
                table: "StudentHealth");

            migrationBuilder.DropColumn(
                name: "UpdatedBy",
                table: "StudentHealth");

            migrationBuilder.DropColumn(
                name: "CreatedAt",
                table: "StudentGuardians");

            migrationBuilder.DropColumn(
                name: "CreatedBy",
                table: "StudentGuardians");

            migrationBuilder.DropColumn(
                name: "UpdatedAt",
                table: "StudentGuardians");

            migrationBuilder.DropColumn(
                name: "UpdatedBy",
                table: "StudentGuardians");

            migrationBuilder.DropColumn(
                name: "CreatedAt",
                table: "AttendanceRecords");

            migrationBuilder.DropColumn(
                name: "CreatedBy",
                table: "AttendanceRecords");

            migrationBuilder.DropColumn(
                name: "UpdatedAt",
                table: "AttendanceRecords");

            migrationBuilder.DropColumn(
                name: "UpdatedBy",
                table: "AttendanceRecords");

            migrationBuilder.DropColumn(
                name: "CreatedBy",
                table: "AdmissionGuardians");

            migrationBuilder.DropColumn(
                name: "UpdatedBy",
                table: "AdmissionGuardians");
        }
    }
}
