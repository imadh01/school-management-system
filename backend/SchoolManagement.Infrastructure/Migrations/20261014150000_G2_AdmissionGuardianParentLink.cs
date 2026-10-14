using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace SchoolManagement.Infrastructure.Migrations
{
    /// <summary>
    /// G2: lets an application point at an EXISTING parent. Adds the nullable AdmissionGuardians.ParentId, its
    /// index, a filtered unique index (one application cannot list the same parent twice) and the foreign key
    /// (Restrict). Nothing existing changes: every current guardian gets ParentId = NULL, which means
    /// "typed by hand", exactly how they behaved before. Down removes the column and both indexes.
    /// Back up first: g1a-backup-before-migration.sql with @label = 'G2'.
    /// </summary>
    public partial class G2_AdmissionGuardianParentLink : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.AddColumn<int>(
                name: "ParentId",
                table: "AdmissionGuardians",
                type: "int",
                nullable: true);

            migrationBuilder.CreateIndex(
                name: "IX_AdmissionGuardians_ParentId",
                table: "AdmissionGuardians",
                column: "ParentId");

            migrationBuilder.CreateIndex(
                name: "UX_AdmissionGuardians_Admission_Parent",
                table: "AdmissionGuardians",
                columns: new[] { "AdmissionId", "ParentId" },
                unique: true,
                filter: "[ParentId] IS NOT NULL");

            migrationBuilder.AddForeignKey(
                name: "FK_AdmissionGuardians_Parents_ParentId",
                table: "AdmissionGuardians",
                column: "ParentId",
                principalTable: "Parents",
                principalColumn: "Id",
                onDelete: ReferentialAction.Restrict);
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropForeignKey(
                name: "FK_AdmissionGuardians_Parents_ParentId",
                table: "AdmissionGuardians");

            migrationBuilder.DropIndex(
                name: "UX_AdmissionGuardians_Admission_Parent",
                table: "AdmissionGuardians");

            migrationBuilder.DropIndex(
                name: "IX_AdmissionGuardians_ParentId",
                table: "AdmissionGuardians");

            migrationBuilder.DropColumn(
                name: "ParentId",
                table: "AdmissionGuardians");
        }
    }
}
