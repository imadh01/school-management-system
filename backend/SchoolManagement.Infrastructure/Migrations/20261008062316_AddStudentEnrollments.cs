using System;
using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace SchoolManagement.Infrastructure.Migrations
{
    /// <inheritdoc />
    public partial class AddStudentEnrollments : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.CreateTable(
                name: "StudentEnrollments",
                columns: table => new
                {
                    Id = table.Column<int>(type: "int", nullable: false)
                        .Annotation("SqlServer:Identity", "1, 1"),
                    StudentId = table.Column<int>(type: "int", nullable: false),
                    AcademicYearId = table.Column<int>(type: "int", nullable: false),
                    ClassSectionId = table.Column<int>(type: "int", nullable: false),
                    RollNumber = table.Column<string>(type: "nvarchar(20)", maxLength: 20, nullable: false),
                    StartDate = table.Column<DateOnly>(type: "date", nullable: false),
                    EndDate = table.Column<DateOnly>(type: "date", nullable: true),
                    Status = table.Column<string>(type: "nvarchar(20)", maxLength: 20, nullable: false, defaultValue: "Active"),
                    Remarks = table.Column<string>(type: "nvarchar(300)", maxLength: 300, nullable: true),
                    CreatedAt = table.Column<DateTime>(type: "datetime2", nullable: false, defaultValueSql: "SYSUTCDATETIME()"),
                    UpdatedAt = table.Column<DateTime>(type: "datetime2", nullable: false, defaultValueSql: "SYSUTCDATETIME()"),
                    CreatedBy = table.Column<int>(type: "int", nullable: true),
                    UpdatedBy = table.Column<int>(type: "int", nullable: true)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_StudentEnrollments", x => x.Id);
                    table.CheckConstraint("CK_StudentEnrollments_EndDate", "([Status] = 'Active' AND [EndDate] IS NULL) OR ([Status] <> 'Active' AND [EndDate] IS NOT NULL AND [EndDate] >= [StartDate])");
                    table.CheckConstraint("CK_StudentEnrollments_Status", "[Status] IN ('Active','Promoted','Repeated','Transferred','Left')");
                    table.ForeignKey(
                        name: "FK_StudentEnrollments_AcademicYears_AcademicYearId",
                        column: x => x.AcademicYearId,
                        principalTable: "AcademicYears",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Restrict);
                    table.ForeignKey(
                        name: "FK_StudentEnrollments_ClassSections_ClassSectionId",
                        column: x => x.ClassSectionId,
                        principalTable: "ClassSections",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Restrict);
                    table.ForeignKey(
                        name: "FK_StudentEnrollments_Students_StudentId",
                        column: x => x.StudentId,
                        principalTable: "Students",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Restrict);
                });

            migrationBuilder.CreateIndex(
                name: "IX_StudentEnrollments_AcademicYearId",
                table: "StudentEnrollments",
                column: "AcademicYearId");

            migrationBuilder.CreateIndex(
                name: "IX_StudentEnrollments_Section_Status",
                table: "StudentEnrollments",
                columns: new[] { "ClassSectionId", "Status" });

            migrationBuilder.CreateIndex(
                name: "IX_StudentEnrollments_Student_Year",
                table: "StudentEnrollments",
                columns: new[] { "StudentId", "AcademicYearId" });

            migrationBuilder.CreateIndex(
                name: "UX_StudentEnrollments_Section_Roll_Active",
                table: "StudentEnrollments",
                columns: new[] { "ClassSectionId", "RollNumber" },
                unique: true,
                filter: "[Status] = 'Active'");

            migrationBuilder.CreateIndex(
                name: "UX_StudentEnrollments_Student_Active",
                table: "StudentEnrollments",
                column: "StudentId",
                unique: true,
                filter: "[Status] = 'Active'");

            // Backfill: every existing (non-deleted) student gets one enrollment built from their
            // current class. Students who already left get a closed period; everyone else an open one.
            migrationBuilder.Sql(@"
                            INSERT INTO [StudentEnrollments]
                                ([StudentId], [AcademicYearId], [ClassSectionId], [RollNumber], [StartDate], [EndDate], [Status])
                            SELECT
                                s.[Id], cs.[AcademicYearId], s.[ClassSectionId], s.[RollNumber], s.[AdmissionDate],
                                CASE WHEN s.[Status] = 'Left'
                                     THEN CASE WHEN s.[AdmissionDate] > CAST(SYSUTCDATETIME() AS date)
                                               THEN s.[AdmissionDate] ELSE CAST(SYSUTCDATETIME() AS date) END
                                     ELSE NULL END,
                                CASE WHEN s.[Status] = 'Left' THEN 'Left' ELSE 'Active' END
                            FROM [Students] s
                            JOIN [ClassSections] cs ON cs.[Id] = s.[ClassSectionId]
                            WHERE s.[IsDeleted] = 0;
                            ");
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropTable(
                name: "StudentEnrollments");
        }
    }
}