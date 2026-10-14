-- E2c pre-check: run in SSMS (database SchoolManagementDb) AFTER g1a-backup-before-migration.sql
-- (@label = 'E2c') and BEFORE applying the E2c migration. It only reads; it changes nothing.
--
-- E2c drops Teachers.Name and Teachers.Phone: the staff profile is the one copy. The migration stops by
-- itself if any row below is not clean; this script shows you which rows, so you can decide.
USE SchoolManagementDb;
GO

SELECT
    (SELECT COUNT(*) FROM Teachers WHERE IsDeleted = 0)                                    AS ActiveTeachers,
    (SELECT COUNT(*) FROM Teachers t
        WHERE t.IsDeleted = 0
          AND NOT EXISTS (SELECT 1 FROM StaffProfiles sp WHERE sp.UserId = t.UserId AND sp.IsDeleted = 0))
                                                                                          AS WithoutProfile,
    (SELECT COUNT(*) FROM Teachers t JOIN StaffProfiles sp ON sp.UserId = t.UserId AND sp.IsDeleted = 0
        WHERE t.IsDeleted = 0 AND sp.FullName <> t.Name)                                  AS NameDiffers,
    (SELECT COUNT(*) FROM Teachers t JOIN StaffProfiles sp ON sp.UserId = t.UserId AND sp.IsDeleted = 0
        WHERE t.IsDeleted = 0 AND ISNULL(sp.Mobile, N'') <> ISNULL(t.Phone, N''))         AS PhoneDiffers;
-- All three of WithoutProfile / NameDiffers / PhoneDiffers must be 0.

-- The rows that are not clean (empty result = clean):
SELECT t.Id AS TeacherId, t.UserId, u.Username,
       t.Name AS TeacherName, sp.FullName AS ProfileName,
       t.Phone AS TeacherPhone, sp.Mobile AS ProfileMobile,
       CASE WHEN sp.Id IS NULL THEN 'no profile' ELSE 'differs' END AS Problem
FROM Teachers t
JOIN Users u ON u.Id = t.UserId
LEFT JOIN StaffProfiles sp ON sp.UserId = t.UserId AND sp.IsDeleted = 0
WHERE t.IsDeleted = 0
  AND (sp.Id IS NULL OR sp.FullName <> t.Name OR ISNULL(sp.Mobile, N'') <> ISNULL(t.Phone, N''));

-- If rows differ, decide which side is right and run ONE of these, then run this script again:
--   keep the TEACHER's values (copy them to the profile):
--     UPDATE sp SET sp.FullName = t.Name, sp.Mobile = t.Phone
--     FROM StaffProfiles sp JOIN Teachers t ON t.UserId = sp.UserId AND t.IsDeleted = 0
--     WHERE sp.IsDeleted = 0 AND (sp.FullName <> t.Name OR ISNULL(sp.Mobile, N'') <> ISNULL(t.Phone, N''));
--   keep the PROFILE's values (nothing to do: the migration drops the teacher's copy), but then
--     the guard still stops, so first make them equal the other way round:
--     UPDATE t SET t.Name = sp.FullName, t.Phone = sp.Mobile
--     FROM Teachers t JOIN StaffProfiles sp ON sp.UserId = t.UserId AND sp.IsDeleted = 0
--     WHERE t.IsDeleted = 0 AND (sp.FullName <> t.Name OR ISNULL(sp.Mobile, N'') <> ISNULL(t.Phone, N''));
-- A teacher WITHOUT a profile: open that user on the Users page and save it once (creates the profile),
-- or insert the profile by hand.
