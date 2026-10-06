using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace SchoolManagement.Infrastructure.Migrations
{
    /// <inheritdoc />
    public partial class ExpandClassSections : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropIndex(
                name: "IX_ClassSections_AcademicYearId_Name_Section",
                table: "ClassSections");

            migrationBuilder.AddColumn<string>(
                name: "Building",
                table: "ClassSections",
                type: "nvarchar(50)",
                maxLength: 50,
                nullable: true);

            migrationBuilder.AddColumn<int>(
                name: "Floor",
                table: "ClassSections",
                type: "int",
                nullable: true);

            migrationBuilder.AddColumn<string>(
                name: "Medium",
                table: "ClassSections",
                type: "nvarchar(20)",
                maxLength: 20,
                nullable: false,
                defaultValue: "English");

            migrationBuilder.AddColumn<string>(
                name: "Stage",
                table: "ClassSections",
                type: "nvarchar(20)",
                maxLength: 20,
                nullable: false,
                defaultValue: "Primary");

            migrationBuilder.AddColumn<string>(
                name: "Stream",
                table: "ClassSections",
                type: "nvarchar(20)",
                maxLength: 20,
                nullable: false,
                defaultValue: "General");

            migrationBuilder.InsertData(
                table: "Permissions",
                columns: new[] { "Id", "Description", "Module", "Name" },
                values: new object[] { 7, "Create, edit, activate/deactivate and delete class sections.", "ClassSections", "ClassSections.Manage" });

            migrationBuilder.InsertData(
                table: "RolePermissions",
                columns: new[] { "PermissionId", "RoleId" },
                values: new object[] { 7, 1 });

            migrationBuilder.CreateIndex(
                name: "IX_ClassSections_AcademicYearId_Name_Section",
                table: "ClassSections",
                columns: new[] { "AcademicYearId", "Name", "Section" },
                unique: true,
                filter: "[IsDeleted] = 0");

            migrationBuilder.AddCheckConstraint(
                name: "CK_ClassSections_Floor",
                table: "ClassSections",
                sql: "[Floor] IS NULL OR [Floor] >= 0");
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropIndex(
                name: "IX_ClassSections_AcademicYearId_Name_Section",
                table: "ClassSections");

            migrationBuilder.DropCheckConstraint(
                name: "CK_ClassSections_Floor",
                table: "ClassSections");

            migrationBuilder.DeleteData(
                table: "RolePermissions",
                keyColumns: new[] { "PermissionId", "RoleId" },
                keyValues: new object[] { 7, 1 });

            migrationBuilder.DeleteData(
                table: "Permissions",
                keyColumn: "Id",
                keyValue: 7);

            migrationBuilder.DropColumn(
                name: "Building",
                table: "ClassSections");

            migrationBuilder.DropColumn(
                name: "Floor",
                table: "ClassSections");

            migrationBuilder.DropColumn(
                name: "Medium",
                table: "ClassSections");

            migrationBuilder.DropColumn(
                name: "Stage",
                table: "ClassSections");

            migrationBuilder.DropColumn(
                name: "Stream",
                table: "ClassSections");

            migrationBuilder.CreateIndex(
                name: "IX_ClassSections_AcademicYearId_Name_Section",
                table: "ClassSections",
                columns: new[] { "AcademicYearId", "Name", "Section" },
                unique: true);
        }
    }
}
