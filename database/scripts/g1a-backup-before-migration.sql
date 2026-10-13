-- G1a: back up the development database BEFORE applying the migration G1a_Addresses.
-- Run in SSMS (or sqlcmd -S IMADH\SQLEXPRESS -E -i this-file). Change the folder if needed: it must
-- exist and the SQL Server service account must be able to write to it.
--
-- COPY_ONLY: does not disturb any other backup schedule. CHECKSUM + VERIFYONLY: the file is
-- checked after it is written, so you know it can be restored.

DECLARE @file nvarchar(400) =
    N'C:\SqlBackups\SchoolManagementDb_before_G1a_' + FORMAT(SYSDATETIME(), 'yyyyMMdd_HHmm') + N'.bak';

BACKUP DATABASE [SchoolManagementDb]
    TO DISK = @file
    WITH COPY_ONLY, INIT, CHECKSUM, STATS = 10,
         NAME = N'SchoolManagementDb before G1a (Addresses)';

RESTORE VERIFYONLY FROM DISK = @file WITH CHECKSUM;

PRINT N'Backup written and verified: ' + @file;

-- To roll back completely (only if the migration went wrong):
--   1. stop the API;
--   2. USE master;
--      ALTER DATABASE [SchoolManagementDb] SET SINGLE_USER WITH ROLLBACK IMMEDIATE;
--      RESTORE DATABASE [SchoolManagementDb] FROM DISK = N'<the file printed above>' WITH REPLACE;
--      ALTER DATABASE [SchoolManagementDb] SET MULTI_USER;
--   3. git checkout 04fa023 (the last commit before G1a).
-- A lighter alternative: the migration's Down only drops the three new tables (the old address
-- columns are never changed), so `dotnet ef database update F1_ScopedReadPermissions` also undoes G1a.
