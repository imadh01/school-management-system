# Data Dictionary (every column)

Generated from `ApplicationDbContextModelSnapshot.cs` (the EF model after migration `E2c_DropTeacherNameAndPhone`), so it matches the real database.
Regenerate it after every migration: `python3 database/scripts/generate-data-dictionary.py backend/SchoolManagement.Infrastructure/Migrations/ApplicationDbContextModelSnapshot.cs docs/data-dictionary.md <last-migration-name>`.

Legend: **PK** primary key · **FK** foreign key (→ table, delete behaviour) · **UQ** single-column unique · **NULL** nullable · Default shown when the model sets one.
Total: **29 tables, 417 columns**.

Audit columns (`CreatedAt`, `CreatedBy`, `UpdatedAt`, `UpdatedBy`) and soft-delete columns (`IsDeleted`, `DeletedAt`, `DeletedBy`) repeat on many tables and are listed last in each table. Sensitive columns are masked in `AuditLogs`.

## Contents

1. [Users](#users) — Login accounts: one row per person who signs in (staff now; student/parent logins are a later decision).
2. [Roles](#roles) — Roles (Admin, Supervisor, Clerk, Teacher and any custom role). Permissions are granted to roles.
3. [Permissions](#permissions) — The permission catalog: one row per `Module.Action` (kept equal to the code's catalog by a test).
4. [UserRoles](#userroles) — Which role a user has (many-to-many table; the service allows one role per user).
5. [RolePermissions](#rolepermissions) — Which permissions a role grants (many-to-many).
6. [StaffProfiles](#staffprofiles) — The person behind a staff login: name, contact and HR details, one per user.
7. [SchoolSettings](#schoolsettings) — The school's own record: one row (name, contacts, time zone, currency, working days).
8. [RefreshTokens](#refreshtokens) — Hashed refresh tokens for sign-in sessions (rotated; reuse is detected).
9. [AuditLogs](#auditlogs) — Who changed what: one row per audited change, with sensitive values masked.
10. [AcademicYears](#academicyears) — School years; exactly one has IsCurrent = 1.
11. [ClassSections](#classsections) — A class + section in one academic year (capacity, class teacher).
12. [Teachers](#teachers) — Teaching data of a user with the Teacher role, one-to-one with a user. Name and phone live on the staff profile.
13. [Subjects](#subjects) — A subject in one class section, taught by at most one teacher.
14. [Admissions](#admissions) — Admission applications (Draft, Confirmed, Enrolled, Rejected).
15. [AdmissionGuardians](#admissionguardians) — Father/mother/guardian written on an application (a snapshot, matched to real parents at enrolment).
16. [Students](#students) — The student's current record (academic history lives in StudentEnrollments).
17. [StudentEnrollments](#studentenrollments) — Academic history: one row per class/section period; one Active period per student.
18. [StudentHealth](#studenthealth) — Health and insurance details, one row per student.
19. [StudentIdentityDocuments](#studentidentitydocuments) — Identity numbers and passport/visa details (sensitive), one row per student.
20. [StudentPickupPersons](#studentpickuppersons) — People authorised to collect the child.
21. [Parents](#parents) — Parent/guardian people, one row per real person.
22. [StudentGuardians](#studentguardians) — Links a student to a parent, with the relation and the primary-contact flag.
23. [Addresses](#addresses) — Postal addresses for a student, parent, application or staff member (exactly one owner).
24. [Countries](#countries) — Reference list of countries (seeded).
25. [CountryRegions](#countryregions) — States / regions of a country (seeded), used by address forms.
26. [DocumentTypes](#documenttypes) — Reference list of document types (birth certificate, passport...), with sensitivity and expiry flags.
27. [Documents](#documents) — Uploaded files (PDF/JPG/PNG) of a student or an application, with their review status.
28. [AttendanceSessions](#attendancesessions) — One roll call: class + date (+ optional subject).
29. [AttendanceRecords](#attendancerecords) — One student's status in one attendance session.


## Users

Login accounts: one row per person who signs in (staff now; student/parent logins are a later decision).  
Entity: `User` · Columns: **18**

| # | Column | SQL type | Null | Default / notes | Key |
|---|---|---|---|---|---|
| 1 | `Id` | int | NOT NULL | identity | PK |
| 2 | `AccessFailedCount` | int | NOT NULL | default 0 |  |
| 3 | `Email` | nvarchar(100) | NOT NULL |  | UQ (filtered) |
| 4 | `LastLoginAt` | datetime2 | NULL |  |  |
| 5 | `LockoutEnd` | datetime2 | NULL |  |  |
| 6 | `MustChangePassword` | bit | NOT NULL | default false |  |
| 7 | `PasswordHash` | nvarchar(max) | NOT NULL |  |  |
| 8 | `RowVersion` | binary(8) | NOT NULL | concurrency token (rowVersion) |  |
| 9 | `SecurityStamp` | uniqueidentifier | NOT NULL | default `NEWID()` |  |
| 10 | `Status` | nvarchar(20) | NOT NULL | default "Active" |  |
| 11 | `Username` | nvarchar(50) | NOT NULL |  | UQ (filtered) |
| 12 | `CreatedAt` | datetime2 | NOT NULL |  |  |
| 13 | `CreatedBy` | int | NULL |  | FK → Users (NoAction) |
| 14 | `UpdatedAt` | datetime2 | NOT NULL |  |  |
| 15 | `UpdatedBy` | int | NULL |  | FK → Users (NoAction) |
| 16 | `IsDeleted` | bit | NOT NULL | default false |  |
| 17 | `DeletedAt` | datetime2 | NULL |  |  |
| 18 | `DeletedBy` | int | NULL |  | FK → Users (NoAction) |

**Indexes**

| Columns | Unique | Filter | Name |
|---|---|---|---|
| CreatedBy |  |  |  |
| DeletedBy |  |  |  |
| Email | yes | `[IsDeleted] = 0` |  |
| UpdatedBy |  |  |  |
| Username | yes | `[IsDeleted] = 0` |  |

**Check constraints**

| Name | Rule |
|---|---|
| CK_Users_Status | `[Status] IN ('Active', 'Inactive', 'Suspended')` |

## Roles

Roles (Admin, Supervisor, Clerk, Teacher and any custom role). Permissions are granted to roles.  
Entity: `Role` · Columns: **10**

| # | Column | SQL type | Null | Default / notes | Key |
|---|---|---|---|---|---|
| 1 | `Id` | int | NOT NULL | identity | PK |
| 2 | `Description` | nvarchar(200) | NULL |  |  |
| 3 | `IsActive` | bit | NOT NULL | default true |  |
| 4 | `IsSystem` | bit | NOT NULL | default false |  |
| 5 | `Name` | nvarchar(30) | NOT NULL |  | UQ |
| 6 | `RowVersion` | rowversion | NOT NULL | concurrency token (rowVersion); rowversion |  |
| 7 | `CreatedAt` | datetime2 | NOT NULL |  |  |
| 8 | `CreatedBy` | int | NULL |  |  |
| 9 | `UpdatedAt` | datetime2 | NOT NULL |  |  |
| 10 | `UpdatedBy` | int | NULL |  |  |

**Indexes**

| Columns | Unique | Filter | Name |
|---|---|---|---|
| Name | yes |  |  |

## Permissions

The permission catalog: one row per `Module.Action` (kept equal to the code's catalog by a test).  
Entity: `Permission` · Columns: **4**

| # | Column | SQL type | Null | Default / notes | Key |
|---|---|---|---|---|---|
| 1 | `Id` | int | NOT NULL | identity | PK |
| 2 | `Description` | nvarchar(200) | NULL |  |  |
| 3 | `Module` | nvarchar(50) | NOT NULL |  |  |
| 4 | `Name` | nvarchar(100) | NOT NULL |  | UQ |

**Indexes**

| Columns | Unique | Filter | Name |
|---|---|---|---|
| Module |  |  |  |
| Name | yes |  |  |

## UserRoles

Which role a user has (many-to-many table; the service allows one role per user).  
Entity: `UserRole` · Columns: **3**

| # | Column | SQL type | Null | Default / notes | Key |
|---|---|---|---|---|---|
| 1 | `UserId` | int | NOT NULL |  | PK (composite), FK → Users (Cascade) |
| 2 | `RoleId` | int | NOT NULL |  | PK (composite), FK → Roles (Cascade) |
| 3 | `AssignedAt` | datetime2 | NOT NULL | default `SYSUTCDATETIME()` |  |

**Indexes**

| Columns | Unique | Filter | Name |
|---|---|---|---|
| RoleId |  |  |  |

## RolePermissions

Which permissions a role grants (many-to-many).  
Entity: `RolePermission` · Columns: **2**

| # | Column | SQL type | Null | Default / notes | Key |
|---|---|---|---|---|---|
| 1 | `RoleId` | int | NOT NULL |  | PK (composite), FK → Roles (Cascade) |
| 2 | `PermissionId` | int | NOT NULL |  | PK (composite), FK → Permissions (Cascade) |

**Indexes**

| Columns | Unique | Filter | Name |
|---|---|---|---|
| PermissionId |  |  |  |

## StaffProfiles

The person behind a staff login: name, contact and HR details, one per user.  
Entity: `StaffProfile` · Columns: **19**

| # | Column | SQL type | Null | Default / notes | Key |
|---|---|---|---|---|---|
| 1 | `Id` | int | NOT NULL | identity | PK |
| 2 | `BloodGroup` | nvarchar(10) | NULL |  |  |
| 3 | `DateOfBirth` | date | NULL |  |  |
| 4 | `EmergencyContactName` | nvarchar(150) | NULL |  |  |
| 5 | `EmergencyContactPhone` | nvarchar(20) | NULL |  |  |
| 6 | `EmployeeNumber` | nvarchar(30) | NULL |  | UQ (filtered) |
| 7 | `FullName` | nvarchar(150) | NOT NULL |  |  |
| 8 | `Gender` | nvarchar(10) | NULL |  |  |
| 9 | `JoiningDate` | date | NULL |  |  |
| 10 | `Mobile` | nvarchar(30) | NULL |  |  |
| 11 | `Qualification` | nvarchar(150) | NULL |  |  |
| 12 | `UserId` | int | NOT NULL |  | UQ (filtered), FK → Users (Restrict) |
| 13 | `CreatedAt` | datetime2 | NOT NULL | default `SYSUTCDATETIME()` |  |
| 14 | `CreatedBy` | int | NULL |  |  |
| 15 | `UpdatedAt` | datetime2 | NOT NULL | default `SYSUTCDATETIME()` |  |
| 16 | `UpdatedBy` | int | NULL |  |  |
| 17 | `IsDeleted` | bit | NOT NULL | default false |  |
| 18 | `DeletedAt` | datetime2 | NULL |  |  |
| 19 | `DeletedBy` | int | NULL |  |  |

**Indexes**

| Columns | Unique | Filter | Name |
|---|---|---|---|
| EmployeeNumber | yes | `[EmployeeNumber] IS NOT NULL AND [IsDeleted] = 0` | UX_StaffProfiles_EmployeeNumber |
| UserId | yes | `[IsDeleted] = 0` | UX_StaffProfiles_UserId |

**Check constraints**

| Name | Rule |
|---|---|
| CK_StaffProfiles_EmployeeNumberNotBlank | `[EmployeeNumber] IS NULL OR LEN(LTRIM(RTRIM([EmployeeNumber]))) > 0` |
| CK_StaffProfiles_FullNameNotBlank | `LEN(LTRIM(RTRIM([FullName]))) > 0` |
| CK_StaffProfiles_Gender | `[Gender] IS NULL OR [Gender] IN ('Male','Female')` |

## SchoolSettings

The school's own record: one row (name, contacts, time zone, currency, working days).  
Entity: `SchoolSettings` · Columns: **17**

| # | Column | SQL type | Null | Default / notes | Key |
|---|---|---|---|---|---|
| 1 | `Id` | int | NOT NULL |  | PK |
| 2 | `AboutTagline` | nvarchar(300) | NULL |  |  |
| 3 | `AddressText` | nvarchar(300) | NULL |  |  |
| 4 | `CountryId` | int | NULL |  | FK → Countries (Restrict) |
| 5 | `CurrencyCode` | nchar(3) | NOT NULL |  |  |
| 6 | `Email` | nvarchar(100) | NULL |  |  |
| 7 | `Phone` | nvarchar(20) | NULL |  |  |
| 8 | `RowVersion` | rowversion | NOT NULL | concurrency token (rowVersion); rowversion |  |
| 9 | `SchoolName` | nvarchar(150) | NOT NULL |  |  |
| 10 | `ShortName` | nvarchar(30) | NULL |  |  |
| 11 | `TimeZoneId` | nvarchar(64) | NOT NULL |  |  |
| 12 | `Website` | nvarchar(200) | NULL |  |  |
| 13 | `WorkingDays` | tinyint | NOT NULL |  |  |
| 14 | `CreatedAt` | datetime2 | NOT NULL | default `SYSUTCDATETIME()` |  |
| 15 | `CreatedBy` | int | NULL |  |  |
| 16 | `UpdatedAt` | datetime2 | NOT NULL | default `SYSUTCDATETIME()` |  |
| 17 | `UpdatedBy` | int | NULL |  |  |

**Indexes**

| Columns | Unique | Filter | Name |
|---|---|---|---|
| CountryId |  |  |  |

**Check constraints**

| Name | Rule |
|---|---|
| CK_SchoolSettings_CurrencyCode | `LEN([CurrencyCode]) = 3 AND [CurrencyCode] COLLATE Latin1_General_BIN2 NOT LIKE '%[^A-Z]%'` |
| CK_SchoolSettings_SchoolNameNotBlank | `LEN(LTRIM(RTRIM([SchoolName]))) > 0` |
| CK_SchoolSettings_SingleRow | `[Id] = 1` |
| CK_SchoolSettings_TimeZoneNotBlank | `LEN(LTRIM(RTRIM([TimeZoneId]))) > 0` |
| CK_SchoolSettings_WorkingDays | `[WorkingDays] BETWEEN 1 AND 127` |

## RefreshTokens

Hashed refresh tokens for sign-in sessions (rotated; reuse is detected).  
Entity: `RefreshToken` · Columns: **13**

| # | Column | SQL type | Null | Default / notes | Key |
|---|---|---|---|---|---|
| 1 | `Id` | bigint | NOT NULL | identity | PK |
| 2 | `CreatedByIp` | varchar(45) | NULL |  |  |
| 3 | `ExpiresAt` | datetime2 | NOT NULL |  |  |
| 4 | `FamilyExpiresAt` | datetime2 | NOT NULL |  |  |
| 5 | `FamilyId` | uniqueidentifier | NOT NULL |  |  |
| 6 | `IssuedAt` | datetime2 | NOT NULL |  |  |
| 7 | `ReplacedByTokenId` | bigint | NULL |  | FK → RefreshTokens (NoAction) |
| 8 | `RevokedAt` | datetime2 | NULL |  |  |
| 9 | `RevokedReason` | varchar(20) | NULL |  |  |
| 10 | `SecurityStamp` | uniqueidentifier | NOT NULL |  |  |
| 11 | `TokenHash` | binary(32) | NOT NULL |  | UQ |
| 12 | `UserAgent` | nvarchar(256) | NULL |  |  |
| 13 | `UserId` | int | NOT NULL |  | FK → Users (NoAction) |

**Indexes**

| Columns | Unique | Filter | Name |
|---|---|---|---|
| FamilyExpiresAt |  |  | IX_RefreshTokens_FamilyExpiresAt |
| FamilyId |  |  | IX_RefreshTokens_FamilyId |
| ReplacedByTokenId |  |  |  |
| TokenHash | yes |  | UX_RefreshTokens_TokenHash |
| UserId |  | `[RevokedAt] IS NULL` | IX_RefreshTokens_UserId_Active |

**Check constraints**

| Name | Rule |
|---|---|
| CK_RefreshTokens_Expiry | `[ExpiresAt] > [IssuedAt] AND [ExpiresAt] <= [FamilyExpiresAt]` |
| CK_RefreshTokens_Revoked | `([RevokedAt] IS NULL AND [RevokedReason] IS NULL) OR ([RevokedAt] IS NOT NULL AND [RevokedReason] IS NOT NULL)` |
| CK_RefreshTokens_RevokedReason | `[RevokedReason] IS NULL OR [RevokedReason] IN ('Rotated','Logout','LogoutAll','PasswordChanged','ReuseDetected','SessionInvalidated')` |

## AuditLogs

Who changed what: one row per audited change, with sensitive values masked.  
Entity: `AuditLog` · Columns: **9**

| # | Column | SQL type | Null | Default / notes | Key |
|---|---|---|---|---|---|
| 1 | `Id` | bigint | NOT NULL | identity | PK |
| 2 | `Action` | nvarchar(10) | NOT NULL |  |  |
| 3 | `Changes` | nvarchar(max) | NULL |  |  |
| 4 | `OccurredAt` | datetime2 | NOT NULL |  |  |
| 5 | `RecordId` | nvarchar(100) | NOT NULL |  |  |
| 6 | `TableName` | nvarchar(100) | NOT NULL |  |  |
| 7 | `TraceId` | nvarchar(64) | NULL |  |  |
| 8 | `UserId` | int | NULL |  |  |
| 9 | `UserName` | nvarchar(256) | NULL |  |  |

**Indexes**

| Columns | Unique | Filter | Name |
|---|---|---|---|
| OccurredAt |  |  | IX_AuditLogs_OccurredAt |
| UserId, OccurredAt |  |  | IX_AuditLogs_User_Time |
| TableName, RecordId, OccurredAt |  |  | IX_AuditLogs_Table_Record |

**Check constraints**

| Name | Rule |
|---|---|
| CK_AuditLogs_Action | `[Action] IN ('Insert','Update','Delete','Read')` |

## AcademicYears

School years; exactly one has IsCurrent = 1.  
Entity: `AcademicYear` · Columns: **10**

| # | Column | SQL type | Null | Default / notes | Key |
|---|---|---|---|---|---|
| 1 | `Id` | int | NOT NULL | identity | PK |
| 2 | `EndDate` | date | NOT NULL |  |  |
| 3 | `IsCurrent` | bit | NOT NULL |  | UQ (filtered) |
| 4 | `Name` | nvarchar(20) | NOT NULL |  | UQ |
| 5 | `StartDate` | date | NOT NULL |  |  |
| 6 | `Status` | nvarchar(20) | NOT NULL | default "Upcoming" |  |
| 7 | `CreatedAt` | datetime2 | NOT NULL |  |  |
| 8 | `CreatedBy` | int | NULL |  |  |
| 9 | `UpdatedAt` | datetime2 | NOT NULL |  |  |
| 10 | `UpdatedBy` | int | NULL |  |  |

**Indexes**

| Columns | Unique | Filter | Name |
|---|---|---|---|
| IsCurrent | yes | `[IsCurrent] = 1` |  |
| Name | yes |  |  |

**Check constraints**

| Name | Rule |
|---|---|
| CK_AcademicYears_Dates | `[EndDate] > [StartDate]` |

## ClassSections

A class + section in one academic year (capacity, class teacher).  
Entity: `ClassSection` · Columns: **22**

| # | Column | SQL type | Null | Default / notes | Key |
|---|---|---|---|---|---|
| 1 | `Id` | int | NOT NULL | identity | PK |
| 2 | `AcademicYearId` | int | NOT NULL |  | FK → AcademicYears (Restrict) |
| 3 | `Building` | nvarchar(50) | NULL |  |  |
| 4 | `Capacity` | int | NULL |  |  |
| 5 | `ClassTeacherId` | int | NULL |  | FK → Teachers (Restrict) |
| 6 | `Floor` | int | NULL |  |  |
| 7 | `Grade` | int | NULL |  |  |
| 8 | `Medium` | nvarchar(20) | NOT NULL | default "English" |  |
| 9 | `Name` | nvarchar(50) | NOT NULL |  |  |
| 10 | `Room` | nvarchar(30) | NULL |  |  |
| 11 | `RowVersion` | rowversion | NOT NULL | concurrency token (rowVersion); rowversion |  |
| 12 | `Section` | nvarchar(10) | NOT NULL |  |  |
| 13 | `Stage` | nvarchar(20) | NOT NULL | default "Primary" |  |
| 14 | `Status` | nvarchar(20) | NOT NULL | default "Active" |  |
| 15 | `Stream` | nvarchar(20) | NOT NULL | default "General" |  |
| 16 | `CreatedAt` | datetime2 | NOT NULL |  |  |
| 17 | `CreatedBy` | int | NULL |  |  |
| 18 | `UpdatedAt` | datetime2 | NOT NULL |  |  |
| 19 | `UpdatedBy` | int | NULL |  |  |
| 20 | `IsDeleted` | bit | NOT NULL | default false |  |
| 21 | `DeletedAt` | datetime2 | NULL |  |  |
| 22 | `DeletedBy` | int | NULL |  |  |

**Indexes**

| Columns | Unique | Filter | Name |
|---|---|---|---|
| ClassTeacherId |  |  |  |
| AcademicYearId, Name, Section | yes | `[IsDeleted] = 0` |  |

**Check constraints**

| Name | Rule |
|---|---|
| CK_ClassSections_Capacity | `[Capacity] IS NULL OR [Capacity] > 0` |
| CK_ClassSections_Floor | `[Floor] IS NULL OR [Floor] >= 0` |
| CK_ClassSections_Status | `[Status] IN ('Active','Inactive')` |

## Teachers

Teaching data of a user with the Teacher role, one-to-one with a user. Name and phone live on the staff profile.  
Entity: `Teacher` · Columns: **10**

| # | Column | SQL type | Null | Default / notes | Key |
|---|---|---|---|---|---|
| 1 | `Id` | int | NOT NULL | identity | PK |
| 2 | `Specialization` | nvarchar(100) | NULL |  |  |
| 3 | `UserId` | int | NOT NULL |  | UQ (filtered), FK → Users (Restrict) |
| 4 | `CreatedAt` | datetime2 | NOT NULL |  |  |
| 5 | `CreatedBy` | int | NULL |  |  |
| 6 | `UpdatedAt` | datetime2 | NOT NULL |  |  |
| 7 | `UpdatedBy` | int | NULL |  |  |
| 8 | `IsDeleted` | bit | NOT NULL | default false |  |
| 9 | `DeletedAt` | datetime2 | NULL |  |  |
| 10 | `DeletedBy` | int | NULL |  |  |

**Indexes**

| Columns | Unique | Filter | Name |
|---|---|---|---|
| UserId | yes | `[IsDeleted] = 0` |  |

## Subjects

A subject in one class section, taught by at most one teacher.  
Entity: `Subject` · Columns: **20**

| # | Column | SQL type | Null | Default / notes | Key |
|---|---|---|---|---|---|
| 1 | `Id` | int | NOT NULL | identity | PK |
| 2 | `ClassSectionId` | int | NOT NULL |  | FK → ClassSections (Restrict) |
| 3 | `Code` | nvarchar(20) | NOT NULL |  |  |
| 4 | `MaxMarks` | int | NULL |  |  |
| 5 | `Name` | nvarchar(100) | NOT NULL |  |  |
| 6 | `PassMarks` | int | NULL |  |  |
| 7 | `PracticalMax` | int | NULL |  |  |
| 8 | `PracticalPass` | int | NULL |  |  |
| 9 | `Status` | nvarchar(20) | NOT NULL | default "Active" |  |
| 10 | `TeacherId` | int | NULL |  | FK → Teachers (Restrict) |
| 11 | `TheoryMax` | int | NULL |  |  |
| 12 | `TheoryPass` | int | NULL |  |  |
| 13 | `Type` | nvarchar(20) | NOT NULL |  |  |
| 14 | `CreatedAt` | datetime2 | NOT NULL |  |  |
| 15 | `CreatedBy` | int | NULL |  |  |
| 16 | `UpdatedAt` | datetime2 | NOT NULL |  |  |
| 17 | `UpdatedBy` | int | NULL |  |  |
| 18 | `IsDeleted` | bit | NOT NULL | default false |  |
| 19 | `DeletedAt` | datetime2 | NULL |  |  |
| 20 | `DeletedBy` | int | NULL |  |  |

**Indexes**

| Columns | Unique | Filter | Name |
|---|---|---|---|
| ClassSectionId |  |  |  |
| TeacherId |  |  |  |
| Code, ClassSectionId | yes | `[IsDeleted] = 0` |  |

**Check constraints**

| Name | Rule |
|---|---|
| CK_Subjects_MarksByType | `([Type] IN ('Theory','Practical') AND [MaxMarks] IS NOT NULL AND [PassMarks] IS NOT NULL  AND [TheoryMax] IS NULL AND [TheoryPass] IS NULL AND [PracticalMax] IS NULL AND [PracticalPass] IS NULL) OR ([Type] = 'Both' AND [TheoryMax] IS NOT NULL AND [TheoryPass] IS NOT NULL  AND [PracticalMax] IS NOT NULL AND [PracticalPass] IS NOT NULL AND [MaxMarks] IS NULL AND [PassMarks] IS NULL)` |
| CK_Subjects_PassNotAboveMax | `([PassMarks] IS NULL OR [MaxMarks] IS NULL OR [PassMarks] <= [MaxMarks]) AND ([TheoryPass] IS NULL OR [TheoryMax] IS NULL OR [TheoryPass] <= [TheoryMax]) AND ([PracticalPass] IS NULL OR [PracticalMax] IS NULL OR [PracticalPass] <= [PracticalMax])` |
| CK_Subjects_Status | `[Status] IN ('Active','Inactive')` |

## Admissions

Admission applications (Draft, Confirmed, Enrolled, Rejected).  
Entity: `Admission` · Columns: **37**

| # | Column | SQL type | Null | Default / notes | Key |
|---|---|---|---|---|---|
| 1 | `Id` | int | NOT NULL | identity | PK |
| 2 | `AcademicYearId` | int | NOT NULL |  | FK → AcademicYears (Restrict) |
| 3 | `AdmissionDate` | date | NULL |  |  |
| 4 | `AdmissionFee` | decimal(10,2) | NULL |  |  |
| 5 | `AdmissionFeeReference` | nvarchar(30) | NULL |  |  |
| 6 | `AdmissionNumber` | nvarchar(30) | NULL |  |  |
| 7 | `AdmissionType` | nvarchar(20) | NOT NULL |  |  |
| 8 | `AllottedClassSectionId` | int | NULL |  | FK → ClassSections (Restrict) |
| 9 | `AppliedForClassSectionId` | int | NOT NULL |  | FK → ClassSections (Restrict) |
| 10 | `BloodGroup` | nvarchar(10) | NULL |  |  |
| 11 | `Category` | nvarchar(50) | NULL |  |  |
| 12 | `DateOfBirth` | date | NOT NULL |  |  |
| 13 | `Email` | nvarchar(100) | NULL |  |  |
| 14 | `EntryPoint` | nvarchar(50) | NULL |  |  |
| 15 | `FirstName` | nvarchar(50) | NOT NULL |  |  |
| 16 | `Gender` | nvarchar(10) | NOT NULL |  |  |
| 17 | `LastName` | nvarchar(50) | NOT NULL |  |  |
| 18 | `MedicalNotes` | nvarchar(300) | NULL |  |  |
| 19 | `MiddleName` | nvarchar(50) | NULL |  |  |
| 20 | `Phone` | nvarchar(20) | NOT NULL |  |  |
| 21 | `PreviousSchool` | nvarchar(100) | NULL |  |  |
| 22 | `RegNo` | nvarchar(30) | NOT NULL |  | UQ |
| 23 | `RegistrationDate` | date | NOT NULL |  |  |
| 24 | `RejectionReason` | nvarchar(300) | NULL |  |  |
| 25 | `Religion` | nvarchar(50) | NULL |  |  |
| 26 | `Remarks` | nvarchar(300) | NULL |  |  |
| 27 | `RollNumber` | nvarchar(20) | NULL |  |  |
| 28 | `RowVersion` | rowversion | NOT NULL | concurrency token (rowVersion); rowversion |  |
| 29 | `Status` | nvarchar(20) | NOT NULL | default "Registered" |  |
| 30 | `TransportRequired` | bit | NOT NULL | default false |  |
| 31 | `CreatedAt` | datetime2 | NOT NULL |  |  |
| 32 | `CreatedBy` | int | NULL |  |  |
| 33 | `UpdatedAt` | datetime2 | NOT NULL |  |  |
| 34 | `UpdatedBy` | int | NULL |  |  |
| 35 | `IsDeleted` | bit | NOT NULL | default false |  |
| 36 | `DeletedAt` | datetime2 | NULL |  |  |
| 37 | `DeletedBy` | int | NULL |  |  |

**Indexes**

| Columns | Unique | Filter | Name |
|---|---|---|---|
| AllottedClassSectionId |  |  |  |
| AppliedForClassSectionId |  |  |  |
| RegNo | yes |  |  |
| AcademicYearId, Status |  |  |  |

**Check constraints**

| Name | Rule |
|---|---|
| CK_Admissions_AdmissionFee | `[AdmissionFee] IS NULL OR [AdmissionFee] >= 0` |
| CK_Admissions_AdmissionType | `[AdmissionType] IN ('New','Transfer')` |
| CK_Admissions_EnrolledIsComplete | `[Status] <> 'Enrolled' OR ([RollNumber] IS NOT NULL AND [AdmissionNumber] IS NOT NULL AND [AdmissionDate] IS NOT NULL AND [AllottedClassSectionId] IS NOT NULL)` |
| CK_Admissions_RejectedHasReason | `[Status] <> 'Rejected' OR ([RejectionReason] IS NOT NULL AND LEN(LTRIM(RTRIM([RejectionReason]))) > 0)` |
| CK_Admissions_Status | `[Status] IN ('Registered','Admitted','Enrolled','Rejected')` |

## AdmissionGuardians

Father/mother/guardian written on an application (a snapshot, matched to real parents at enrolment).  
Entity: `AdmissionGuardian` · Columns: **12**

| # | Column | SQL type | Null | Default / notes | Key |
|---|---|---|---|---|---|
| 1 | `Id` | int | NOT NULL | identity | PK |
| 2 | `AdmissionId` | int | NOT NULL |  | UQ (filtered), FK → Admissions (Cascade) |
| 3 | `Email` | nvarchar(100) | NULL |  |  |
| 4 | `IsPrimaryContact` | bit | NOT NULL | default false |  |
| 5 | `Mobile` | nvarchar(20) | NULL |  |  |
| 6 | `MobileKey` | nvarchar(20) | NULL | computed `CAST(REPLACE(REPLACE(REPLACE(REPLACE(REPLACE(REPLACE([Mobile],' ',''),'-',''),'+',''),'(',''),')',''),'.','') AS nvarchar(20))` |  |
| 7 | `Name` | nvarchar(100) | NOT NULL |  |  |
| 8 | `RelationType` | nvarchar(30) | NOT NULL |  |  |
| 9 | `CreatedAt` | datetime2 | NOT NULL | default `SYSUTCDATETIME()` |  |
| 10 | `CreatedBy` | int | NULL |  |  |
| 11 | `UpdatedAt` | datetime2 | NOT NULL | default `SYSUTCDATETIME()` |  |
| 12 | `UpdatedBy` | int | NULL |  |  |

**Indexes**

| Columns | Unique | Filter | Name |
|---|---|---|---|
| AdmissionId | yes | `[IsPrimaryContact] = 1` | UX_AdmissionGuardians_Admission_Primary |

**Check constraints**

| Name | Rule |
|---|---|
| CK_AdmissionGuardians_RelationType | `[RelationType] IN ('Father','Mother','Guardian')` |

## Students

The student's current record (academic history lives in StudentEnrollments).  
Entity: `Student` · Columns: **43**

| # | Column | SQL type | Null | Default / notes | Key |
|---|---|---|---|---|---|
| 1 | `Id` | int | NOT NULL | identity | PK |
| 2 | `AdmNo` | nvarchar(30) | NOT NULL |  | UQ |
| 3 | `AdmissionDate` | date | NOT NULL |  |  |
| 4 | `AdmissionId` | int | NULL |  | UQ (filtered), FK → Admissions (Restrict) |
| 5 | `AdmissionType` | nvarchar(20) | NOT NULL | default "New" |  |
| 6 | `Category` | nvarchar(50) | NOT NULL | default "General" |  |
| 7 | `ClassSectionId` | int | NOT NULL |  | FK → ClassSections (Restrict) |
| 8 | `CountryOfBirth` | nvarchar(50) | NULL |  |  |
| 9 | `CurriculumTrack` | nvarchar(50) | NULL |  |  |
| 10 | `CustodyArrangement` | nvarchar(20) | NULL |  |  |
| 11 | `DateOfBirth` | date | NOT NULL |  |  |
| 12 | `EalCode` | nvarchar(30) | NULL |  |  |
| 13 | `Email` | nvarchar(100) | NULL |  |  |
| 14 | `EnglishProficiency` | nvarchar(30) | NULL |  |  |
| 15 | `FeeConcessionPercent` | decimal(5,2) | NULL |  |  |
| 16 | `FirstName` | nvarchar(50) | NOT NULL |  |  |
| 17 | `Gender` | nvarchar(10) | NOT NULL |  |  |
| 18 | `HomeLanguage` | nvarchar(50) | NULL |  |  |
| 19 | `House` | nvarchar(30) | NULL |  |  |
| 20 | `LastName` | nvarchar(50) | NOT NULL |  |  |
| 21 | `MediaConsent` | bit | NOT NULL | default true |  |
| 22 | `MiddleName` | nvarchar(50) | NULL |  |  |
| 23 | `Mobile` | nvarchar(20) | NULL |  |  |
| 24 | `MotherTongue` | nvarchar(50) | NULL |  |  |
| 25 | `Nationality` | nvarchar(50) | NULL |  |  |
| 26 | `PhotoUrl` | nvarchar(500) | NULL |  |  |
| 27 | `PreferredName` | nvarchar(50) | NULL |  |  |
| 28 | `PreviousSchool` | nvarchar(100) | NULL |  |  |
| 29 | `Religion` | nvarchar(50) | NULL |  |  |
| 30 | `RollNumber` | nvarchar(20) | NOT NULL |  |  |
| 31 | `RowVersion` | rowversion | NOT NULL | concurrency token (rowVersion); rowversion |  |
| 32 | `SecondNationality` | nvarchar(50) | NULL |  |  |
| 33 | `Status` | nvarchar(20) | NOT NULL | default "Active" |  |
| 34 | `TransportRequired` | bit | NOT NULL | default false |  |
| 35 | `TransportRoute` | nvarchar(100) | NULL |  |  |
| 36 | `UserId` | int | NULL |  | UQ (filtered), FK → Users (Restrict) |
| 37 | `CreatedAt` | datetime2 | NOT NULL |  |  |
| 38 | `CreatedBy` | int | NULL |  |  |
| 39 | `UpdatedAt` | datetime2 | NOT NULL |  |  |
| 40 | `UpdatedBy` | int | NULL |  |  |
| 41 | `IsDeleted` | bit | NOT NULL | default false |  |
| 42 | `DeletedAt` | datetime2 | NULL |  |  |
| 43 | `DeletedBy` | int | NULL |  |  |

**Indexes**

| Columns | Unique | Filter | Name |
|---|---|---|---|
| AdmNo | yes |  |  |
| AdmissionId | yes | `[AdmissionId] IS NOT NULL` |  |
| UserId | yes | `[UserId] IS NOT NULL AND [IsDeleted] = 0` | UX_Students_UserId |
| ClassSectionId, Status |  |  | IX_Students_ClassSectionId_Status |

**Check constraints**

| Name | Rule |
|---|---|
| CK_Students_AdmissionType | `[AdmissionType] IN ('New','Transfer')` |
| CK_Students_FeeConcession | `[FeeConcessionPercent] IS NULL OR ([FeeConcessionPercent] >= 0 AND [FeeConcessionPercent] <= 100)` |
| CK_Students_Status | `[Status] IN ('Active','Inactive','Left')` |

## StudentEnrollments

Academic history: one row per class/section period; one Active period per student.  
Entity: `StudentEnrollment` · Columns: **13**

| # | Column | SQL type | Null | Default / notes | Key |
|---|---|---|---|---|---|
| 1 | `Id` | int | NOT NULL | identity | PK |
| 2 | `AcademicYearId` | int | NOT NULL |  | FK → AcademicYears (Restrict) |
| 3 | `ClassSectionId` | int | NOT NULL |  | FK → ClassSections (Restrict) |
| 4 | `EndDate` | date | NULL |  |  |
| 5 | `Remarks` | nvarchar(300) | NULL |  |  |
| 6 | `RollNumber` | nvarchar(20) | NOT NULL |  |  |
| 7 | `StartDate` | date | NOT NULL |  |  |
| 8 | `Status` | nvarchar(20) | NOT NULL | default "Active" |  |
| 9 | `StudentId` | int | NOT NULL |  | UQ (filtered), FK → Students (Restrict) |
| 10 | `CreatedAt` | datetime2 | NOT NULL | default `SYSUTCDATETIME()` |  |
| 11 | `CreatedBy` | int | NULL |  |  |
| 12 | `UpdatedAt` | datetime2 | NOT NULL | default `SYSUTCDATETIME()` |  |
| 13 | `UpdatedBy` | int | NULL |  |  |

**Indexes**

| Columns | Unique | Filter | Name |
|---|---|---|---|
| AcademicYearId |  |  |  |
| StudentId | yes | `[Status] = 'Active'` | UX_StudentEnrollments_Student_Active |
| ClassSectionId, RollNumber | yes | `[Status] = 'Active'` | UX_StudentEnrollments_Section_Roll_Active |
| ClassSectionId, Status |  |  | IX_StudentEnrollments_Section_Status |
| StudentId, AcademicYearId |  |  | IX_StudentEnrollments_Student_Year |

**Check constraints**

| Name | Rule |
|---|---|
| CK_StudentEnrollments_EndDate | `([Status] = 'Active' AND [EndDate] IS NULL) OR ([Status] <> 'Active' AND [EndDate] IS NOT NULL AND [EndDate] >= [StartDate])` |
| CK_StudentEnrollments_Status | `[Status] IN ('Active','Promoted','Repeated','Transferred','Left')` |

## StudentHealth

Health and insurance details, one row per student.  
Entity: `StudentHealth` · Columns: **12**

| # | Column | SQL type | Null | Default / notes | Key |
|---|---|---|---|---|---|
| 1 | `StudentId` | int | NOT NULL |  | PK, FK → Students (Cascade) |
| 2 | `Allergies` | nvarchar(300) | NULL |  |  |
| 3 | `BloodGroup` | nvarchar(10) | NULL |  |  |
| 4 | `DietaryRequirements` | nvarchar(200) | NULL |  |  |
| 5 | `InsurancePolicyExpiry` | date | NULL |  |  |
| 6 | `InsuranceProvider` | nvarchar(100) | NULL |  |  |
| 7 | `MedicalNotes` | nvarchar(500) | NULL |  |  |
| 8 | `SpecialEducationalNeeds` | nvarchar(500) | NULL |  |  |
| 9 | `CreatedAt` | datetime2 | NOT NULL | default `SYSUTCDATETIME()` |  |
| 10 | `CreatedBy` | int | NULL |  |  |
| 11 | `UpdatedAt` | datetime2 | NOT NULL | default `SYSUTCDATETIME()` |  |
| 12 | `UpdatedBy` | int | NULL |  |  |

## StudentIdentityDocuments

Identity numbers and passport/visa details (sensitive), one row per student.  
Entity: `StudentIdentityDocument` · Columns: **10**

| # | Column | SQL type | Null | Default / notes | Key |
|---|---|---|---|---|---|
| 1 | `StudentId` | int | NOT NULL |  | PK, FK → Students (Cascade) |
| 2 | `AadhaarNumber` | nvarchar(12) | NULL |  | UQ (filtered) |
| 3 | `PassportExpiry` | date | NULL |  |  |
| 4 | `PassportNumber` | nvarchar(30) | NULL |  |  |
| 5 | `VisaExpiry` | date | NULL |  |  |
| 6 | `VisaType` | nvarchar(30) | NULL |  |  |
| 7 | `CreatedAt` | datetime2 | NOT NULL | default `SYSUTCDATETIME()` |  |
| 8 | `CreatedBy` | int | NULL |  |  |
| 9 | `UpdatedAt` | datetime2 | NOT NULL | default `SYSUTCDATETIME()` |  |
| 10 | `UpdatedBy` | int | NULL |  |  |

**Indexes**

| Columns | Unique | Filter | Name |
|---|---|---|---|
| AadhaarNumber | yes | `[AadhaarNumber] IS NOT NULL` | UX_StudentIdentityDocuments_Aadhaar |

**Check constraints**

| Name | Rule |
|---|---|
| CK_StudentIdentityDocuments_Aadhaar | `[AadhaarNumber] IS NULL OR (LEN([AadhaarNumber]) = 12 AND [AadhaarNumber] NOT LIKE '%[^0-9]%')` |

## StudentPickupPersons

People authorised to collect the child.  
Entity: `StudentPickupPerson` · Columns: **10**

| # | Column | SQL type | Null | Default / notes | Key |
|---|---|---|---|---|---|
| 1 | `Id` | int | NOT NULL | identity | PK |
| 2 | `IdNote` | nvarchar(100) | NULL |  |  |
| 3 | `Name` | nvarchar(100) | NOT NULL |  |  |
| 4 | `Phone` | nvarchar(20) | NOT NULL |  |  |
| 5 | `Relation` | nvarchar(50) | NOT NULL |  |  |
| 6 | `StudentId` | int | NOT NULL |  | FK → Students (Cascade) |
| 7 | `CreatedAt` | datetime2 | NOT NULL | default `SYSUTCDATETIME()` |  |
| 8 | `CreatedBy` | int | NULL |  |  |
| 9 | `UpdatedAt` | datetime2 | NOT NULL | default `SYSUTCDATETIME()` |  |
| 10 | `UpdatedBy` | int | NULL |  |  |

**Indexes**

| Columns | Unique | Filter | Name |
|---|---|---|---|
| StudentId |  |  | IX_StudentPickupPersons_StudentId |

## Parents

Parent/guardian people, one row per real person.  
Entity: `Parent` · Columns: **33**

| # | Column | SQL type | Null | Default / notes | Key |
|---|---|---|---|---|---|
| 1 | `Id` | int | NOT NULL | identity | PK |
| 2 | `BillingContact` | bit | NOT NULL |  |  |
| 3 | `CountryOfResidence` | nvarchar(50) | NULL |  |  |
| 4 | `Email` | nvarchar(100) | NULL |  |  |
| 5 | `EmergencyOnly` | bit | NOT NULL |  |  |
| 6 | `Employer` | nvarchar(100) | NULL |  |  |
| 7 | `JobTitle` | nvarchar(100) | NULL |  |  |
| 8 | `Mobile` | nvarchar(20) | NOT NULL |  |  |
| 9 | `MobileKey` | nvarchar(20) | NOT NULL | computed `CAST(REPLACE(REPLACE(REPLACE(REPLACE(REPLACE(REPLACE([Mobile],' ',''),'-',''),'+',''),'(',''),')',''),'.','') AS nvarchar(20))` |  |
| 10 | `Name` | nvarchar(100) | NOT NULL |  |  |
| 11 | `Nationality` | nvarchar(50) | NULL |  |  |
| 12 | `NotifyAttendance` | bit | NOT NULL | default true |  |
| 13 | `NotifyDiscipline` | bit | NOT NULL | default true |  |
| 14 | `NotifyExams` | bit | NOT NULL | default true |  |
| 15 | `NotifyFees` | bit | NOT NULL | default true |  |
| 16 | `NotifyNotices` | bit | NOT NULL | default true |  |
| 17 | `Occupation` | nvarchar(100) | NULL |  |  |
| 18 | `PreferredContactMethod` | nvarchar(30) | NULL |  |  |
| 19 | `PreferredLanguage` | nvarchar(30) | NULL |  |  |
| 20 | `RowVersion` | rowversion | NOT NULL | concurrency token (rowVersion); rowversion |  |
| 21 | `Status` | nvarchar(20) | NOT NULL | default "Active" |  |
| 22 | `Timezone` | nvarchar(50) | NULL |  |  |
| 23 | `UserId` | int | NULL |  | UQ (filtered), FK → Users (Restrict) |
| 24 | `Whatsapp` | nvarchar(20) | NULL |  |  |
| 25 | `WorkEmail` | nvarchar(100) | NULL |  |  |
| 26 | `WorkPhone` | nvarchar(20) | NULL |  |  |
| 27 | `CreatedAt` | datetime2 | NOT NULL |  |  |
| 28 | `CreatedBy` | int | NULL |  |  |
| 29 | `UpdatedAt` | datetime2 | NOT NULL |  |  |
| 30 | `UpdatedBy` | int | NULL |  |  |
| 31 | `IsDeleted` | bit | NOT NULL | default false |  |
| 32 | `DeletedAt` | datetime2 | NULL |  |  |
| 33 | `DeletedBy` | int | NULL |  |  |

**Indexes**

| Columns | Unique | Filter | Name |
|---|---|---|---|
| MobileKey |  |  | IX_Parents_MobileKey |
| UserId | yes | `[UserId] IS NOT NULL AND [IsDeleted] = 0` | UX_Parents_UserId |

**Check constraints**

| Name | Rule |
|---|---|
| CK_Parents_Status | `[Status] IN ('Active','Inactive')` |

## StudentGuardians

Links a student to a parent, with the relation and the primary-contact flag.  
Entity: `StudentGuardian` · Columns: **8**

| # | Column | SQL type | Null | Default / notes | Key |
|---|---|---|---|---|---|
| 1 | `StudentId` | int | NOT NULL |  | PK (composite), UQ (filtered), FK → Students (Cascade) |
| 2 | `ParentId` | int | NOT NULL |  | PK (composite), FK → Parents (Cascade) |
| 3 | `IsPrimaryContact` | bit | NOT NULL |  |  |
| 4 | `RelationType` | nvarchar(30) | NOT NULL |  |  |
| 5 | `CreatedAt` | datetime2 | NOT NULL | default `SYSUTCDATETIME()` |  |
| 6 | `CreatedBy` | int | NULL |  |  |
| 7 | `UpdatedAt` | datetime2 | NOT NULL | default `SYSUTCDATETIME()` |  |
| 8 | `UpdatedBy` | int | NULL |  |  |

**Indexes**

| Columns | Unique | Filter | Name |
|---|---|---|---|
| ParentId |  |  |  |
| StudentId | yes | `[IsPrimaryContact] = 1` | UX_StudentGuardians_Student_Primary |

**Check constraints**

| Name | Rule |
|---|---|
| CK_StudentGuardians_RelationType | `[RelationType] IN ('Father','Mother','Guardian')` |

## Addresses

Postal addresses for a student, parent, application or staff member (exactly one owner).  
Entity: `Address` · Columns: **17**

| # | Column | SQL type | Null | Default / notes | Key |
|---|---|---|---|---|---|
| 1 | `Id` | int | NOT NULL | identity | PK |
| 2 | `AddressType` | nvarchar(20) | NOT NULL |  |  |
| 3 | `AdmissionId` | int | NULL |  | UQ (filtered), FK → Admissions (Cascade) |
| 4 | `City` | nvarchar(60) | NOT NULL |  |  |
| 5 | `CountryId` | int | NOT NULL |  | FK → Countries (Restrict), FK → CountryRegions with CountryId, RegionId (Restrict) |
| 6 | `IsPrimary` | bit | NOT NULL | default false |  |
| 7 | `Line1` | nvarchar(200) | NOT NULL |  |  |
| 8 | `Line2` | nvarchar(200) | NULL |  |  |
| 9 | `ParentId` | int | NULL |  | UQ (filtered), FK → Parents (Cascade) |
| 10 | `PostalCode` | nvarchar(12) | NULL |  |  |
| 11 | `RegionId` | int | NULL |  | FK → CountryRegions with CountryId, RegionId (Restrict) |
| 12 | `StaffProfileId` | int | NULL |  | UQ (filtered), FK → StaffProfiles (Cascade) |
| 13 | `StudentId` | int | NULL |  | UQ (filtered), FK → Students (Cascade) |
| 14 | `CreatedAt` | datetime2 | NOT NULL | default `SYSUTCDATETIME()` |  |
| 15 | `CreatedBy` | int | NULL |  |  |
| 16 | `UpdatedAt` | datetime2 | NOT NULL | default `SYSUTCDATETIME()` |  |
| 17 | `UpdatedBy` | int | NULL |  |  |

**Indexes**

| Columns | Unique | Filter | Name |
|---|---|---|---|
| AdmissionId | yes | `[AdmissionId] IS NOT NULL AND [IsPrimary] = 1` | UX_Addresses_Admission_Primary |
| ParentId | yes | `[ParentId] IS NOT NULL AND [IsPrimary] = 1` | UX_Addresses_Parent_Primary |
| StaffProfileId | yes | `[StaffProfileId] IS NOT NULL AND [IsPrimary] = 1` | UX_Addresses_Staff_Primary |
| StudentId | yes | `[StudentId] IS NOT NULL AND [IsPrimary] = 1` | UX_Addresses_Student_Primary |
| AdmissionId, AddressType | yes | `[AdmissionId] IS NOT NULL` | UX_Addresses_Admission_Type |
| CountryId, RegionId |  |  |  |
| ParentId, AddressType | yes | `[ParentId] IS NOT NULL` | UX_Addresses_Parent_Type |
| StaffProfileId, AddressType | yes | `[StaffProfileId] IS NOT NULL` | UX_Addresses_Staff_Type |
| StudentId, AddressType | yes | `[StudentId] IS NOT NULL` | UX_Addresses_Student_Type |

**Check constraints**

| Name | Rule |
|---|---|
| CK_Addresses_AddressType | `[AddressType] IN ('Home','Permanent','Mailing')` |
| CK_Addresses_CityNotBlank | `LEN(LTRIM(RTRIM([City]))) > 0` |
| CK_Addresses_ExactlyOneOwner | `(CASE WHEN [StudentId] IS NULL THEN 0 ELSE 1 END) + (CASE WHEN [ParentId] IS NULL THEN 0 ELSE 1 END) + (CASE WHEN [AdmissionId] IS NULL THEN 0 ELSE 1 END) + (CASE WHEN [StaffProfileId] IS NULL THEN 0 ELSE 1 END) = 1` |
| CK_Addresses_Line1NotBlank | `LEN(LTRIM(RTRIM([Line1]))) > 0` |

## Countries

Reference list of countries (seeded).  
Entity: `Country` · Columns: **9**

| # | Column | SQL type | Null | Default / notes | Key |
|---|---|---|---|---|---|
| 1 | `Id` | int | NOT NULL | identity | PK |
| 2 | `Code` | nchar(2) | NOT NULL |  | UQ |
| 3 | `IsActive` | bit | NOT NULL | default true |  |
| 4 | `Name` | nvarchar(60) | NOT NULL |  | UQ |
| 5 | `PostalCodeHint` | nvarchar(60) | NULL |  |  |
| 6 | `PostalCodePattern` | nvarchar(100) | NULL |  |  |
| 7 | `PostalCodeRequired` | bit | NOT NULL | default false |  |
| 8 | `RegionLabel` | nvarchar(30) | NOT NULL |  |  |
| 9 | `SortOrder` | int | NOT NULL | default 0 |  |

**Indexes**

| Columns | Unique | Filter | Name |
|---|---|---|---|
| Code | yes |  | UX_Countries_Code |
| Name | yes |  | UX_Countries_Name |

**Check constraints**

| Name | Rule |
|---|---|
| CK_Countries_Code | `LEN([Code]) = 2 AND [Code] = UPPER([Code])` |

## CountryRegions

States / regions of a country (seeded), used by address forms.  
Entity: `CountryRegion` · Columns: **5**

| # | Column | SQL type | Null | Default / notes | Key |
|---|---|---|---|---|---|
| 1 | `Id` | int | NOT NULL | identity | PK |
| 2 | `CountryId` | int | NOT NULL |  | FK → Countries (Restrict) |
| 3 | `IsActive` | bit | NOT NULL | default true |  |
| 4 | `Name` | nvarchar(60) | NOT NULL |  |  |
| 5 | `SortOrder` | int | NOT NULL | default 0 |  |

**Indexes**

| Columns | Unique | Filter | Name |
|---|---|---|---|
| CountryId, Name | yes |  | UX_CountryRegions_Country_Name |

## DocumentTypes

Reference list of document types (birth certificate, passport...), with sensitivity and expiry flags.  
Entity: `DocumentType` · Columns: **9**

| # | Column | SQL type | Null | Default / notes | Key |
|---|---|---|---|---|---|
| 1 | `Id` | int | NOT NULL | identity | PK |
| 2 | `Code` | nvarchar(40) | NOT NULL |  | UQ |
| 3 | `ForStaff` | bit | NOT NULL | default false |  |
| 4 | `ForStudents` | bit | NOT NULL | default true |  |
| 5 | `HasExpiry` | bit | NOT NULL | default false |  |
| 6 | `IsActive` | bit | NOT NULL | default true |  |
| 7 | `IsSensitive` | bit | NOT NULL | default false |  |
| 8 | `Name` | nvarchar(80) | NOT NULL |  |  |
| 9 | `SortOrder` | int | NOT NULL | default 0 |  |

**Indexes**

| Columns | Unique | Filter | Name |
|---|---|---|---|
| Code | yes |  | UX_DocumentTypes_Code |

**Check constraints**

| Name | Rule |
|---|---|
| CK_DocumentTypes_CodeNotBlank | `LEN(LTRIM(RTRIM([Code]))) > 0` |

## Documents

Uploaded files (PDF/JPG/PNG) of a student or an application, with their review status.  
Entity: `Document` · Columns: **24**

| # | Column | SQL type | Null | Default / notes | Key |
|---|---|---|---|---|---|
| 1 | `Id` | int | NOT NULL | identity | PK |
| 2 | `AdmissionId` | int | NULL |  | FK → Admissions (Restrict) |
| 3 | `ContentType` | nvarchar(50) | NOT NULL |  |  |
| 4 | `DocumentTypeId` | int | NOT NULL |  | FK → DocumentTypes (Restrict) |
| 5 | `ExpiryDate` | date | NULL |  |  |
| 6 | `FilePurgedAt` | datetime2 | NULL |  |  |
| 7 | `OriginalFileName` | nvarchar(255) | NOT NULL |  |  |
| 8 | `RejectionReason` | nvarchar(300) | NULL |  |  |
| 9 | `RowVersion` | rowversion | NOT NULL | concurrency token (rowVersion); rowversion |  |
| 10 | `Sha256` | binary(32) | NOT NULL |  |  |
| 11 | `SizeBytes` | int | NOT NULL |  |  |
| 12 | `Status` | nvarchar(20) | NOT NULL | default "Pending" |  |
| 13 | `StorageKey` | nvarchar(200) | NOT NULL |  | UQ |
| 14 | `StudentId` | int | NULL |  | FK → Students (Restrict) |
| 15 | `Title` | nvarchar(120) | NULL |  |  |
| 16 | `VerifiedAt` | datetime2 | NULL |  |  |
| 17 | `VerifiedBy` | int | NULL |  | FK → Users (Restrict) |
| 18 | `CreatedAt` | datetime2 | NOT NULL | default `SYSUTCDATETIME()` |  |
| 19 | `CreatedBy` | int | NULL |  |  |
| 20 | `UpdatedAt` | datetime2 | NOT NULL | default `SYSUTCDATETIME()` |  |
| 21 | `UpdatedBy` | int | NULL |  |  |
| 22 | `IsDeleted` | bit | NOT NULL | default false |  |
| 23 | `DeletedAt` | datetime2 | NULL |  |  |
| 24 | `DeletedBy` | int | NULL |  |  |

**Indexes**

| Columns | Unique | Filter | Name |
|---|---|---|---|
| AdmissionId |  | `[AdmissionId] IS NOT NULL AND [IsDeleted] = 0` | IX_Documents_AdmissionId |
| DeletedAt |  | `[IsDeleted] = 1 AND [FilePurgedAt] IS NULL` | IX_Documents_PurgeQueue |
| DocumentTypeId |  |  |  |
| ExpiryDate |  | `[ExpiryDate] IS NOT NULL AND [IsDeleted] = 0` | IX_Documents_ExpiryDate |
| StorageKey | yes |  | UX_Documents_StorageKey |
| StudentId |  | `[StudentId] IS NOT NULL AND [IsDeleted] = 0` | IX_Documents_StudentId |
| StudentId |  |  |  |
| VerifiedBy |  |  |  |
| AdmissionId, Sha256 | yes | `[AdmissionId] IS NOT NULL AND [IsDeleted] = 0` | UX_Documents_Admission_Sha256 |
| Status, CreatedAt |  | `[IsDeleted] = 0` | IX_Documents_Status_CreatedAt |
| StudentId, Sha256 | yes | `[StudentId] IS NOT NULL AND [IsDeleted] = 0` | UX_Documents_Student_Sha256 |

**Check constraints**

| Name | Rule |
|---|---|
| CK_Documents_ContentType | `[ContentType] IN ('application/pdf','image/jpeg','image/png')` |
| CK_Documents_ExactlyOneOwner | `(CASE WHEN [StudentId] IS NULL THEN 0 ELSE 1 END) + (CASE WHEN [AdmissionId] IS NULL THEN 0 ELSE 1 END) = 1` |
| CK_Documents_NoSelfVerify | `[VerifiedBy] IS NULL OR [CreatedBy] IS NULL OR [VerifiedBy] <> [CreatedBy]` |
| CK_Documents_PendingHasNoReviewer | `[Status] <> 'Pending' OR ([VerifiedBy] IS NULL AND [VerifiedAt] IS NULL AND [RejectionReason] IS NULL)` |
| CK_Documents_RejectedHasReason | `[Status] <> 'Rejected' OR LEN(LTRIM(RTRIM([RejectionReason]))) > 0` |
| CK_Documents_ReviewedHasReviewer | `[Status] = 'Pending' OR ([VerifiedBy] IS NOT NULL AND [VerifiedAt] IS NOT NULL)` |
| CK_Documents_SizePositive | `[SizeBytes] > 0` |
| CK_Documents_Status | `[Status] IN ('Pending','Verified','Rejected')` |

## AttendanceSessions

One roll call: class + date (+ optional subject).  
Entity: `AttendanceSession` · Columns: **9**

| # | Column | SQL type | Null | Default / notes | Key |
|---|---|---|---|---|---|
| 1 | `Id` | int | NOT NULL | identity | PK |
| 2 | `ClassSectionId` | int | NOT NULL |  | FK → ClassSections (Restrict) |
| 3 | `Date` | date | NOT NULL |  |  |
| 4 | `SubjectId` | int | NULL |  | FK → Subjects (Restrict) |
| 5 | `TakenByUserId` | int | NOT NULL |  | FK → Users (Restrict) |
| 6 | `CreatedAt` | datetime2 | NOT NULL |  |  |
| 7 | `CreatedBy` | int | NULL |  |  |
| 8 | `UpdatedAt` | datetime2 | NOT NULL |  |  |
| 9 | `UpdatedBy` | int | NULL |  |  |

**Indexes**

| Columns | Unique | Filter | Name |
|---|---|---|---|
| Date |  |  |  |
| SubjectId |  |  |  |
| TakenByUserId |  |  |  |
| ClassSectionId, Date | yes | `[SubjectId] IS NULL` | UX_AttendanceSessions_Class_Date_Daily |
| ClassSectionId, Date, SubjectId | yes | `[SubjectId] IS NOT NULL` | UX_AttendanceSessions_Class_Date_Subject |

## AttendanceRecords

One student's status in one attendance session.  
Entity: `AttendanceRecord` · Columns: **9**

| # | Column | SQL type | Null | Default / notes | Key |
|---|---|---|---|---|---|
| 1 | `Id` | int | NOT NULL | identity | PK |
| 2 | `Remarks` | nvarchar(250) | NULL |  |  |
| 3 | `SessionId` | int | NOT NULL |  | FK → AttendanceSessions (Cascade) |
| 4 | `Status` | nvarchar(10) | NOT NULL |  |  |
| 5 | `StudentId` | int | NOT NULL |  | FK → Students (Restrict) |
| 6 | `CreatedAt` | datetime2 | NOT NULL | default `SYSUTCDATETIME()` |  |
| 7 | `CreatedBy` | int | NULL |  |  |
| 8 | `UpdatedAt` | datetime2 | NOT NULL | default `SYSUTCDATETIME()` |  |
| 9 | `UpdatedBy` | int | NULL |  |  |

**Indexes**

| Columns | Unique | Filter | Name |
|---|---|---|---|
| SessionId, StudentId | yes |  |  |
| StudentId, SessionId |  |  |  |

**Check constraints**

| Name | Rule |
|---|---|
| CK_AttendanceRecords_Status | `[Status] IN ('Present','Absent','Late','Half Day','Leave')` |
