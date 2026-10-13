using System;
using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace SchoolManagement.Infrastructure.Migrations
{
    /// <summary>
    /// G1a: Addresses (+ Countries, CountryRegions). HAND-EDITED after scaffolding: seed data and the copy of
    /// the old address columns are written as SQL below. Do NOT regenerate. Back up the database first
    /// (database/scripts/g1a-backup-before-migration.sql). Down drops the new tables; the old columns
    /// were never changed, so nothing is lost.
    /// </summary>
    public partial class G1a_Addresses : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.CreateTable(
                name: "Countries",
                columns: table => new
                {
                    Id = table.Column<int>(type: "int", nullable: false)
                        .Annotation("SqlServer:Identity", "1, 1"),
                    Code = table.Column<string>(type: "nchar(2)", fixedLength: true, maxLength: 2, nullable: false),
                    Name = table.Column<string>(type: "nvarchar(60)", maxLength: 60, nullable: false),
                    RegionLabel = table.Column<string>(type: "nvarchar(30)", maxLength: 30, nullable: false),
                    PostalCodeRequired = table.Column<bool>(type: "bit", nullable: false, defaultValue: false),
                    PostalCodePattern = table.Column<string>(type: "nvarchar(100)", maxLength: 100, nullable: true),
                    PostalCodeHint = table.Column<string>(type: "nvarchar(60)", maxLength: 60, nullable: true),
                    SortOrder = table.Column<int>(type: "int", nullable: false, defaultValue: 0),
                    IsActive = table.Column<bool>(type: "bit", nullable: false, defaultValue: true)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_Countries", x => x.Id);
                    table.CheckConstraint("CK_Countries_Code", "LEN([Code]) = 2 AND [Code] = UPPER([Code])");
                });

            migrationBuilder.CreateTable(
                name: "CountryRegions",
                columns: table => new
                {
                    Id = table.Column<int>(type: "int", nullable: false)
                        .Annotation("SqlServer:Identity", "1, 1"),
                    CountryId = table.Column<int>(type: "int", nullable: false),
                    Name = table.Column<string>(type: "nvarchar(60)", maxLength: 60, nullable: false),
                    SortOrder = table.Column<int>(type: "int", nullable: false, defaultValue: 0),
                    IsActive = table.Column<bool>(type: "bit", nullable: false, defaultValue: true)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_CountryRegions", x => x.Id);
                    table.UniqueConstraint("AK_CountryRegions_CountryId_Id", x => new { x.CountryId, x.Id });
                    table.ForeignKey(
                        name: "FK_CountryRegions_Countries_CountryId",
                        column: x => x.CountryId,
                        principalTable: "Countries",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Restrict);
                });

            migrationBuilder.CreateTable(
                name: "Addresses",
                columns: table => new
                {
                    Id = table.Column<int>(type: "int", nullable: false)
                        .Annotation("SqlServer:Identity", "1, 1"),
                    StudentId = table.Column<int>(type: "int", nullable: true),
                    ParentId = table.Column<int>(type: "int", nullable: true),
                    AdmissionId = table.Column<int>(type: "int", nullable: true),
                    AddressType = table.Column<string>(type: "nvarchar(20)", maxLength: 20, nullable: false),
                    IsPrimary = table.Column<bool>(type: "bit", nullable: false, defaultValue: false),
                    Line1 = table.Column<string>(type: "nvarchar(200)", maxLength: 200, nullable: false),
                    Line2 = table.Column<string>(type: "nvarchar(200)", maxLength: 200, nullable: true),
                    City = table.Column<string>(type: "nvarchar(60)", maxLength: 60, nullable: false),
                    PostalCode = table.Column<string>(type: "nvarchar(12)", maxLength: 12, nullable: true),
                    CountryId = table.Column<int>(type: "int", nullable: false),
                    RegionId = table.Column<int>(type: "int", nullable: true),
                    CreatedAt = table.Column<DateTime>(type: "datetime2", nullable: false, defaultValueSql: "SYSUTCDATETIME()"),
                    UpdatedAt = table.Column<DateTime>(type: "datetime2", nullable: false, defaultValueSql: "SYSUTCDATETIME()"),
                    CreatedBy = table.Column<int>(type: "int", nullable: true),
                    UpdatedBy = table.Column<int>(type: "int", nullable: true)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_Addresses", x => x.Id);
                    table.CheckConstraint("CK_Addresses_AddressType", "[AddressType] IN ('Home','Permanent','Mailing')");
                    table.CheckConstraint("CK_Addresses_CityNotBlank", "LEN(LTRIM(RTRIM([City]))) > 0");
                    table.CheckConstraint("CK_Addresses_ExactlyOneOwner", "(CASE WHEN [StudentId] IS NULL THEN 0 ELSE 1 END) + (CASE WHEN [ParentId] IS NULL THEN 0 ELSE 1 END) + (CASE WHEN [AdmissionId] IS NULL THEN 0 ELSE 1 END) = 1");
                    table.CheckConstraint("CK_Addresses_Line1NotBlank", "LEN(LTRIM(RTRIM([Line1]))) > 0");
                    table.ForeignKey(
                        name: "FK_Addresses_Admissions_AdmissionId",
                        column: x => x.AdmissionId,
                        principalTable: "Admissions",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Cascade);
                    table.ForeignKey(
                        name: "FK_Addresses_Countries_CountryId",
                        column: x => x.CountryId,
                        principalTable: "Countries",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Restrict);
                    table.ForeignKey(
                        name: "FK_Addresses_CountryRegions_CountryId_RegionId",
                        columns: x => new { x.CountryId, x.RegionId },
                        principalTable: "CountryRegions",
                        principalColumns: new[] { "CountryId", "Id" },
                        onDelete: ReferentialAction.Restrict);
                    table.ForeignKey(
                        name: "FK_Addresses_Parents_ParentId",
                        column: x => x.ParentId,
                        principalTable: "Parents",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Cascade);
                    table.ForeignKey(
                        name: "FK_Addresses_Students_StudentId",
                        column: x => x.StudentId,
                        principalTable: "Students",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Cascade);
                });

            migrationBuilder.CreateIndex(
                name: "IX_Addresses_CountryId_RegionId",
                table: "Addresses",
                columns: new[] { "CountryId", "RegionId" });

            migrationBuilder.CreateIndex(
                name: "UX_Addresses_Admission_Primary",
                table: "Addresses",
                column: "AdmissionId",
                unique: true,
                filter: "[AdmissionId] IS NOT NULL AND [IsPrimary] = 1");

            migrationBuilder.CreateIndex(
                name: "UX_Addresses_Admission_Type",
                table: "Addresses",
                columns: new[] { "AdmissionId", "AddressType" },
                unique: true,
                filter: "[AdmissionId] IS NOT NULL");

            migrationBuilder.CreateIndex(
                name: "UX_Addresses_Parent_Primary",
                table: "Addresses",
                column: "ParentId",
                unique: true,
                filter: "[ParentId] IS NOT NULL AND [IsPrimary] = 1");

            migrationBuilder.CreateIndex(
                name: "UX_Addresses_Parent_Type",
                table: "Addresses",
                columns: new[] { "ParentId", "AddressType" },
                unique: true,
                filter: "[ParentId] IS NOT NULL");

            migrationBuilder.CreateIndex(
                name: "UX_Addresses_Student_Primary",
                table: "Addresses",
                column: "StudentId",
                unique: true,
                filter: "[StudentId] IS NOT NULL AND [IsPrimary] = 1");

            migrationBuilder.CreateIndex(
                name: "UX_Addresses_Student_Type",
                table: "Addresses",
                columns: new[] { "StudentId", "AddressType" },
                unique: true,
                filter: "[StudentId] IS NOT NULL");

            migrationBuilder.CreateIndex(
                name: "UX_Countries_Code",
                table: "Countries",
                column: "Code",
                unique: true);

            migrationBuilder.CreateIndex(
                name: "UX_Countries_Name",
                table: "Countries",
                column: "Name",
                unique: true);

            migrationBuilder.CreateIndex(
                name: "UX_CountryRegions_Country_Name",
                table: "CountryRegions",
                columns: new[] { "CountryId", "Name" },
                unique: true);

            // ---------------------------------------------------------------- seed (name-keyed, safe to re-run)
            // Countries are data, not code: a new country is added with rows like these (later from Settings).
            migrationBuilder.Sql(@"
INSERT INTO [Countries] ([Code], [Name], [RegionLabel], [PostalCodeRequired], [PostalCodePattern], [PostalCodeHint], [SortOrder], [IsActive])
SELECT v.[Code], v.[Name], v.[RegionLabel], v.[PostalCodeRequired], v.[PostalCodePattern], v.[PostalCodeHint], v.[SortOrder], 1
FROM (VALUES
    ('IN', N'India', N'State', 1, N'^[1-9][0-9]{5}$', N'6 digits, e.g. 600001', 1),
    ('AE', N'United Arab Emirates', N'Emirate', 0, NULL, NULL, 2)
) AS v([Code], [Name], [RegionLabel], [PostalCodeRequired], [PostalCodePattern], [PostalCodeHint], [SortOrder])
WHERE NOT EXISTS (SELECT 1 FROM [Countries] c WHERE c.[Code] = v.[Code]);");

            migrationBuilder.Sql(@"
INSERT INTO [CountryRegions] ([CountryId], [Name], [SortOrder], [IsActive])
SELECT c.[Id], v.[Name], v.[SortOrder], 1
FROM (VALUES
    ('IN', N'Andhra Pradesh', 1),
    ('IN', N'Arunachal Pradesh', 2),
    ('IN', N'Assam', 3),
    ('IN', N'Bihar', 4),
    ('IN', N'Chhattisgarh', 5),
    ('IN', N'Goa', 6),
    ('IN', N'Gujarat', 7),
    ('IN', N'Haryana', 8),
    ('IN', N'Himachal Pradesh', 9),
    ('IN', N'Jharkhand', 10),
    ('IN', N'Karnataka', 11),
    ('IN', N'Kerala', 12),
    ('IN', N'Madhya Pradesh', 13),
    ('IN', N'Maharashtra', 14),
    ('IN', N'Manipur', 15),
    ('IN', N'Meghalaya', 16),
    ('IN', N'Mizoram', 17),
    ('IN', N'Nagaland', 18),
    ('IN', N'Odisha', 19),
    ('IN', N'Punjab', 20),
    ('IN', N'Rajasthan', 21),
    ('IN', N'Sikkim', 22),
    ('IN', N'Tamil Nadu', 23),
    ('IN', N'Telangana', 24),
    ('IN', N'Tripura', 25),
    ('IN', N'Uttar Pradesh', 26),
    ('IN', N'Uttarakhand', 27),
    ('IN', N'West Bengal', 28),
    ('IN', N'Andaman and Nicobar Islands', 29),
    ('IN', N'Chandigarh', 30),
    ('IN', N'Dadra and Nagar Haveli and Daman and Diu', 31),
    ('IN', N'Delhi', 32),
    ('IN', N'Jammu and Kashmir', 33),
    ('IN', N'Ladakh', 34),
    ('IN', N'Lakshadweep', 35),
    ('IN', N'Puducherry', 36),
    ('AE', N'Abu Dhabi', 1),
    ('AE', N'Dubai', 2),
    ('AE', N'Sharjah', 3),
    ('AE', N'Ajman', 4),
    ('AE', N'Umm Al Quwain', 5),
    ('AE', N'Ras Al Khaimah', 6),
    ('AE', N'Fujairah', 7)
) AS v([CountryCode], [Name], [SortOrder])
JOIN [Countries] c ON c.[Code] = v.[CountryCode]
WHERE NOT EXISTS (SELECT 1 FROM [CountryRegions] r WHERE r.[CountryId] = c.[Id] AND r.[Name] = v.[Name]);");

            // ---------------------------------------------------------------- copy the old single address (expand step)
            // Each Student / Parent / Admission with an old address gets one 'Home' address, marked primary.
            // The country is taken from the state / emirate name, else a 6-digit Indian PIN code, else a city
            // that is an emirate. Rows without an address line, a city or a recognisable country are NOT copied:
            // database/scripts/g1a-address-copy-check.sql lists them so they can be entered by hand before G1c.
            // The old columns are not touched (they are dropped in G1c).

            migrationBuilder.Sql(@"
WITH src AS (
    SELECT o.[Id],
           LTRIM(RTRIM(o.[AddressLine])) AS [Line1],
           LTRIM(RTRIM(o.[City])) AS [City],
           NULLIF(LTRIM(RTRIM(o.[State])), '') AS [StateText],
           NULLIF(LTRIM(RTRIM(o.[Pincode])), '') AS [Pin]
    FROM [Students] o
), matched AS (
    SELECT src.*,
           r.[Id] AS [RegionId],
           COALESCE(
               r.[CountryId],
               CASE WHEN src.[Pin] LIKE '[1-9][0-9][0-9][0-9][0-9][0-9]' THEN (SELECT [Id] FROM [Countries] WHERE [Code] = 'IN') END,
               CASE WHEN src.[City] IN (SELECT er.[Name] FROM [CountryRegions] er JOIN [Countries] ec ON ec.[Id] = er.[CountryId] WHERE ec.[Code] = 'AE')
                    THEN (SELECT [Id] FROM [Countries] WHERE [Code] = 'AE') END
           ) AS [CountryId]
    FROM src
    OUTER APPLY (
        SELECT TOP (1) cr.[Id], cr.[CountryId]
        FROM [CountryRegions] cr
        WHERE cr.[Name] = src.[StateText]
           OR (src.[StateText] IS NULL AND cr.[Name] = src.[City]
               AND cr.[CountryId] = (SELECT [Id] FROM [Countries] WHERE [Code] = 'AE'))
        ORDER BY cr.[Id]
    ) r
)
INSERT INTO [Addresses] ([StudentId], [AddressType], [IsPrimary], [Line1], [Line2], [City], [PostalCode], [CountryId], [RegionId])
SELECT m.[Id], 'Home', 1, m.[Line1], NULL, m.[City], m.[Pin], m.[CountryId], m.[RegionId]
FROM matched m
WHERE m.[Line1] <> '' AND m.[City] <> '' AND m.[CountryId] IS NOT NULL
  AND NOT EXISTS (SELECT 1 FROM [Addresses] a WHERE a.[StudentId] = m.[Id]);");

            migrationBuilder.Sql(@"
WITH src AS (
    SELECT o.[Id],
           LTRIM(RTRIM(o.[AddressLine])) AS [Line1],
           LTRIM(RTRIM(o.[City])) AS [City],
           NULLIF(LTRIM(RTRIM(o.[State])), '') AS [StateText],
           NULLIF(LTRIM(RTRIM(o.[Pincode])), '') AS [Pin]
    FROM [Parents] o
), matched AS (
    SELECT src.*,
           r.[Id] AS [RegionId],
           COALESCE(
               r.[CountryId],
               CASE WHEN src.[Pin] LIKE '[1-9][0-9][0-9][0-9][0-9][0-9]' THEN (SELECT [Id] FROM [Countries] WHERE [Code] = 'IN') END,
               CASE WHEN src.[City] IN (SELECT er.[Name] FROM [CountryRegions] er JOIN [Countries] ec ON ec.[Id] = er.[CountryId] WHERE ec.[Code] = 'AE')
                    THEN (SELECT [Id] FROM [Countries] WHERE [Code] = 'AE') END
           ) AS [CountryId]
    FROM src
    OUTER APPLY (
        SELECT TOP (1) cr.[Id], cr.[CountryId]
        FROM [CountryRegions] cr
        WHERE cr.[Name] = src.[StateText]
           OR (src.[StateText] IS NULL AND cr.[Name] = src.[City]
               AND cr.[CountryId] = (SELECT [Id] FROM [Countries] WHERE [Code] = 'AE'))
        ORDER BY cr.[Id]
    ) r
)
INSERT INTO [Addresses] ([ParentId], [AddressType], [IsPrimary], [Line1], [Line2], [City], [PostalCode], [CountryId], [RegionId])
SELECT m.[Id], 'Home', 1, m.[Line1], NULL, m.[City], m.[Pin], m.[CountryId], m.[RegionId]
FROM matched m
WHERE m.[Line1] <> '' AND m.[City] <> '' AND m.[CountryId] IS NOT NULL
  AND NOT EXISTS (SELECT 1 FROM [Addresses] a WHERE a.[ParentId] = m.[Id]);");

            migrationBuilder.Sql(@"
WITH src AS (
    SELECT o.[Id],
           LTRIM(RTRIM(o.[AddressLine])) AS [Line1],
           LTRIM(RTRIM(o.[City])) AS [City],
           NULLIF(LTRIM(RTRIM(o.[State])), '') AS [StateText],
           NULLIF(LTRIM(RTRIM(o.[Pincode])), '') AS [Pin]
    FROM [Admissions] o
), matched AS (
    SELECT src.*,
           r.[Id] AS [RegionId],
           COALESCE(
               r.[CountryId],
               CASE WHEN src.[Pin] LIKE '[1-9][0-9][0-9][0-9][0-9][0-9]' THEN (SELECT [Id] FROM [Countries] WHERE [Code] = 'IN') END,
               CASE WHEN src.[City] IN (SELECT er.[Name] FROM [CountryRegions] er JOIN [Countries] ec ON ec.[Id] = er.[CountryId] WHERE ec.[Code] = 'AE')
                    THEN (SELECT [Id] FROM [Countries] WHERE [Code] = 'AE') END
           ) AS [CountryId]
    FROM src
    OUTER APPLY (
        SELECT TOP (1) cr.[Id], cr.[CountryId]
        FROM [CountryRegions] cr
        WHERE cr.[Name] = src.[StateText]
           OR (src.[StateText] IS NULL AND cr.[Name] = src.[City]
               AND cr.[CountryId] = (SELECT [Id] FROM [Countries] WHERE [Code] = 'AE'))
        ORDER BY cr.[Id]
    ) r
)
INSERT INTO [Addresses] ([AdmissionId], [AddressType], [IsPrimary], [Line1], [Line2], [City], [PostalCode], [CountryId], [RegionId])
SELECT m.[Id], 'Home', 1, m.[Line1], NULL, m.[City], m.[Pin], m.[CountryId], m.[RegionId]
FROM matched m
WHERE m.[Line1] <> '' AND m.[City] <> '' AND m.[CountryId] IS NOT NULL
  AND NOT EXISTS (SELECT 1 FROM [Addresses] a WHERE a.[AdmissionId] = m.[Id]);");
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropTable(
                name: "Addresses");

            migrationBuilder.DropTable(
                name: "CountryRegions");

            migrationBuilder.DropTable(
                name: "Countries");
        }
    }
}
