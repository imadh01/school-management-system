// Hand-written data migration (the model does not change, so there is no Designer file).
using Microsoft.EntityFrameworkCore.Infrastructure;
using Microsoft.EntityFrameworkCore.Migrations;
using SchoolManagement.Infrastructure.Persistence;

#nullable disable

namespace SchoolManagement.Infrastructure.Migrations
{
    /// <summary>
    /// One-time back-fill of blank employee numbers (EMP-0001 ...), in StaffProfiles.Id order, keeping existing
    /// numbers. Every teacher and staff member is an employee; new accounts already get a number when created.
    /// A backup table (StaffProfiles_EmployeeNumberBackfill) lists the rows that were blank; it can be dropped later.
    /// A failed count or unique check THROWs, which rolls the migration back.
    /// </summary>
    [DbContext(typeof(ApplicationDbContext))]
    [Migration("20261019083000_BackfillEmployeeNumbers")]
    public partial class BackfillEmployeeNumbers : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.Sql(@"
-- One-time back-fill of blank employee numbers. Rows that already have a number are never touched.
-- Order is fixed: StaffProfiles.Id ascending, numbering continues after the highest existing EMP-nnnn.
DECLARE @expected int = (SELECT COUNT(*) FROM dbo.StaffProfiles WHERE IsDeleted = 0 AND EmployeeNumber IS NULL);

IF @expected > 0
BEGIN
    -- 1. Backup: the profile ids that were blank, so the change can be undone
    --    (UPDATE dbo.StaffProfiles SET EmployeeNumber = NULL WHERE Id IN (SELECT Id FROM dbo.StaffProfiles_EmployeeNumberBackfill)).
    SELECT Id, SYSUTCDATETIME() AS BackedUpAtUtc
    INTO dbo.StaffProfiles_EmployeeNumberBackfill
    FROM dbo.StaffProfiles
    WHERE IsDeleted = 0 AND EmployeeNumber IS NULL;

    -- 2. Highest serial already used by a live profile: only values that are exactly EMP- followed by 1 to 9 digits.
    DECLARE @start int = ISNULL((
        SELECT MAX(CAST(SUBSTRING(EmployeeNumber, 5, 30) AS int))
        FROM dbo.StaffProfiles
        WHERE IsDeleted = 0
          AND EmployeeNumber LIKE 'EMP-%'
          AND LEN(EmployeeNumber) BETWEEN 5 AND 13
          AND SUBSTRING(EmployeeNumber, 5, 30) NOT LIKE '%[^0-9]%'), 0);

    -- 3. Number the blank rows in Id order: EMP-0001 style, at least 4 digits.
    ;WITH todo AS (
        SELECT Id, @start + ROW_NUMBER() OVER (ORDER BY Id) AS serial
        FROM dbo.StaffProfiles
        WHERE IsDeleted = 0 AND EmployeeNumber IS NULL
    )
    UPDATE p
    SET EmployeeNumber = 'EMP-' + REPLICATE('0', CASE WHEN LEN(CAST(t.serial AS varchar(12))) < 4 THEN 4 - LEN(CAST(t.serial AS varchar(12))) ELSE 0 END)
                         + CAST(t.serial AS varchar(12)),
        UpdatedAt = SYSUTCDATETIME()
    FROM dbo.StaffProfiles p
    JOIN todo t ON t.Id = p.Id;

    -- 4. Count check: exactly the rows that were blank got a number, and none are blank now.
    DECLARE @updated int = (SELECT COUNT(*) FROM dbo.StaffProfiles p JOIN dbo.StaffProfiles_EmployeeNumberBackfill b ON b.Id = p.Id WHERE p.EmployeeNumber IS NOT NULL);
    DECLARE @stillBlank int = (SELECT COUNT(*) FROM dbo.StaffProfiles WHERE IsDeleted = 0 AND EmployeeNumber IS NULL);
    IF @updated <> @expected OR @stillBlank <> 0
        THROW 51000, 'Employee number back-fill: the number of updated rows does not match the number of blank rows.', 1;

    -- 5. Unique check (the unique index would refuse duplicates anyway; this gives a clear message first).
    IF EXISTS (SELECT EmployeeNumber FROM dbo.StaffProfiles WHERE IsDeleted = 0 AND EmployeeNumber IS NOT NULL GROUP BY EmployeeNumber HAVING COUNT(*) > 1)
        THROW 51001, 'Employee number back-fill: duplicate employee numbers exist after the back-fill.', 1;
END
");
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            // Numbers handed out later cannot be told apart from typed ones, so Down only undoes the back-fill rows
            // that still hold their generated number, then drops the backup.
            migrationBuilder.Sql(@"
IF OBJECT_ID('dbo.StaffProfiles_EmployeeNumberBackfill') IS NOT NULL
BEGIN
    UPDATE p SET EmployeeNumber = NULL, UpdatedAt = SYSUTCDATETIME()
    FROM dbo.StaffProfiles p JOIN dbo.StaffProfiles_EmployeeNumberBackfill b ON b.Id = p.Id;
    DROP TABLE dbo.StaffProfiles_EmployeeNumberBackfill;
END");
        }
    }
}
