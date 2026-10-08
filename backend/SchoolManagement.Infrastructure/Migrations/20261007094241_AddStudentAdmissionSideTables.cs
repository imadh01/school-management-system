using System;
using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

#pragma warning disable CA1814 // Prefer jagged arrays over multidimensional

namespace SchoolManagement.Infrastructure.Migrations
{
    /// <inheritdoc />
    public partial class AddStudentAdmissionSideTables : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.AddColumn<string>(
                name: "MobileKey",
                table: "Parents",
                type: "nvarchar(20)",
                maxLength: 20,
                nullable: false,
                computedColumnSql: "CAST(REPLACE(REPLACE(REPLACE(REPLACE(REPLACE(REPLACE([Mobile],' ',''),'-',''),'+',''),'(',''),')',''),'.','') AS nvarchar(20))",
                stored: true);

            migrationBuilder.CreateTable(
                name: "AdmissionGuardians",
                columns: table => new
                {
                    Id = table.Column<int>(type: "int", nullable: false)
                        .Annotation("SqlServer:Identity", "1, 1"),
                    AdmissionId = table.Column<int>(type: "int", nullable: false),
                    RelationType = table.Column<string>(type: "nvarchar(30)", maxLength: 30, nullable: false),
                    Name = table.Column<string>(type: "nvarchar(100)", maxLength: 100, nullable: false),
                    Mobile = table.Column<string>(type: "nvarchar(20)", maxLength: 20, nullable: true),
                    Email = table.Column<string>(type: "nvarchar(100)", maxLength: 100, nullable: true),
                    IsPrimaryContact = table.Column<bool>(type: "bit", nullable: false, defaultValue: false),
                    MobileKey = table.Column<string>(type: "nvarchar(20)", maxLength: 20, nullable: true, computedColumnSql: "CAST(REPLACE(REPLACE(REPLACE(REPLACE(REPLACE(REPLACE([Mobile],' ',''),'-',''),'+',''),'(',''),')',''),'.','') AS nvarchar(20))", stored: true),
                    CreatedAt = table.Column<DateTime>(type: "datetime2", nullable: false, defaultValueSql: "SYSUTCDATETIME()"),
                    UpdatedAt = table.Column<DateTime>(type: "datetime2", nullable: false, defaultValueSql: "SYSUTCDATETIME()")
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_AdmissionGuardians", x => x.Id);
                    table.CheckConstraint("CK_AdmissionGuardians_RelationType", "[RelationType] IN ('Father','Mother','Guardian')");
                    table.ForeignKey(
                        name: "FK_AdmissionGuardians_Admissions_AdmissionId",
                        column: x => x.AdmissionId,
                        principalTable: "Admissions",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Cascade);
                });

            migrationBuilder.CreateTable(
                name: "StudentHealth",
                columns: table => new
                {
                    StudentId = table.Column<int>(type: "int", nullable: false),
                    BloodGroup = table.Column<string>(type: "nvarchar(10)", maxLength: 10, nullable: true),
                    Allergies = table.Column<string>(type: "nvarchar(300)", maxLength: 300, nullable: true),
                    DietaryRequirements = table.Column<string>(type: "nvarchar(200)", maxLength: 200, nullable: true),
                    MedicalNotes = table.Column<string>(type: "nvarchar(500)", maxLength: 500, nullable: true),
                    SpecialEducationalNeeds = table.Column<string>(type: "nvarchar(500)", maxLength: 500, nullable: true),
                    InsuranceProvider = table.Column<string>(type: "nvarchar(100)", maxLength: 100, nullable: true),
                    InsurancePolicyExpiry = table.Column<DateOnly>(type: "date", nullable: true),
                    CreatedAt = table.Column<DateTime>(type: "datetime2", nullable: false, defaultValueSql: "SYSUTCDATETIME()"),
                    UpdatedAt = table.Column<DateTime>(type: "datetime2", nullable: false, defaultValueSql: "SYSUTCDATETIME()")
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_StudentHealth", x => x.StudentId);
                    table.ForeignKey(
                        name: "FK_StudentHealth_Students_StudentId",
                        column: x => x.StudentId,
                        principalTable: "Students",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Cascade);
                });

            migrationBuilder.CreateTable(
                name: "StudentIdentityDocuments",
                columns: table => new
                {
                    StudentId = table.Column<int>(type: "int", nullable: false),
                    AadhaarNumber = table.Column<string>(type: "nvarchar(12)", maxLength: 12, nullable: true),
                    PassportNumber = table.Column<string>(type: "nvarchar(30)", maxLength: 30, nullable: true),
                    PassportExpiry = table.Column<DateOnly>(type: "date", nullable: true),
                    VisaType = table.Column<string>(type: "nvarchar(30)", maxLength: 30, nullable: true),
                    VisaExpiry = table.Column<DateOnly>(type: "date", nullable: true),
                    CreatedAt = table.Column<DateTime>(type: "datetime2", nullable: false, defaultValueSql: "SYSUTCDATETIME()"),
                    UpdatedAt = table.Column<DateTime>(type: "datetime2", nullable: false, defaultValueSql: "SYSUTCDATETIME()")
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_StudentIdentityDocuments", x => x.StudentId);
                    table.CheckConstraint("CK_StudentIdentityDocuments_Aadhaar", "[AadhaarNumber] IS NULL OR (LEN([AadhaarNumber]) = 12 AND [AadhaarNumber] NOT LIKE '%[^0-9]%')");
                    table.ForeignKey(
                        name: "FK_StudentIdentityDocuments_Students_StudentId",
                        column: x => x.StudentId,
                        principalTable: "Students",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Cascade);
                });

            migrationBuilder.CreateTable(
                name: "StudentPickupPersons",
                columns: table => new
                {
                    Id = table.Column<int>(type: "int", nullable: false)
                        .Annotation("SqlServer:Identity", "1, 1"),
                    StudentId = table.Column<int>(type: "int", nullable: false),
                    Name = table.Column<string>(type: "nvarchar(100)", maxLength: 100, nullable: false),
                    Relation = table.Column<string>(type: "nvarchar(50)", maxLength: 50, nullable: false),
                    Phone = table.Column<string>(type: "nvarchar(20)", maxLength: 20, nullable: false),
                    IdNote = table.Column<string>(type: "nvarchar(100)", maxLength: 100, nullable: true),
                    CreatedAt = table.Column<DateTime>(type: "datetime2", nullable: false, defaultValueSql: "SYSUTCDATETIME()"),
                    UpdatedAt = table.Column<DateTime>(type: "datetime2", nullable: false, defaultValueSql: "SYSUTCDATETIME()")
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_StudentPickupPersons", x => x.Id);
                    table.ForeignKey(
                        name: "FK_StudentPickupPersons_Students_StudentId",
                        column: x => x.StudentId,
                        principalTable: "Students",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Cascade);
                });

            migrationBuilder.InsertData(
                table: "Permissions",
                columns: new[] { "Id", "Description", "Module", "Name" },
                values: new object[] { 11, "View and edit full Aadhaar, passport and visa details of students.", "Students", "Students.ViewSensitive" });

            migrationBuilder.InsertData(
                table: "RolePermissions",
                columns: new[] { "PermissionId", "RoleId" },
                values: new object[,]
                {
                    { 11, 1 },
                    { 11, 2 }
                });

            migrationBuilder.CreateIndex(
                name: "UX_StudentGuardians_Student_Primary",
                table: "StudentGuardians",
                column: "StudentId",
                unique: true,
                filter: "[IsPrimaryContact] = 1");

            migrationBuilder.AddCheckConstraint(
                name: "CK_StudentGuardians_RelationType",
                table: "StudentGuardians",
                sql: "[RelationType] IN ('Father','Mother','Guardian')");

            migrationBuilder.CreateIndex(
                name: "IX_Parents_MobileKey",
                table: "Parents",
                column: "MobileKey");

            migrationBuilder.CreateIndex(
                name: "UX_AdmissionGuardians_Admission_Primary",
                table: "AdmissionGuardians",
                column: "AdmissionId",
                unique: true,
                filter: "[IsPrimaryContact] = 1");

            migrationBuilder.CreateIndex(
                name: "UX_StudentIdentityDocuments_Aadhaar",
                table: "StudentIdentityDocuments",
                column: "AadhaarNumber",
                unique: true,
                filter: "[AadhaarNumber] IS NOT NULL");

            migrationBuilder.CreateIndex(
                name: "IX_StudentPickupPersons_StudentId",
                table: "StudentPickupPersons",
                column: "StudentId");
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropTable(
                name: "AdmissionGuardians");

            migrationBuilder.DropTable(
                name: "StudentHealth");

            migrationBuilder.DropTable(
                name: "StudentIdentityDocuments");

            migrationBuilder.DropTable(
                name: "StudentPickupPersons");

            migrationBuilder.DropIndex(
                name: "UX_StudentGuardians_Student_Primary",
                table: "StudentGuardians");

            migrationBuilder.DropCheckConstraint(
                name: "CK_StudentGuardians_RelationType",
                table: "StudentGuardians");

            migrationBuilder.DropIndex(
                name: "IX_Parents_MobileKey",
                table: "Parents");

            migrationBuilder.DeleteData(
                table: "RolePermissions",
                keyColumns: new[] { "PermissionId", "RoleId" },
                keyValues: new object[] { 11, 1 });

            migrationBuilder.DeleteData(
                table: "RolePermissions",
                keyColumns: new[] { "PermissionId", "RoleId" },
                keyValues: new object[] { 11, 2 });

            migrationBuilder.DeleteData(
                table: "Permissions",
                keyColumn: "Id",
                keyValue: 11);

            migrationBuilder.DropColumn(
                name: "MobileKey",
                table: "Parents");
        }
    }
}
