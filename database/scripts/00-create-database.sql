/* ==========================================================================
   Database creation — run once, against no specific database context
   (e.g. connected to `master`), before any table script.
   Target: Microsoft SQL Server
   ========================================================================== */

IF NOT EXISTS (SELECT 1 FROM sys.databases WHERE name = N'SchoolManagementDb')
BEGIN
    CREATE DATABASE [SchoolManagementDb];
END
GO

/*
   Collation: left at server default rather than pinned here.
   Flagging as a BUSINESS DECISION REQUIRED if the school needs
   case-sensitive comparisons anywhere (e.g. Username) or a specific
   collation for Arabic-script sorting — not guessed at.
*/
