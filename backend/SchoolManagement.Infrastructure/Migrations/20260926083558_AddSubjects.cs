using System;
using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace SchoolManagement.Infrastructure.Migrations
{
    /// <inheritdoc />
    public partial class AddSubjects : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.CreateTable(
                name: "Subjects",
                columns: table => new
                {
                    Id = table.Column<int>(type: "int", nullable: false)
                        .Annotation("SqlServer:Identity", "1, 1"),
                    Name = table.Column<string>(type: "nvarchar(100)", maxLength: 100, nullable: false),
                    Code = table.Column<string>(type: "nvarchar(20)", maxLength: 20, nullable: false),
                    ClassSectionId = table.Column<int>(type: "int", nullable: false),
                    Type = table.Column<string>(type: "nvarchar(20)", maxLength: 20, nullable: false),
                    MaxMarks = table.Column<int>(type: "int", nullable: true),
                    PassMarks = table.Column<int>(type: "int", nullable: true),
                    TheoryMax = table.Column<int>(type: "int", nullable: true),
                    TheoryPass = table.Column<int>(type: "int", nullable: true),
                    PracticalMax = table.Column<int>(type: "int", nullable: true),
                    PracticalPass = table.Column<int>(type: "int", nullable: true),
                    Status = table.Column<string>(type: "nvarchar(20)", maxLength: 20, nullable: false, defaultValue: "Active"),
                    CreatedAt = table.Column<DateTime>(type: "datetime2", nullable: false),
                    UpdatedAt = table.Column<DateTime>(type: "datetime2", nullable: false),
                    CreatedBy = table.Column<int>(type: "int", nullable: true),
                    UpdatedBy = table.Column<int>(type: "int", nullable: true),
                    IsDeleted = table.Column<bool>(type: "bit", nullable: false, defaultValue: false),
                    DeletedAt = table.Column<DateTime>(type: "datetime2", nullable: true),
                    DeletedBy = table.Column<int>(type: "int", nullable: true)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_Subjects", x => x.Id);
                    table.CheckConstraint("CK_Subjects_MarksByType", "([Type] IN ('Theory','Practical') AND [MaxMarks] IS NOT NULL AND [PassMarks] IS NOT NULL  AND [TheoryMax] IS NULL AND [TheoryPass] IS NULL AND [PracticalMax] IS NULL AND [PracticalPass] IS NULL) OR ([Type] = 'Both' AND [TheoryMax] IS NOT NULL AND [TheoryPass] IS NOT NULL  AND [PracticalMax] IS NOT NULL AND [PracticalPass] IS NOT NULL AND [MaxMarks] IS NULL AND [PassMarks] IS NULL)");
                    table.ForeignKey(
                        name: "FK_Subjects_ClassSections_ClassSectionId",
                        column: x => x.ClassSectionId,
                        principalTable: "ClassSections",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Restrict);
                });

            migrationBuilder.InsertData(
                table: "Permissions",
                columns: new[] { "Id", "Description", "Module", "Name" },
                values: new object[] { 6, "Create and edit subjects, including their marks configuration.", "Subjects", "Subjects.Manage" });

            migrationBuilder.InsertData(
                table: "RolePermissions",
                columns: new[] { "PermissionId", "RoleId" },
                values: new object[] { 6, 1 });

            migrationBuilder.CreateIndex(
                name: "IX_Subjects_ClassSectionId",
                table: "Subjects",
                column: "ClassSectionId");

            migrationBuilder.CreateIndex(
                name: "IX_Subjects_Code_ClassSectionId",
                table: "Subjects",
                columns: new[] { "Code", "ClassSectionId" },
                unique: true,
                filter: "[IsDeleted] = 0");
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropTable(
                name: "Subjects");

            migrationBuilder.DeleteData(
                table: "RolePermissions",
                keyColumns: new[] { "PermissionId", "RoleId" },
                keyValues: new object[] { 6, 1 });

            migrationBuilder.DeleteData(
                table: "Permissions",
                keyColumn: "Id",
                keyValue: 6);
        }
    }
}
