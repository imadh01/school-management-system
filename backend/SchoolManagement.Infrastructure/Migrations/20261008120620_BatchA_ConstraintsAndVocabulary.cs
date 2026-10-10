using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace SchoolManagement.Infrastructure.Migrations
{
    /// <inheritdoc />
    public partial class BatchA_ConstraintsAndVocabulary : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            // ---- Data fixes. They MUST run first: the new CHECK constraints and the narrower
            // ---- AdmissionType column would otherwise fail on old rows. ----
            migrationBuilder.Sql(@"UPDATE Admissions SET AdmissionType = 'New' WHERE AdmissionType = 'Fresh Admission';");
            migrationBuilder.Sql(@"UPDATE s SET s.AdmissionType = a.AdmissionType FROM Students s JOIN Admissions a ON a.Id = s.AdmissionId;");
            migrationBuilder.Sql(@"UPDATE Students SET AdmissionType = 'New' WHERE AdmissionType = 'Fresh Admission';");

            migrationBuilder.DropIndex(
                name: "IX_Users_Email",
                table: "Users");

            migrationBuilder.DropIndex(
                name: "IX_Users_Username",
                table: "Users");

            migrationBuilder.DropIndex(
                name: "IX_Teachers_UserId",
                table: "Teachers");

            migrationBuilder.DropIndex(
                name: "IX_Students_ClassSectionId_RollNumber",
                table: "Students");

            migrationBuilder.DropIndex(
                name: "IX_Students_UserId",
                table: "Students");

            migrationBuilder.DropIndex(
                name: "IX_Parents_UserId",
                table: "Parents");

            migrationBuilder.AlterColumn<string>(
                name: "Category",
                table: "Students",
                type: "nvarchar(50)",
                maxLength: 50,
                nullable: false,
                defaultValue: "General",
                oldClrType: typeof(string),
                oldType: "nvarchar(20)",
                oldMaxLength: 20,
                oldDefaultValue: "General");

            migrationBuilder.AlterColumn<string>(
                name: "AdmissionType",
                table: "Students",
                type: "nvarchar(20)",
                maxLength: 20,
                nullable: false,
                defaultValue: "New",
                oldClrType: typeof(string),
                oldType: "nvarchar(30)",
                oldMaxLength: 30,
                oldDefaultValue: "Fresh Admission");

            migrationBuilder.CreateIndex(
                name: "IX_Users_Email",
                table: "Users",
                column: "Email",
                unique: true,
                filter: "[IsDeleted] = 0");

            migrationBuilder.CreateIndex(
                name: "IX_Users_Username",
                table: "Users",
                column: "Username",
                unique: true,
                filter: "[IsDeleted] = 0");

            migrationBuilder.CreateIndex(
                name: "IX_Teachers_UserId",
                table: "Teachers",
                column: "UserId",
                unique: true,
                filter: "[IsDeleted] = 0");

            migrationBuilder.AddCheckConstraint(
                name: "CK_Subjects_PassNotAboveMax",
                table: "Subjects",
                sql: "([PassMarks] IS NULL OR [MaxMarks] IS NULL OR [PassMarks] <= [MaxMarks]) AND ([TheoryPass] IS NULL OR [TheoryMax] IS NULL OR [TheoryPass] <= [TheoryMax]) AND ([PracticalPass] IS NULL OR [PracticalMax] IS NULL OR [PracticalPass] <= [PracticalMax])");

            migrationBuilder.AddCheckConstraint(
                name: "CK_Subjects_Status",
                table: "Subjects",
                sql: "[Status] IN ('Active','Inactive')");

            migrationBuilder.CreateIndex(
                name: "IX_Students_ClassSectionId_Status",
                table: "Students",
                columns: new[] { "ClassSectionId", "Status" });

            migrationBuilder.CreateIndex(
                name: "UX_Students_UserId",
                table: "Students",
                column: "UserId",
                unique: true,
                filter: "[UserId] IS NOT NULL AND [IsDeleted] = 0");

            migrationBuilder.AddCheckConstraint(
                name: "CK_Students_AdmissionType",
                table: "Students",
                sql: "[AdmissionType] IN ('New','Transfer')");

            migrationBuilder.AddCheckConstraint(
                name: "CK_Students_FeeConcession",
                table: "Students",
                sql: "[FeeConcessionPercent] IS NULL OR ([FeeConcessionPercent] >= 0 AND [FeeConcessionPercent] <= 100)");

            migrationBuilder.AddCheckConstraint(
                name: "CK_Students_Status",
                table: "Students",
                sql: "[Status] IN ('Active','Inactive','Left')");

            migrationBuilder.CreateIndex(
                name: "UX_Parents_UserId",
                table: "Parents",
                column: "UserId",
                unique: true,
                filter: "[UserId] IS NOT NULL AND [IsDeleted] = 0");

            migrationBuilder.AddCheckConstraint(
                name: "CK_Parents_Status",
                table: "Parents",
                sql: "[Status] IN ('Active','Inactive')");

            migrationBuilder.AddCheckConstraint(
                name: "CK_ClassSections_Capacity",
                table: "ClassSections",
                sql: "[Capacity] IS NULL OR [Capacity] > 0");

            migrationBuilder.AddCheckConstraint(
                name: "CK_ClassSections_Status",
                table: "ClassSections",
                sql: "[Status] IN ('Active','Inactive')");

            migrationBuilder.AddCheckConstraint(
                name: "CK_Admissions_AdmissionFee",
                table: "Admissions",
                sql: "[AdmissionFee] IS NULL OR [AdmissionFee] >= 0");

            migrationBuilder.AddCheckConstraint(
                name: "CK_Admissions_AdmissionType",
                table: "Admissions",
                sql: "[AdmissionType] IN ('New','Transfer')");

            migrationBuilder.AddCheckConstraint(
                name: "CK_Admissions_EnrolledIsComplete",
                table: "Admissions",
                sql: "[Status] <> 'Enrolled' OR ([RollNumber] IS NOT NULL AND [AdmissionNumber] IS NOT NULL AND [AdmissionDate] IS NOT NULL AND [AllottedClassSectionId] IS NOT NULL)");

            migrationBuilder.AddCheckConstraint(
                name: "CK_Admissions_RejectedHasReason",
                table: "Admissions",
                sql: "[Status] <> 'Rejected' OR ([RejectionReason] IS NOT NULL AND LEN(LTRIM(RTRIM([RejectionReason]))) > 0)");

            migrationBuilder.AddCheckConstraint(
                name: "CK_Admissions_Status",
                table: "Admissions",
                sql: "[Status] IN ('Registered','Admitted','Enrolled','Rejected')");

            migrationBuilder.AddCheckConstraint(
                name: "CK_AcademicYears_Dates",
                table: "AcademicYears",
                sql: "[EndDate] > [StartDate]");
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropIndex(
                name: "IX_Users_Email",
                table: "Users");

            migrationBuilder.DropIndex(
                name: "IX_Users_Username",
                table: "Users");

            migrationBuilder.DropIndex(
                name: "IX_Teachers_UserId",
                table: "Teachers");

            migrationBuilder.DropCheckConstraint(
                name: "CK_Subjects_PassNotAboveMax",
                table: "Subjects");

            migrationBuilder.DropCheckConstraint(
                name: "CK_Subjects_Status",
                table: "Subjects");

            migrationBuilder.DropIndex(
                name: "IX_Students_ClassSectionId_Status",
                table: "Students");

            migrationBuilder.DropIndex(
                name: "UX_Students_UserId",
                table: "Students");

            migrationBuilder.DropCheckConstraint(
                name: "CK_Students_AdmissionType",
                table: "Students");

            migrationBuilder.DropCheckConstraint(
                name: "CK_Students_FeeConcession",
                table: "Students");

            migrationBuilder.DropCheckConstraint(
                name: "CK_Students_Status",
                table: "Students");

            migrationBuilder.DropIndex(
                name: "UX_Parents_UserId",
                table: "Parents");

            migrationBuilder.DropCheckConstraint(
                name: "CK_Parents_Status",
                table: "Parents");

            migrationBuilder.DropCheckConstraint(
                name: "CK_ClassSections_Capacity",
                table: "ClassSections");

            migrationBuilder.DropCheckConstraint(
                name: "CK_ClassSections_Status",
                table: "ClassSections");

            migrationBuilder.DropCheckConstraint(
                name: "CK_Admissions_AdmissionFee",
                table: "Admissions");

            migrationBuilder.DropCheckConstraint(
                name: "CK_Admissions_AdmissionType",
                table: "Admissions");

            migrationBuilder.DropCheckConstraint(
                name: "CK_Admissions_EnrolledIsComplete",
                table: "Admissions");

            migrationBuilder.DropCheckConstraint(
                name: "CK_Admissions_RejectedHasReason",
                table: "Admissions");

            migrationBuilder.DropCheckConstraint(
                name: "CK_Admissions_Status",
                table: "Admissions");

            migrationBuilder.DropCheckConstraint(
                name: "CK_AcademicYears_Dates",
                table: "AcademicYears");

            migrationBuilder.AlterColumn<string>(
                name: "Category",
                table: "Students",
                type: "nvarchar(20)",
                maxLength: 20,
                nullable: false,
                defaultValue: "General",
                oldClrType: typeof(string),
                oldType: "nvarchar(50)",
                oldMaxLength: 50,
                oldDefaultValue: "General");

            migrationBuilder.AlterColumn<string>(
                name: "AdmissionType",
                table: "Students",
                type: "nvarchar(30)",
                maxLength: 30,
                nullable: false,
                defaultValue: "Fresh Admission",
                oldClrType: typeof(string),
                oldType: "nvarchar(20)",
                oldMaxLength: 20,
                oldDefaultValue: "New");

            migrationBuilder.CreateIndex(
                name: "IX_Users_Email",
                table: "Users",
                column: "Email",
                unique: true);

            migrationBuilder.CreateIndex(
                name: "IX_Users_Username",
                table: "Users",
                column: "Username",
                unique: true);

            migrationBuilder.CreateIndex(
                name: "IX_Teachers_UserId",
                table: "Teachers",
                column: "UserId",
                unique: true);

            migrationBuilder.CreateIndex(
                name: "IX_Students_ClassSectionId_RollNumber",
                table: "Students",
                columns: new[] { "ClassSectionId", "RollNumber" },
                unique: true);

            migrationBuilder.CreateIndex(
                name: "IX_Students_UserId",
                table: "Students",
                column: "UserId");

            migrationBuilder.CreateIndex(
                name: "IX_Parents_UserId",
                table: "Parents",
                column: "UserId");

            // ---- Data fix for Down(): put the old vocabulary back. ----
            migrationBuilder.Sql(@"UPDATE Students SET AdmissionType = 'Fresh Admission' WHERE AdmissionType = 'New';");
        }
    }
}