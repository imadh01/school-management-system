/* ==========================================================================
   Module 1 — Authentication & Authorization
   Tables: Users, Roles, Permissions, UserRoles, RolePermissions
   Matches: "Module 1: Authentication & Authorization — Database Design" doc
   Target: Microsoft SQL Server
   ========================================================================== */

/* Run 00-create-database.sql first. This script assumes that database
   exists and targets it explicitly — it does not rely on whatever
   database the connection happens to default to. */
USE [SchoolManagementDb];
GO

SET ANSI_NULLS ON;
SET QUOTED_IDENTIFIER ON;
GO

/* --------------------------------------------------------------------------
   Roles — fixed catalog: Admin, Supervisor, Clerk, Teacher, Student, Parent
   -------------------------------------------------------------------------- */
CREATE TABLE dbo.Roles
(
    Id              INT IDENTITY(1,1)      NOT NULL,
    [Name]          NVARCHAR(30)           NOT NULL,
    Description     NVARCHAR(200)          NULL,

    CONSTRAINT PK_Roles PRIMARY KEY CLUSTERED (Id),
    CONSTRAINT UQ_Roles_Name UNIQUE (Name)
);
GO

/* --------------------------------------------------------------------------
   Permissions — granular action rows. Empty at Module 1 sign-off; seeded
   per-module as each module's actions are designed (Decision #3).
   -------------------------------------------------------------------------- */
CREATE TABLE dbo.Permissions
(
    Id              INT IDENTITY(1,1)      NOT NULL,
    [Name]          NVARCHAR(100)          NOT NULL,   -- e.g. 'Student.Create'
    [Module]        NVARCHAR(50)           NOT NULL,   -- groups by module for admin UI
    Description     NVARCHAR(200)          NULL,

    CONSTRAINT PK_Permissions PRIMARY KEY CLUSTERED (Id),
    CONSTRAINT UQ_Permissions_Name UNIQUE (Name)
);
GO

CREATE NONCLUSTERED INDEX IX_Permissions_Module
    ON dbo.Permissions ([Module]);
GO

/* --------------------------------------------------------------------------
   Users — one row per login-capable person, any role. Only table in this
   module with soft delete + audit-by columns, since it's referenced by
   nearly every future table (Students/Teachers/Parents link here later).
   -------------------------------------------------------------------------- */
CREATE TABLE dbo.Users
(
    Id              INT IDENTITY(1,1)      NOT NULL,
    Username        NVARCHAR(50)           NOT NULL,
    Email           NVARCHAR(100)          NOT NULL,
    PasswordHash    NVARCHAR(MAX)          NOT NULL,
    [Status]        NVARCHAR(20)           NOT NULL
                        CONSTRAINT DF_Users_Status DEFAULT ('Active'),
    LastLoginAt     DATETIME2              NULL,       -- UTC

    CreatedAt       DATETIME2              NOT NULL,   -- UTC, set by app (SaveChanges interceptor)
    UpdatedAt       DATETIME2              NOT NULL,   -- UTC
    CreatedBy       INT                    NULL,
    UpdatedBy       INT                    NULL,

    IsDeleted       BIT                    NOT NULL
                        CONSTRAINT DF_Users_IsDeleted DEFAULT (0),
    DeletedAt       DATETIME2              NULL,
    DeletedBy       INT                    NULL,

    CONSTRAINT PK_Users PRIMARY KEY CLUSTERED (Id),
    CONSTRAINT UQ_Users_Username UNIQUE (Username),
    CONSTRAINT UQ_Users_Email UNIQUE (Email),
    CONSTRAINT CK_Users_Status
        CHECK ([Status] IN ('Active', 'Inactive', 'Suspended')),

    -- Self-referencing audit FKs (who created/updated/deleted this user)
    CONSTRAINT FK_Users_CreatedBy FOREIGN KEY (CreatedBy) REFERENCES dbo.Users (Id),
    CONSTRAINT FK_Users_UpdatedBy FOREIGN KEY (UpdatedBy) REFERENCES dbo.Users (Id),
    CONSTRAINT FK_Users_DeletedBy FOREIGN KEY (DeletedBy) REFERENCES dbo.Users (Id)
);
GO

/* --------------------------------------------------------------------------
   UserRoles — many-to-many; a person can hold more than one role
   -------------------------------------------------------------------------- */
CREATE TABLE dbo.UserRoles
(
    UserId          INT                    NOT NULL,
    RoleId          INT                    NOT NULL,
    AssignedAt      DATETIME2              NOT NULL
                        CONSTRAINT DF_UserRoles_AssignedAt DEFAULT (SYSUTCDATETIME()),

    CONSTRAINT PK_UserRoles PRIMARY KEY CLUSTERED (UserId, RoleId),
    CONSTRAINT FK_UserRoles_Users FOREIGN KEY (UserId) REFERENCES dbo.Users (Id) ON DELETE CASCADE,
    CONSTRAINT FK_UserRoles_Roles FOREIGN KEY (RoleId) REFERENCES dbo.Roles (Id) ON DELETE CASCADE
);
GO

-- Reverse lookup: "who holds this role" (e.g. listing all Teachers)
CREATE NONCLUSTERED INDEX IX_UserRoles_RoleId
    ON dbo.UserRoles (RoleId);
GO

/* --------------------------------------------------------------------------
   RolePermissions — many-to-many; a role's access is data, not hardcoded
   -------------------------------------------------------------------------- */
CREATE TABLE dbo.RolePermissions
(
    RoleId          INT                    NOT NULL,
    PermissionId    INT                    NOT NULL,

    CONSTRAINT PK_RolePermissions PRIMARY KEY CLUSTERED (RoleId, PermissionId),
    CONSTRAINT FK_RolePermissions_Roles FOREIGN KEY (RoleId) REFERENCES dbo.Roles (Id) ON DELETE CASCADE,
    CONSTRAINT FK_RolePermissions_Permissions FOREIGN KEY (PermissionId) REFERENCES dbo.Permissions (Id) ON DELETE CASCADE
);
GO

/* --------------------------------------------------------------------------
   Seed: the six confirmed roles only. No permissions seeded here —
   per Decision #3, the base Permissions list is defined per-module as
   each module's actions are designed.
   -------------------------------------------------------------------------- */
INSERT INTO dbo.Roles ([Name]) VALUES
    ('Admin'), ('Supervisor'), ('Clerk'), ('Teacher'), ('Student'), ('Parent');
GO
