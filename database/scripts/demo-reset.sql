-- Clears the school's day-to-day data so the demo starts clean. Run in SSMS against the TEST /
-- demo database only, AFTER a backup (g1a-backup-before-migration.sql with @label = 'demo').
--
-- DELETED: students, parents, admissions, documents (rows), attendance, class sections, subjects,
--          teachers, every user that is not an Admin, refresh tokens, audit log.
-- KEPT:    Admin users (and their profiles), roles, permissions, academic years, countries and
--          regions, document types, school settings.
--
-- It runs inside a transaction and ROLLS BACK unless you set @Commit = 1, so the first run is a dry
-- run that only shows the counts. Uploaded document FILES on disk are not touched: delete the
-- upload folder by hand if you want them gone (see Storage in appsettings).
--
-- Raw DELETEs bypass the soft-delete filter on purpose: soft-deleted rows go too.

SET XACT_ABORT ON;
DECLARE @Commit bit = 0;   -- 0 = dry run (rolls back), 1 = really delete

BEGIN TRAN;

DECLARE @keep TABLE (UserId int PRIMARY KEY);
INSERT @keep
SELECT DISTINCT ur.UserId
FROM UserRoles ur JOIN Roles r ON r.Id = ur.RoleId
WHERE r.Name = N'Admin';

IF NOT EXISTS (SELECT 1 FROM @keep)
BEGIN
    RAISERROR('No Admin user found. Nothing was deleted.', 16, 1);
    ROLLBACK;
    RETURN;
END

DELETE FROM AttendanceRecords;
DELETE FROM AttendanceSessions;
DELETE FROM Documents;
DELETE FROM StudentEnrollments;
DELETE FROM StudentPickupPersons;
DELETE FROM StudentIdentityDocuments;
DELETE FROM StudentHealth;
DELETE FROM StudentGuardians;
DELETE FROM AdmissionGuardians;

DELETE FROM Addresses
WHERE StudentId IS NOT NULL OR ParentId IS NOT NULL OR AdmissionId IS NOT NULL
   OR StaffProfileId IN (SELECT Id FROM StaffProfiles WHERE UserId NOT IN (SELECT UserId FROM @keep));

DELETE FROM Students;      -- Students reference Admissions, so they go first
DELETE FROM Admissions;
DELETE FROM Parents;

UPDATE Subjects SET TeacherId = NULL;
UPDATE ClassSections SET ClassTeacherId = NULL;
DELETE FROM Subjects;
DELETE FROM ClassSections;
DELETE FROM Teachers;

DELETE FROM StaffProfiles WHERE UserId NOT IN (SELECT UserId FROM @keep);
DELETE FROM RefreshTokens WHERE UserId NOT IN (SELECT UserId FROM @keep);
DELETE FROM UserRoles WHERE UserId NOT IN (SELECT UserId FROM @keep);
UPDATE Users SET CreatedBy = NULL WHERE CreatedBy IS NOT NULL AND CreatedBy NOT IN (SELECT UserId FROM @keep);
UPDATE Users SET UpdatedBy = NULL WHERE UpdatedBy IS NOT NULL AND UpdatedBy NOT IN (SELECT UserId FROM @keep);
UPDATE Users SET DeletedBy = NULL WHERE DeletedBy IS NOT NULL AND DeletedBy NOT IN (SELECT UserId FROM @keep);
DELETE FROM Users WHERE Id NOT IN (SELECT UserId FROM @keep);
DELETE FROM AuditLogs;

-- What is left (should be only configuration and the Admin users):
SELECT 'Users kept' AS What, COUNT(*) AS N FROM Users
UNION ALL SELECT 'Students', COUNT(*) FROM Students
UNION ALL SELECT 'Parents', COUNT(*) FROM Parents
UNION ALL SELECT 'Admissions', COUNT(*) FROM Admissions
UNION ALL SELECT 'ClassSections', COUNT(*) FROM ClassSections
UNION ALL SELECT 'AcademicYears (kept)', COUNT(*) FROM AcademicYears
UNION ALL SELECT 'DocumentTypes (kept)', COUNT(*) FROM DocumentTypes;

IF @Commit = 1
BEGIN
    COMMIT;
    PRINT 'Committed: demo data cleared.';
END
ELSE
BEGIN
    ROLLBACK;
    PRINT 'DRY RUN: rolled back, nothing was deleted. Set @Commit = 1 and run again to delete.';
END
