using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace SchoolManagement.Infrastructure.Migrations
{
    /// <inheritdoc />
    public partial class BatchB_RowVersion : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.AddColumn<byte[]>(
                name: "RowVersion",
                table: "Students",
                type: "rowversion",
                rowVersion: true,
                nullable: false,
                defaultValue: new byte[0]);

            migrationBuilder.AddColumn<byte[]>(
                name: "RowVersion",
                table: "Parents",
                type: "rowversion",
                rowVersion: true,
                nullable: false,
                defaultValue: new byte[0]);

            migrationBuilder.AddColumn<byte[]>(
                name: "RowVersion",
                table: "ClassSections",
                type: "rowversion",
                rowVersion: true,
                nullable: false,
                defaultValue: new byte[0]);

            migrationBuilder.AddColumn<byte[]>(
                name: "RowVersion",
                table: "Admissions",
                type: "rowversion",
                rowVersion: true,
                nullable: false,
                defaultValue: new byte[0]);
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropColumn(
                name: "RowVersion",
                table: "Students");

            migrationBuilder.DropColumn(
                name: "RowVersion",
                table: "Parents");

            migrationBuilder.DropColumn(
                name: "RowVersion",
                table: "ClassSections");

            migrationBuilder.DropColumn(
                name: "RowVersion",
                table: "Admissions");
        }
    }
}
