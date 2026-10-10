# Data Dictionary (every column)

Generated from `ApplicationDbContextModelSnapshot.cs` (the EF model after migration `ContractStudentAdmission`), so it matches the real database.
If you add a migration, regenerate or update this file.

Legend: **PK** primary key · **FK** foreign key (→ table, delete behaviour) · **UQ** single-column unique · **NULL** nullable · Default shown when the model sets one.
Total: **20 tables, 277 columns**.

Audit columns (`CreatedAt`, `UpdatedAt`, `CreatedBy`, `UpdatedBy`) and soft-delete columns (`IsDeleted`, `DeletedAt`, `DeletedBy`) repeat on many tables; they are listed in each table for completeness.

## Contents

1. [Users](#users) — Login accounts (staff now; student/parent logins later).
2. [Roles](#roles) — Seeded roles: Admin, Supervisor, Clerk, Teacher, Student, Parent.
3. [Permissions](#permissions) — One row per permission (seeded, ids 1-11); the name goes into the JWT.
4. [UserRoles](#userroles) — Which roles a user has (many-to-many).
5. [RolePermissions](#rolepermissions) — Which permissions a role has (many-to-many).
6. [AcademicYears](#academicyears) — School years; exactly one has IsCurrent = 1.
7. [ClassSections](#classsections) — A class + section in one academic year.
8. [Teachers](#teachers) — Teacher profile, one-to-one with a user.
9. [Subjects](#subjects) — A subject in one class section, taught by at most one teacher.
10. [Admissions](#admissions) — Admission application pipeline (Registered, Admitted, Enrolled, Rejected).
11. [AdmissionGuardians](#admissionguardians) — Father/mother/guardian written on an application (snapshot).
12. [Students](#students) — The student's current record (history lives in StudentEnrollments).
13. [StudentEnrollments](#studentenrollments) — Academic history: one row per class/section period.
14. [StudentHealth](#studenthealth) — Health and insurance details, one row per student.
15. [StudentIdentityDocuments](#studentidentitydocuments) — Aadhaar, passport, visa (sensitive), one row per student.
16. [StudentPickupPersons](#studentpickuppersons) — People authorised to collect the child.
17. [Parents](#parents) — Parent/guardian people, one row per real person.
18. [StudentGuardians](#studentguardians) — Link student to parent, with relation and primary-contact flag.
19. [AttendanceSessions](#attendancesessions) — One roll call: class + date (+ optional subject).
20. [AttendanceRecords](#attendancerecords) — One student's status in one session.


## Users

Login accounts (staff now; student/parent logins later).  
Entity: `User` · Columns: **13**

| # | Column | SQL type | Null | Default / notes | Key |
|---|---|---|---|---|---|
| 1 | `Id` | int | NOT NULL | identity | PK |
| 2 | `CreatedBy` | int | NULL |  | FK → Users (NoAction) |
| 3 | `DeletedBy` | int | NULL |  | FK → Users (NoAction) |
| 4 | `UpdatedBy` | int | NULL |  | FK → Users (NoAction) |
| 5 | `Email` | nvarchar(100) | NOT NULL |  | UQ |
| 6 | `LastLoginAt` | datetime2 | NULL |  |  |
| 7 | `PasswordHash` | nvarchar(max) | NOT NULL |  |  |
| 8 | `Status` | nvarchar(20) | NOT NULL | default "Active" |  |
| 9 | `Username` | nvarchar(50) | NOT NULL |  | UQ |
| 10 | `CreatedAt` | datetime2 | NOT NULL |  |  |
| 11 | `DeletedAt` | datetime2 | NULL |  |  |
| 12 | `IsDeleted` | bit | NOT NULL | default false |  |
| 13 | `UpdatedAt` | datetime2 | NOT NULL |  |  |

**Indexes**

| Columns | Unique | Filter | Name |
|---|---|---|---|
| CreatedBy |  |  |  |
| DeletedBy |  |  |  |
| Email | yes |  |  |
| UpdatedBy |  |  |  |
| Username | yes |  |  |

**Check constraints**

- `CK_Users_Status`: `[Status] IN ('Active', 'Inactive', 'Suspended')`

## Roles

Seeded roles: Admin, Supervisor, Clerk, Teacher, Student, Parent.  
Entity: `Role` · Columns: **3**

| # | Column | SQL type | Null | Default / notes | Key |
|---|---|---|---|---|---|
| 1 | `Id` | int | NOT NULL | identity | PK |
| 2 | `Description` | nvarchar(200) | NULL |  |  |
| 3 | `Name` | nvarchar(30) | NOT NULL |  | UQ |

**Indexes**

| Columns | Unique | Filter | Name |
|---|---|---|---|
| Name | yes |  |  |

## Permissions

One row per permission (seeded, ids 1-11); the name goes into the JWT.  
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

Which roles a user has (many-to-many).  
Entity: `UserRole` · Columns: **3**

| # | Column | SQL type | Null | Default / notes | Key |
|---|---|---|---|---|---|
| 1 | `RoleId` | int | NOT NULL |  | PK, FK → Roles (Cascade) |
| 2 | `UserId` | int | NOT NULL |  | PK, FK → Users (Cascade) |
| 3 | `AssignedAt` | datetime2 | NOT NULL | default SYSUTCDATETIME() |  |

**Indexes**

| Columns | Unique | Filter | Name |
|---|---|---|---|
| RoleId |  |  |  |

## RolePermissions

Which permissions a role has (many-to-many).  
Entity: `RolePermission` · Columns: **2**

| # | Column | SQL type | Null | Default / notes | Key |
|---|---|---|---|---|---|
| 1 | `PermissionId` | int | NOT NULL |  | PK, FK → Permissions (Cascade) |
| 2 | `RoleId` | int | NOT NULL |  | PK, FK → Roles (Cascade) |

**Indexes**

| Columns | Unique | Filter | Name |
|---|---|---|---|
| PermissionId |  |  |  |

## AcademicYears

School years; exactly one has IsCurrent = 1.  
Entity: `AcademicYear` · Columns: **10**

| # | Column | SQL type | Null | Default / notes | Key |
|---|---|---|---|---|---|
| 1 | `Id` | int | NOT NULL | identity | PK |
| 2 | `EndDate` | date | NOT NULL |  |  |
| 3 | `IsCurrent` | bit | NOT NULL |  |  |
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
| IsCurrent | yes | [IsCurrent] = 1 |  |
| Name | yes |  |  |

## ClassSections

A class + section in one academic year.  
Entity: `ClassSection` · Columns: **21**

| # | Column | SQL type | Null | Default / notes | Key |
|---|---|---|---|---|---|
| 1 | `Id` | int | NOT NULL | identity | PK |
| 2 | `AcademicYearId` | int | NOT NULL |  | FK → AcademicYears (Restrict) |
| 3 | `ClassTeacherId` | int | NULL |  | FK → Teachers (Restrict) |
| 4 | `Building` | nvarchar(50) | NULL |  |  |
| 5 | `Capacity` | int | NULL |  |  |
| 6 | `Floor` | int | NULL |  |  |
| 7 | `Grade` | int | NULL |  |  |
| 8 | `Medium` | nvarchar(20) | NOT NULL | default "English" |  |
| 9 | `Name` | nvarchar(50) | NOT NULL |  |  |
| 10 | `Room` | nvarchar(30) | NULL |  |  |
| 11 | `Section` | nvarchar(10) | NOT NULL |  |  |
| 12 | `Stage` | nvarchar(20) | NOT NULL | default "Primary" |  |
| 13 | `Status` | nvarchar(20) | NOT NULL | default "Active" |  |
| 14 | `Stream` | nvarchar(20) | NOT NULL | default "General" |  |
| 15 | `CreatedAt` | datetime2 | NOT NULL |  |  |
| 16 | `CreatedBy` | int | NULL |  |  |
| 17 | `DeletedAt` | datetime2 | NULL |  |  |
| 18 | `DeletedBy` | int | NULL |  |  |
| 19 | `IsDeleted` | bit | NOT NULL | default false |  |
| 20 | `UpdatedAt` | datetime2 | NOT NULL |  |  |
| 21 | `UpdatedBy` | int | NULL |  |  |

**Indexes**

| Columns | Unique | Filter | Name |
|---|---|---|---|
| ClassTeacherId |  |  |  |
| AcademicYearId, Name, Section | yes | [IsDeleted] = 0 |  |

**Check constraints**

- `CK_ClassSections_Floor`: `[Floor] IS NULL OR [Floor] >= 0`

## Teachers

Teacher profile, one-to-one with a user.  
Entity: `Teacher` · Columns: **12**

| # | Column | SQL type | Null | Default / notes | Key |
|---|---|---|---|---|---|
| 1 | `Id` | int | NOT NULL | identity | PK |
| 2 | `UserId` | int | NOT NULL |  | FK → Users (Restrict), UQ |
| 3 | `Name` | nvarchar(150) | NOT NULL |  |  |
| 4 | `Phone` | nvarchar(30) | NULL |  |  |
| 5 | `Specialization` | nvarchar(100) | NULL |  |  |
| 6 | `CreatedAt` | datetime2 | NOT NULL |  |  |
| 7 | `CreatedBy` | int | NULL |  |  |
| 8 | `DeletedAt` | datetime2 | NULL |  |  |
| 9 | `DeletedBy` | int | NULL |  |  |
| 10 | `IsDeleted` | bit | NOT NULL | default false |  |
| 11 | `UpdatedAt` | datetime2 | NOT NULL |  |  |
| 12 | `UpdatedBy` | int | NULL |  |  |

**Indexes**

| Columns | Unique | Filter | Name |
|---|---|---|---|
| UserId | yes |  |  |

## Subjects

A subject in one class section, taught by at most one teacher.  
Entity: `Subject` · Columns: **20**

| # | Column | SQL type | Null | Default / notes | Key |
|---|---|---|---|---|---|
| 1 | `Id` | int | NOT NULL | identity | PK |
| 2 | `ClassSectionId` | int | NOT NULL |  | FK → ClassSections (Restrict) |
| 3 | `TeacherId` | int | NULL |  | FK → Teachers (Restrict) |
| 4 | `Code` | nvarchar(20) | NOT NULL |  |  |
| 5 | `MaxMarks` | int | NULL |  |  |
| 6 | `Name` | nvarchar(100) | NOT NULL |  |  |
| 7 | `PassMarks` | int | NULL |  |  |
| 8 | `PracticalMax` | int | NULL |  |  |
| 9 | `PracticalPass` | int | NULL |  |  |
| 10 | `Status` | nvarchar(20) | NOT NULL | default "Active" |  |
| 11 | `TheoryMax` | int | NULL |  |  |
| 12 | `TheoryPass` | int | NULL |  |  |
| 13 | `Type` | nvarchar(20) | NOT NULL |  |  |
| 14 | `CreatedAt` | datetime2 | NOT NULL |  |  |
| 15 | `CreatedBy` | int | NULL |  |  |
| 16 | `DeletedAt` | datetime2 | NULL |  |  |
| 17 | `DeletedBy` | int | NULL |  |  |
| 18 | `IsDeleted` | bit | NOT NULL | default false |  |
| 19 | `UpdatedAt` | datetime2 | NOT NULL |  |  |
| 20 | `UpdatedBy` | int | NULL |  |  |

**Indexes**

| Columns | Unique | Filter | Name |
|---|---|---|---|
| ClassSectionId |  |  |  |
| TeacherId |  |  |  |
| Code, ClassSectionId | yes | [IsDeleted] = 0 |  |

**Check constraints**

- `CK_Subjects_MarksByType`: `([Type] IN ('Theory','Practical') AND [MaxMarks] IS NOT NULL AND [PassMarks] IS NOT NULL  AND [TheoryMax] IS NULL AND [TheoryPass] IS NULL AND [PracticalMax] IS NULL AND [PracticalPass] IS NULL) OR ([Type] = 'Both' AND [TheoryMax] IS NOT NULL AND [TheoryPass] IS NOT NULL  AND [PracticalMax] IS NOT NULL AND [PracticalPass] IS NOT NULL AND [MaxMarks] IS NULL AND [PassMarks] IS NULL)`

## Admissions

Admission application pipeline (Registered, Admitted, Enrolled, Rejected).  
Entity: `Admission` · Columns: **40**

| # | Column | SQL type | Null | Default / notes | Key |
|---|---|---|---|---|---|
| 1 | `Id` | int | NOT NULL | identity | PK |
| 2 | `AcademicYearId` | int | NOT NULL |  | FK → AcademicYears (Restrict) |
| 3 | `AllottedClassSectionId` | int | NULL |  | FK → ClassSections (Restrict) |
| 4 | `AppliedForClassSectionId` | int | NOT NULL |  | FK → ClassSections (Restrict) |
| 5 | `AddressLine` | nvarchar(200) | NULL |  |  |
| 6 | `AdmissionDate` | date | NULL |  |  |
| 7 | `AdmissionFee` | decimal(10,2) | NULL |  |  |
| 8 | `AdmissionFeeReference` | nvarchar(30) | NULL |  |  |
| 9 | `AdmissionNumber` | nvarchar(30) | NULL |  |  |
| 10 | `AdmissionType` | nvarchar(20) | NOT NULL |  |  |
| 11 | `BloodGroup` | nvarchar(10) | NULL |  |  |
| 12 | `Category` | nvarchar(50) | NULL |  |  |
| 13 | `City` | nvarchar(50) | NULL |  |  |
| 14 | `DateOfBirth` | date | NOT NULL |  |  |
| 15 | `Email` | nvarchar(100) | NULL |  |  |
| 16 | `EntryPoint` | nvarchar(50) | NULL |  |  |
| 17 | `FirstName` | nvarchar(50) | NOT NULL |  |  |
| 18 | `Gender` | nvarchar(10) | NOT NULL |  |  |
| 19 | `LastName` | nvarchar(50) | NOT NULL |  |  |
| 20 | `MedicalNotes` | nvarchar(300) | NULL |  |  |
| 21 | `MiddleName` | nvarchar(50) | NULL |  |  |
| 22 | `Phone` | nvarchar(20) | NOT NULL |  |  |
| 23 | `Pincode` | nvarchar(10) | NULL |  |  |
| 24 | `PreviousSchool` | nvarchar(100) | NULL |  |  |
| 25 | `RegNo` | nvarchar(30) | NOT NULL |  | UQ |
| 26 | `RegistrationDate` | date | NOT NULL |  |  |
| 27 | `RejectionReason` | nvarchar(300) | NULL |  |  |
| 28 | `Religion` | nvarchar(50) | NULL |  |  |
| 29 | `Remarks` | nvarchar(300) | NULL |  |  |
| 30 | `RollNumber` | nvarchar(20) | NULL |  |  |
| 31 | `State` | nvarchar(50) | NULL |  |  |
| 32 | `Status` | nvarchar(20) | NOT NULL | default "Registered" |  |
| 33 | `TransportRequired` | bit | NOT NULL | default false |  |
| 34 | `CreatedAt` | datetime2 | NOT NULL |  |  |
| 35 | `CreatedBy` | int | NULL |  |  |
| 36 | `DeletedAt` | datetime2 | NULL |  |  |
| 37 | `DeletedBy` | int | NULL |  |  |
| 38 | `IsDeleted` | bit | NOT NULL | default false |  |
| 39 | `UpdatedAt` | datetime2 | NOT NULL |  |  |
| 40 | `UpdatedBy` | int | NULL |  |  |

**Indexes**

| Columns | Unique | Filter | Name |
|---|---|---|---|
| AllottedClassSectionId |  |  |  |
| AppliedForClassSectionId |  |  |  |
| RegNo | yes |  |  |
| AcademicYearId, Status |  |  |  |

## AdmissionGuardians

Father/mother/guardian written on an application (snapshot).  
Entity: `AdmissionGuardian` · Columns: **10**

| # | Column | SQL type | Null | Default / notes | Key |
|---|---|---|---|---|---|
| 1 | `Id` | int | NOT NULL | identity | PK |
| 2 | `AdmissionId` | int | NOT NULL |  | FK → Admissions (Cascade) |
| 3 | `Email` | nvarchar(100) | NULL |  |  |
| 4 | `IsPrimaryContact` | bit | NOT NULL | default false |  |
| 5 | `Mobile` | nvarchar(20) | NULL |  |  |
| 6 | `MobileKey` | nvarchar(20) | NULL | computed, stored: CAST(REPLACE(REPLACE(REPLACE(REPLACE(REPLACE(REPLACE([Mobile],' ',''),'-',''),'+ |  |
| 7 | `Name` | nvarchar(100) | NOT NULL |  |  |
| 8 | `RelationType` | nvarchar(30) | NOT NULL |  |  |
| 9 | `CreatedAt` | datetime2 | NOT NULL | default SYSUTCDATETIME() |  |
| 10 | `UpdatedAt` | datetime2 | NOT NULL | default SYSUTCDATETIME() |  |

**Indexes**

| Columns | Unique | Filter | Name |
|---|---|---|---|
| AdmissionId | yes | [IsPrimaryContact] = 1 | UX_AdmissionGuardians_Admission_Primary |

**Check constraints**

- `CK_AdmissionGuardians_RelationType`: `[RelationType] IN ('Father','Mother','Guardian')`

## Students

The student's current record (history lives in StudentEnrollments).  
Entity: `Student` · Columns: **46**

| # | Column | SQL type | Null | Default / notes | Key |
|---|---|---|---|---|---|
| 1 | `Id` | int | NOT NULL | identity | PK |
| 2 | `AdmissionId` | int | NULL |  | FK → Admissions (Restrict) |
| 3 | `ClassSectionId` | int | NOT NULL |  | FK → ClassSections (Restrict) |
| 4 | `UserId` | int | NULL |  | FK → Users (Restrict) |
| 5 | `AddressLine` | nvarchar(200) | NULL |  |  |
| 6 | `AdmNo` | nvarchar(30) | NOT NULL |  | UQ |
| 7 | `AdmissionDate` | date | NOT NULL |  |  |
| 8 | `AdmissionType` | nvarchar(30) | NOT NULL | default "Fresh Admission" |  |
| 9 | `Category` | nvarchar(20) | NOT NULL | default "General" |  |
| 10 | `City` | nvarchar(50) | NULL |  |  |
| 11 | `CountryOfBirth` | nvarchar(50) | NULL |  |  |
| 12 | `CurriculumTrack` | nvarchar(50) | NULL |  |  |
| 13 | `CustodyArrangement` | nvarchar(20) | NULL |  |  |
| 14 | `DateOfBirth` | date | NOT NULL |  |  |
| 15 | `EalCode` | nvarchar(30) | NULL |  |  |
| 16 | `Email` | nvarchar(100) | NULL |  |  |
| 17 | `EnglishProficiency` | nvarchar(30) | NULL |  |  |
| 18 | `FeeConcessionPercent` | decimal(5,2) | NULL |  |  |
| 19 | `FirstName` | nvarchar(50) | NOT NULL |  |  |
| 20 | `Gender` | nvarchar(10) | NOT NULL |  |  |
| 21 | `HomeLanguage` | nvarchar(50) | NULL |  |  |
| 22 | `House` | nvarchar(30) | NULL |  |  |
| 23 | `LastName` | nvarchar(50) | NOT NULL |  |  |
| 24 | `MediaConsent` | bit | NOT NULL | default true |  |
| 25 | `MiddleName` | nvarchar(50) | NULL |  |  |
| 26 | `Mobile` | nvarchar(20) | NULL |  |  |
| 27 | `MotherTongue` | nvarchar(50) | NULL |  |  |
| 28 | `Nationality` | nvarchar(50) | NULL |  |  |
| 29 | `PhotoUrl` | nvarchar(500) | NULL |  |  |
| 30 | `Pincode` | nvarchar(10) | NULL |  |  |
| 31 | `PreferredName` | nvarchar(50) | NULL |  |  |
| 32 | `PreviousSchool` | nvarchar(100) | NULL |  |  |
| 33 | `Religion` | nvarchar(50) | NULL |  |  |
| 34 | `RollNumber` | nvarchar(20) | NOT NULL |  |  |
| 35 | `SecondNationality` | nvarchar(50) | NULL |  |  |
| 36 | `State` | nvarchar(50) | NULL |  |  |
| 37 | `Status` | nvarchar(20) | NOT NULL | default "Active" |  |
| 38 | `TransportRequired` | bit | NOT NULL | default false |  |
| 39 | `TransportRoute` | nvarchar(100) | NULL |  |  |
| 40 | `CreatedAt` | datetime2 | NOT NULL |  |  |
| 41 | `CreatedBy` | int | NULL |  |  |
| 42 | `DeletedAt` | datetime2 | NULL |  |  |
| 43 | `DeletedBy` | int | NULL |  |  |
| 44 | `IsDeleted` | bit | NOT NULL | default false |  |
| 45 | `UpdatedAt` | datetime2 | NOT NULL |  |  |
| 46 | `UpdatedBy` | int | NULL |  |  |

**Indexes**

| Columns | Unique | Filter | Name |
|---|---|---|---|
| AdmNo | yes |  |  |
| AdmissionId | yes | [AdmissionId] IS NOT NULL |  |
| UserId |  |  |  |
| ClassSectionId, RollNumber | yes |  |  |

## StudentEnrollments

Academic history: one row per class/section period.  
Entity: `StudentEnrollment` · Columns: **13**

| # | Column | SQL type | Null | Default / notes | Key |
|---|---|---|---|---|---|
| 1 | `Id` | int | NOT NULL | identity | PK |
| 2 | `AcademicYearId` | int | NOT NULL |  | FK → AcademicYears (Restrict) |
| 3 | `ClassSectionId` | int | NOT NULL |  | FK → ClassSections (Restrict) |
| 4 | `StudentId` | int | NOT NULL |  | FK → Students (Restrict) |
| 5 | `EndDate` | date | NULL |  |  |
| 6 | `Remarks` | nvarchar(300) | NULL |  |  |
| 7 | `RollNumber` | nvarchar(20) | NOT NULL |  |  |
| 8 | `StartDate` | date | NOT NULL |  |  |
| 9 | `Status` | nvarchar(20) | NOT NULL | default "Active" |  |
| 10 | `CreatedAt` | datetime2 | NOT NULL | default SYSUTCDATETIME() |  |
| 11 | `CreatedBy` | int | NULL |  |  |
| 12 | `UpdatedAt` | datetime2 | NOT NULL | default SYSUTCDATETIME() |  |
| 13 | `UpdatedBy` | int | NULL |  |  |

**Indexes**

| Columns | Unique | Filter | Name |
|---|---|---|---|
| AcademicYearId |  |  |  |
| StudentId | yes | [Status] = 'Active' | UX_StudentEnrollments_Student_Active |
| ClassSectionId, RollNumber | yes | [Status] = 'Active' | UX_StudentEnrollments_Section_Roll_Active |
| ClassSectionId, Status |  |  | IX_StudentEnrollments_Section_Status |
| StudentId, AcademicYearId |  |  | IX_StudentEnrollments_Student_Year |

**Check constraints**

- `CK_StudentEnrollments_EndDate`: `([Status] = 'Active' AND [EndDate] IS NULL) OR ([Status] <> 'Active' AND [EndDate] IS NOT NULL AND [EndDate] >= [StartDate])`
- `CK_StudentEnrollments_Status`: `[Status] IN ('Active','Promoted','Repeated','Transferred','Left')`

## StudentHealth

Health and insurance details, one row per student.  
Entity: `StudentHealth` · Columns: **10**

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
| 9 | `CreatedAt` | datetime2 | NOT NULL | default SYSUTCDATETIME() |  |
| 10 | `UpdatedAt` | datetime2 | NOT NULL | default SYSUTCDATETIME() |  |

## StudentIdentityDocuments

Aadhaar, passport, visa (sensitive), one row per student.  
Entity: `StudentIdentityDocument` · Columns: **8**

| # | Column | SQL type | Null | Default / notes | Key |
|---|---|---|---|---|---|
| 1 | `StudentId` | int | NOT NULL |  | PK, FK → Students (Cascade) |
| 2 | `AadhaarNumber` | nvarchar(12) | NULL |  |  |
| 3 | `PassportExpiry` | date | NULL |  |  |
| 4 | `PassportNumber` | nvarchar(30) | NULL |  |  |
| 5 | `VisaExpiry` | date | NULL |  |  |
| 6 | `VisaType` | nvarchar(30) | NULL |  |  |
| 7 | `CreatedAt` | datetime2 | NOT NULL | default SYSUTCDATETIME() |  |
| 8 | `UpdatedAt` | datetime2 | NOT NULL | default SYSUTCDATETIME() |  |

**Indexes**

| Columns | Unique | Filter | Name |
|---|---|---|---|
| AadhaarNumber | yes | [AadhaarNumber] IS NOT NULL | UX_StudentIdentityDocuments_Aadhaar |

**Check constraints**

- `CK_StudentIdentityDocuments_Aadhaar`: `[AadhaarNumber] IS NULL OR (LEN([AadhaarNumber]) = 12 AND [AadhaarNumber] NOT LIKE '%[^0-9]%')`

## StudentPickupPersons

People authorised to collect the child.  
Entity: `StudentPickupPerson` · Columns: **8**

| # | Column | SQL type | Null | Default / notes | Key |
|---|---|---|---|---|---|
| 1 | `Id` | int | NOT NULL | identity | PK |
| 2 | `StudentId` | int | NOT NULL |  | FK → Students (Cascade) |
| 3 | `IdNote` | nvarchar(100) | NULL |  |  |
| 4 | `Name` | nvarchar(100) | NOT NULL |  |  |
| 5 | `Phone` | nvarchar(20) | NOT NULL |  |  |
| 6 | `Relation` | nvarchar(50) | NOT NULL |  |  |
| 7 | `CreatedAt` | datetime2 | NOT NULL | default SYSUTCDATETIME() |  |
| 8 | `UpdatedAt` | datetime2 | NOT NULL | default SYSUTCDATETIME() |  |

**Indexes**

| Columns | Unique | Filter | Name |
|---|---|---|---|
| StudentId |  |  | IX_StudentPickupPersons_StudentId |

## Parents

Parent/guardian people, one row per real person.  
Entity: `Parent` · Columns: **36**

| # | Column | SQL type | Null | Default / notes | Key |
|---|---|---|---|---|---|
| 1 | `Id` | int | NOT NULL | identity | PK |
| 2 | `UserId` | int | NULL |  | FK → Users (Restrict) |
| 3 | `AddressLine` | nvarchar(200) | NULL |  |  |
| 4 | `BillingContact` | bit | NOT NULL |  |  |
| 5 | `City` | nvarchar(50) | NULL |  |  |
| 6 | `CountryOfResidence` | nvarchar(50) | NULL |  |  |
| 7 | `Email` | nvarchar(100) | NULL |  |  |
| 8 | `EmergencyOnly` | bit | NOT NULL |  |  |
| 9 | `Employer` | nvarchar(100) | NULL |  |  |
| 10 | `JobTitle` | nvarchar(100) | NULL |  |  |
| 11 | `Mobile` | nvarchar(20) | NOT NULL |  |  |
| 12 | `MobileKey` | nvarchar(20) | NOT NULL | computed, stored: CAST(REPLACE(REPLACE(REPLACE(REPLACE(REPLACE(REPLACE([Mobile],' ',''),'-',''),'+ |  |
| 13 | `Name` | nvarchar(100) | NOT NULL |  |  |
| 14 | `Nationality` | nvarchar(50) | NULL |  |  |
| 15 | `NotifyAttendance` | bit | NOT NULL | default true |  |
| 16 | `NotifyDiscipline` | bit | NOT NULL | default true |  |
| 17 | `NotifyExams` | bit | NOT NULL | default true |  |
| 18 | `NotifyFees` | bit | NOT NULL | default true |  |
| 19 | `NotifyNotices` | bit | NOT NULL | default true |  |
| 20 | `Occupation` | nvarchar(100) | NULL |  |  |
| 21 | `Pincode` | nvarchar(10) | NULL |  |  |
| 22 | `PreferredContactMethod` | nvarchar(30) | NULL |  |  |
| 23 | `PreferredLanguage` | nvarchar(30) | NULL |  |  |
| 24 | `State` | nvarchar(50) | NULL |  |  |
| 25 | `Status` | nvarchar(20) | NOT NULL | default "Active" |  |
| 26 | `Timezone` | nvarchar(50) | NULL |  |  |
| 27 | `Whatsapp` | nvarchar(20) | NULL |  |  |
| 28 | `WorkEmail` | nvarchar(100) | NULL |  |  |
| 29 | `WorkPhone` | nvarchar(20) | NULL |  |  |
| 30 | `CreatedAt` | datetime2 | NOT NULL |  |  |
| 31 | `CreatedBy` | int | NULL |  |  |
| 32 | `DeletedAt` | datetime2 | NULL |  |  |
| 33 | `DeletedBy` | int | NULL |  |  |
| 34 | `IsDeleted` | bit | NOT NULL | default false |  |
| 35 | `UpdatedAt` | datetime2 | NOT NULL |  |  |
| 36 | `UpdatedBy` | int | NULL |  |  |

**Indexes**

| Columns | Unique | Filter | Name |
|---|---|---|---|
| MobileKey |  |  | IX_Parents_MobileKey |
| UserId |  |  |  |

## StudentGuardians

Link student to parent, with relation and primary-contact flag.  
Entity: `StudentGuardian` · Columns: **4**

| # | Column | SQL type | Null | Default / notes | Key |
|---|---|---|---|---|---|
| 1 | `ParentId` | int | NOT NULL |  | PK, FK → Parents (Cascade) |
| 2 | `StudentId` | int | NOT NULL |  | PK, FK → Students (Cascade) |
| 3 | `IsPrimaryContact` | bit | NOT NULL |  |  |
| 4 | `RelationType` | nvarchar(30) | NOT NULL |  |  |

**Indexes**

| Columns | Unique | Filter | Name |
|---|---|---|---|
| ParentId |  |  |  |
| StudentId | yes | [IsPrimaryContact] = 1 | UX_StudentGuardians_Student_Primary |

**Check constraints**

- `CK_StudentGuardians_RelationType`: `[RelationType] IN ('Father','Mother','Guardian')`

## AttendanceSessions

One roll call: class + date (+ optional subject).  
Entity: `AttendanceSession` · Columns: **9**

| # | Column | SQL type | Null | Default / notes | Key |
|---|---|---|---|---|---|
| 1 | `Id` | int | NOT NULL | identity | PK |
| 2 | `ClassSectionId` | int | NOT NULL |  | FK → ClassSections (Restrict) |
| 3 | `SubjectId` | int | NULL |  | FK → Subjects (Restrict) |
| 4 | `TakenByUserId` | int | NOT NULL |  | FK → Users (Restrict) |
| 5 | `Date` | date | NOT NULL |  |  |
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
| ClassSectionId, Date | yes | [SubjectId] IS NULL | UX_AttendanceSessions_Class_Date_Daily |
| ClassSectionId, Date, SubjectId | yes | [SubjectId] IS NOT NULL | UX_AttendanceSessions_Class_Date_Subject |

## AttendanceRecords

One student's status in one session.  
Entity: `AttendanceRecord` · Columns: **5**

| # | Column | SQL type | Null | Default / notes | Key |
|---|---|---|---|---|---|
| 1 | `Id` | int | NOT NULL | identity | PK |
| 2 | `SessionId` | int | NOT NULL |  | FK → AttendanceSessions (Cascade) |
| 3 | `StudentId` | int | NOT NULL |  | FK → Students (Restrict) |
| 4 | `Remarks` | nvarchar(250) | NULL |  |  |
| 5 | `Status` | nvarchar(10) | NOT NULL |  |  |

**Indexes**

| Columns | Unique | Filter | Name |
|---|---|---|---|
| SessionId, StudentId | yes |  |  |
| StudentId, SessionId |  |  |  |

**Check constraints**

- `CK_AttendanceRecords_Status`: `[Status] IN ('Present','Absent','Late','Half Day','Leave')`
