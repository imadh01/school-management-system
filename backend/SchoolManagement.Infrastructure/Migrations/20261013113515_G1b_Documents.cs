using System;
using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace SchoolManagement.Infrastructure.Migrations
{
    /// <summary>
    /// G1b: Documents (+ DocumentTypes) and the four Documents.* permissions. HAND-EDITED after
    /// scaffolding: the document types and permissions are seeded with SQL keyed by Code / Name (like
    /// E1/F1), so this is a frozen copy that never reads the C# constants. Do NOT regenerate.
    ///
    /// No role is granted the new permissions: Admin holds them in code, and other roles get them
    /// through the Roles page (decision: office staff only, chosen per school).
    ///
    /// Down drops both tables. It does NOT delete stored files: if documents were uploaded, empty
    /// the documents folder (Documents:StorageRoot) by hand after rolling back.
    /// </summary>
    public partial class G1b_Documents : Migration
    {
        /// <summary>(Code, Name, IsSensitive, HasExpiry, SortOrder) - the approved list (decision 3).</summary>
        private static readonly (string Code, string Name, bool Sensitive, bool Expiry, int Sort)[] Types =
        {
            ("BirthCertificate", "Birth certificate", true, false, 10),
            ("Passport", "Passport", true, true, 20),
            ("ResidenceVisa", "Residence visa", true, true, 30),
            ("EmiratesId", "Emirates ID", true, true, 40),
            ("Aadhaar", "Aadhaar", true, false, 50),
            ("Photo", "Photograph", false, false, 60),
            ("TransferCertificate", "Transfer certificate", false, false, 70),
            ("ReportCard", "Previous report card / marksheet", false, false, 80),
            ("MedicalRecord", "Medical / vaccination record", true, false, 90),
            ("Other", "Other", false, false, 100),
        };

        private static readonly (string Name, string Module, string Description)[] AddedPermissions =
        {
            ("Documents.View", "Documents",
                "See documents of students and applicants and download them (identity and medical files also need Students.ViewSensitive)."),
            ("Documents.Upload", "Documents", "Upload documents for students and applicants."),
            ("Documents.Verify", "Documents", "Verify, reject and unverify documents uploaded by someone else."),
            ("Documents.Delete", "Documents", "Delete documents that are not verified."),
        };

        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.CreateTable(
                name: "DocumentTypes",
                columns: table => new
                {
                    Id = table.Column<int>(type: "int", nullable: false)
                        .Annotation("SqlServer:Identity", "1, 1"),
                    Code = table.Column<string>(type: "nvarchar(40)", maxLength: 40, nullable: false),
                    Name = table.Column<string>(type: "nvarchar(80)", maxLength: 80, nullable: false),
                    IsSensitive = table.Column<bool>(type: "bit", nullable: false, defaultValue: false),
                    HasExpiry = table.Column<bool>(type: "bit", nullable: false, defaultValue: false),
                    ForStudents = table.Column<bool>(type: "bit", nullable: false, defaultValue: true),
                    ForStaff = table.Column<bool>(type: "bit", nullable: false, defaultValue: false),
                    SortOrder = table.Column<int>(type: "int", nullable: false, defaultValue: 0),
                    IsActive = table.Column<bool>(type: "bit", nullable: false, defaultValue: true)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_DocumentTypes", x => x.Id);
                    table.CheckConstraint("CK_DocumentTypes_CodeNotBlank", "LEN(LTRIM(RTRIM([Code]))) > 0");
                });

            migrationBuilder.CreateTable(
                name: "Documents",
                columns: table => new
                {
                    Id = table.Column<int>(type: "int", nullable: false)
                        .Annotation("SqlServer:Identity", "1, 1"),
                    StudentId = table.Column<int>(type: "int", nullable: true),
                    AdmissionId = table.Column<int>(type: "int", nullable: true),
                    DocumentTypeId = table.Column<int>(type: "int", nullable: false),
                    Title = table.Column<string>(type: "nvarchar(120)", maxLength: 120, nullable: true),
                    OriginalFileName = table.Column<string>(type: "nvarchar(255)", maxLength: 255, nullable: false),
                    ContentType = table.Column<string>(type: "nvarchar(50)", maxLength: 50, nullable: false),
                    SizeBytes = table.Column<int>(type: "int", nullable: false),
                    Sha256 = table.Column<byte[]>(type: "binary(32)", nullable: false),
                    StorageKey = table.Column<string>(type: "nvarchar(200)", maxLength: 200, nullable: false),
                    ExpiryDate = table.Column<DateOnly>(type: "date", nullable: true),
                    Status = table.Column<string>(type: "nvarchar(20)", maxLength: 20, nullable: false, defaultValue: "Pending"),
                    VerifiedBy = table.Column<int>(type: "int", nullable: true),
                    VerifiedAt = table.Column<DateTime>(type: "datetime2", nullable: true),
                    RejectionReason = table.Column<string>(type: "nvarchar(300)", maxLength: 300, nullable: true),
                    CreatedAt = table.Column<DateTime>(type: "datetime2", nullable: false, defaultValueSql: "SYSUTCDATETIME()"),
                    UpdatedAt = table.Column<DateTime>(type: "datetime2", nullable: false, defaultValueSql: "SYSUTCDATETIME()"),
                    CreatedBy = table.Column<int>(type: "int", nullable: true),
                    UpdatedBy = table.Column<int>(type: "int", nullable: true),
                    RowVersion = table.Column<byte[]>(type: "rowversion", rowVersion: true, nullable: false),
                    IsDeleted = table.Column<bool>(type: "bit", nullable: false, defaultValue: false),
                    DeletedAt = table.Column<DateTime>(type: "datetime2", nullable: true),
                    DeletedBy = table.Column<int>(type: "int", nullable: true),
                    FilePurgedAt = table.Column<DateTime>(type: "datetime2", nullable: true)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_Documents", x => x.Id);
                    table.CheckConstraint("CK_Documents_ContentType", "[ContentType] IN ('application/pdf','image/jpeg','image/png')");
                    table.CheckConstraint("CK_Documents_ExactlyOneOwner", "(CASE WHEN [StudentId] IS NULL THEN 0 ELSE 1 END) + (CASE WHEN [AdmissionId] IS NULL THEN 0 ELSE 1 END) = 1");
                    table.CheckConstraint("CK_Documents_NoSelfVerify", "[VerifiedBy] IS NULL OR [CreatedBy] IS NULL OR [VerifiedBy] <> [CreatedBy]");
                    table.CheckConstraint("CK_Documents_PendingHasNoReviewer", "[Status] <> 'Pending' OR ([VerifiedBy] IS NULL AND [VerifiedAt] IS NULL AND [RejectionReason] IS NULL)");
                    table.CheckConstraint("CK_Documents_RejectedHasReason", "[Status] <> 'Rejected' OR LEN(LTRIM(RTRIM([RejectionReason]))) > 0");
                    table.CheckConstraint("CK_Documents_ReviewedHasReviewer", "[Status] = 'Pending' OR ([VerifiedBy] IS NOT NULL AND [VerifiedAt] IS NOT NULL)");
                    table.CheckConstraint("CK_Documents_SizePositive", "[SizeBytes] > 0");
                    table.CheckConstraint("CK_Documents_Status", "[Status] IN ('Pending','Verified','Rejected')");
                    table.ForeignKey(
                        name: "FK_Documents_Admissions_AdmissionId",
                        column: x => x.AdmissionId,
                        principalTable: "Admissions",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Restrict);
                    table.ForeignKey(
                        name: "FK_Documents_DocumentTypes_DocumentTypeId",
                        column: x => x.DocumentTypeId,
                        principalTable: "DocumentTypes",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Restrict);
                    table.ForeignKey(
                        name: "FK_Documents_Students_StudentId",
                        column: x => x.StudentId,
                        principalTable: "Students",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Restrict);
                    table.ForeignKey(
                        name: "FK_Documents_Users_VerifiedBy",
                        column: x => x.VerifiedBy,
                        principalTable: "Users",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Restrict);
                });

            migrationBuilder.CreateIndex(
                name: "IX_Documents_AdmissionId",
                table: "Documents",
                column: "AdmissionId",
                filter: "[AdmissionId] IS NOT NULL AND [IsDeleted] = 0");

            migrationBuilder.CreateIndex(
                name: "IX_Documents_DocumentTypeId",
                table: "Documents",
                column: "DocumentTypeId");

            migrationBuilder.CreateIndex(
                name: "IX_Documents_ExpiryDate",
                table: "Documents",
                column: "ExpiryDate",
                filter: "[ExpiryDate] IS NOT NULL AND [IsDeleted] = 0");

            migrationBuilder.CreateIndex(
                name: "IX_Documents_PurgeQueue",
                table: "Documents",
                column: "DeletedAt",
                filter: "[IsDeleted] = 1 AND [FilePurgedAt] IS NULL");

            migrationBuilder.CreateIndex(
                name: "IX_Documents_Status_CreatedAt",
                table: "Documents",
                columns: new[] { "Status", "CreatedAt" },
                filter: "[IsDeleted] = 0");

            migrationBuilder.CreateIndex(
                name: "IX_Documents_StudentId",
                table: "Documents",
                column: "StudentId",
                filter: "[StudentId] IS NOT NULL AND [IsDeleted] = 0")
                .Annotation("SqlServer:Include", new[] { "DocumentTypeId", "Status" });

            migrationBuilder.CreateIndex(
                name: "IX_Documents_VerifiedBy",
                table: "Documents",
                column: "VerifiedBy");

            migrationBuilder.CreateIndex(
                name: "UX_Documents_Admission_Sha256",
                table: "Documents",
                columns: new[] { "AdmissionId", "Sha256" },
                unique: true,
                filter: "[AdmissionId] IS NOT NULL AND [IsDeleted] = 0");

            migrationBuilder.CreateIndex(
                name: "UX_Documents_StorageKey",
                table: "Documents",
                column: "StorageKey",
                unique: true);

            migrationBuilder.CreateIndex(
                name: "UX_Documents_Student_Sha256",
                table: "Documents",
                columns: new[] { "StudentId", "Sha256" },
                unique: true,
                filter: "[StudentId] IS NOT NULL AND [IsDeleted] = 0");

            migrationBuilder.CreateIndex(
                name: "UX_DocumentTypes_Code",
                table: "DocumentTypes",
                column: "Code",
                unique: true);

            // ── Seed: document types (keyed by Code) ──────────────────────
            migrationBuilder.Sql(
                "MERGE [DocumentTypes] AS t\n" +
                "USING (VALUES\n" +
                string.Join(",\n", Types.Select(x =>
                    $"    ({Q(x.Code)}, {Q(x.Name)}, {Bit(x.Sensitive)}, {Bit(x.Expiry)}, {x.Sort})")) +
                "\n) AS s ([Code], [Name], [IsSensitive], [HasExpiry], [SortOrder])\n" +
                "ON t.[Code] = s.[Code]\n" +
                "WHEN MATCHED THEN UPDATE SET t.[Name] = s.[Name], t.[IsSensitive] = s.[IsSensitive], t.[HasExpiry] = s.[HasExpiry], t.[SortOrder] = s.[SortOrder]\n" +
                "WHEN NOT MATCHED THEN INSERT ([Code], [Name], [IsSensitive], [HasExpiry], [ForStudents], [ForStaff], [SortOrder], [IsActive])\n" +
                "    VALUES (s.[Code], s.[Name], s.[IsSensitive], s.[HasExpiry], 1, 0, s.[SortOrder], 1);");

            // ── Seed: permissions (keyed by Name; no role grants) ─────────
            migrationBuilder.Sql(
                "MERGE [Permissions] AS t\n" +
                "USING (VALUES\n" +
                string.Join(",\n", AddedPermissions.Select(p => $"    ({Q(p.Name)}, {Q(p.Module)}, {Q(p.Description)})")) +
                "\n) AS s ([Name], [Module], [Description])\n" +
                "ON t.[Name] = s.[Name]\n" +
                "WHEN MATCHED THEN UPDATE SET t.[Module] = s.[Module], t.[Description] = s.[Description]\n" +
                "WHEN NOT MATCHED THEN INSERT ([Name], [Module], [Description]) VALUES (s.[Name], s.[Module], s.[Description]);");
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            // Grants of the new permissions (made on the Roles page) go with them (FK cascade).
            migrationBuilder.Sql(
                $"DELETE FROM [Permissions] WHERE [Name] IN ({string.Join(", ", AddedPermissions.Select(p => Q(p.Name)))});");

            migrationBuilder.DropTable(
                name: "Documents");

            migrationBuilder.DropTable(
                name: "DocumentTypes");
        }

        // ── SQL helpers ───────────────────────────────────────────────────

        /// <summary>N'text' with quotes doubled.</summary>
        private static string Q(string value) => "N'" + value.Replace("'", "''") + "'";

        private static string Bit(bool value) => value ? "1" : "0";
    }
}
