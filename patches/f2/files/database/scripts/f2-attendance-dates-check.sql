-- F2 (school time zone): proof that existing attendance dates do not change.
-- Run this in SSMS BEFORE deploying F2 and again AFTER (and after changing the school time zone in Settings).
-- The two result rows must be IDENTICAL. F2 has no migration and never rewrites AttendanceSessions.Date:
-- the school's time zone only decides what "today" means for NEW requests (future-date and 7-day checks,
-- the default range of a student's history).
USE [SchoolManagementDb];

SELECT COUNT(*)                                              AS Sessions,
       MIN([Date])                                           AS FirstDate,
       MAX([Date])                                           AS LastDate,
       CHECKSUM_AGG(CHECKSUM([Id], [ClassSectionId], [SubjectId], [Date])) AS DatesChecksum
FROM [dbo].[AttendanceSessions];

SELECT COUNT(*)                                              AS Records,
       CHECKSUM_AGG(CHECKSUM([SessionId], [StudentId], [Status])) AS RecordsChecksum
FROM [dbo].[AttendanceRecords];
