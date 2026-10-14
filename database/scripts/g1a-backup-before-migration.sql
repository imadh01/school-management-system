-- Back up the development database BEFORE applying a migration (written for G1a, used for every
-- migration since). Run in SSMS, or: sqlcmd -S IMADH\SQLEXPRESS -E -i this-file
--
-- 1. Set @label to the migration you are about to apply (it goes into the file name).
-- 2. Leave @folder NULL to use SQL Server's own default backup folder. The SQL Server SERVICE account
--    (not your Windows user) writes the file, and it can always write there. A folder of your own,
--    e.g. C:\SqlBackups, must exist AND give that account write permission, otherwise SQL Server
--    answers "Operating system error 5 (Access is denied)".
--
-- COPY_ONLY: does not disturb any other backup schedule. CHECKSUM + VERIFYONLY: the file is
-- checked after it is written, so you know it can be restored. If anything fails, the script stops
-- with the error and prints nothing that looks like success.

DECLARE @label  nvarchar(50)  = N'G1b';  -- the migration about to be applied
DECLARE @folder nvarchar(300) = NULL;    -- NULL = the instance's default backup folder

SET @folder = COALESCE(@folder, CAST(SERVERPROPERTY('InstanceDefaultBackupPath') AS nvarchar(300)));
IF RIGHT(@folder, 1) <> N'\' SET @folder += N'\';

DECLARE @file nvarchar(400) =
    @folder + N'SchoolManagementDb_before_' + @label + N'_' + FORMAT(SYSDATETIME(), 'yyyyMMdd_HHmm') + N'.bak';
DECLARE @name nvarchar(128) = N'SchoolManagementDb before ' + @label;

BEGIN TRY
    BACKUP DATABASE [SchoolManagementDb]
        TO DISK = @file
        WITH COPY_ONLY, INIT, CHECKSUM, STATS = 10, NAME = @name;

    RESTORE VERIFYONLY FROM DISK = @file WITH CHECKSUM;

    PRINT N'Backup written and verified: ' + @file;
END TRY
BEGIN CATCH
    PRINT N'BACKUP FAILED - do not apply the migration. File: ' + @file;
    THROW; -- shows SQL Server's own error (e.g. 3201 / OS error 5 = no write permission on the folder)
END CATCH;

-- To roll back completely (only if the migration went wrong):
--   1. stop the API;
--   2. USE master;
--      ALTER DATABASE [SchoolManagementDb] SET SINGLE_USER WITH ROLLBACK IMMEDIATE;
--      RESTORE DATABASE [SchoolManagementDb] FROM DISK = N'<the file printed above>' WITH REPLACE;
--      ALTER DATABASE [SchoolManagementDb] SET MULTI_USER;
--   3. git checkout <the commit before the migration>.
-- A lighter alternative: `dotnet ef database update <previous migration>` runs the migration's Down.
-- G1b's Down drops Documents and DocumentTypes; files already uploaded stay in the documents folder
-- (Documents:StorageRoot) and must be removed by hand.
