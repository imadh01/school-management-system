# Academic Years: design (not built)

Status: design only. No code, no migration. Written 19 Oct 2026.
Builds on: `AcademicYears` (exists, one seeded row `2026-2027`, current). Needed by: Timetable (periods per year), Exams, Fees, promotion.

## 1. What exists today

- Table `AcademicYears`: `Id, Name, StartDate, EndDate, IsCurrent, Status (default "Upcoming"), audit columns`. A filtered unique index allows only one row with `IsCurrent = 1`.
- The only row comes from one `HasData` seed. There is no API and no screen. A class section belongs to one year (`ClassSection.AcademicYearId`); `StudentEnrollment.AcademicYearId` mirrors it.
- "Current year" already drives behaviour: scoped teacher attendance (`DataScopeQueries`) only looks at the current year.
- `Status` has no defined vocabulary in code. **Check before building:** which value the seed row carries (it should be "Current" or "Active" if the column is meant to track `IsCurrent`).

## 2. What the prototype does

The prototype has **no Academic Years screen**. Years appear only as text inside class labels ("Class 1 A (2024-2025)") and as year dropdowns on Exams and Fee Structure (`2024-2025`, `2026-2027`). So this module is new; it is not a conversion of a prototype page. It exists because Timetable, Exams and Fees all need a real year to hang on to.

## 3. Where this conflicts with what we built

1. The prototype's class label contains the year; ours has `AcademicYearId`. The year dropdowns on Exams and Fee Structure must become a real foreign key, not text.
2. `IsCurrent` and `Status` say the same thing twice. They can disagree.
3. Nothing stops two years overlapping in dates.
4. Nothing creates the next year, so after 2026-2027 ends the school cannot add classes.

## 4. Lifecycle

```
Upcoming --set current--> Current --close--> Closed
```

- **Upcoming:** created, may be edited freely, may hold classes and periods, not yet the school's working year.
- **Current:** exactly one at a time. Data scope, defaults and "today's" screens use it.
- **Closed:** read-only history. Its classes, enrollments, timetable, exams and fees stay visible; nothing new can be added to it.

`Status` becomes `Upcoming | Current | Closed`. `IsCurrent` is kept (the unique filtered index is useful) but is written only by the service so the two never disagree; a CHECK constraint `(IsCurrent = 1) = (Status = 'Current')` enforces it.

## 5. Business rules

- **Create:** name unique (e.g. `2027-2028`), `StartDate < EndDate`, and the date range must not overlap another live year. New year starts `Upcoming` and empty (no classes, subjects, periods).
- **Edit:** name and dates editable while `Upcoming`. For `Current`, only `EndDate` and name; shortening below the last date of any existing record (enrollment end, attendance, exam) is refused. `Closed` cannot be edited.
- **Set current:** target must be `Upcoming`. One transaction under `sp_getapplock('AcademicYearCurrent')`: the old current row is cleared first and saved (what its status becomes is decision 2), then the new row is set, so the filtered unique index is never violated mid-statement.
- **Close:** only a non-current year (set another current first), or the current year if decision 3 allows. Refused with 409 while `Active` enrollments exist in the year (they must be promoted, repeated or left first). Closing records `ClosedAt/ClosedBy`.
- **Delete:** only an `Upcoming` year with no class sections, periods, exams or fee rows. Soft delete, to match the rest of the system. A year that has ever been Current is never deletable.
- **Reopen a closed year:** not allowed in v1 (decision 4).
- **Copy periods from previous year:** see 7. Target must be `Upcoming` or `Current` and have **no live periods**; otherwise 409 (no silent merge). Copies `Label, StartTime, EndTime, Kind, SortOrder`. Needs the Timetable tables, so this action ships with or after Timetable.
- **Consequence to warn about:** setting a new year current changes what scoped teachers see (their class/subject scope is "current year only"). The confirm dialog says so.

## 6. Database (changes to the existing table)

| Column | Type | Notes |
|---|---|---|
| `Status` | nvarchar(20) | CHECK in (`Upcoming`,`Current`,`Closed`); existing rows migrated: current row -> `Current`, others `Upcoming` |
| `RowVersion` | rowversion | new; the other masters have it |
| `ClosedAt`, `ClosedBy` | datetime2 null, int null | set on Close |
| `IsDeleted`, `DeletedAt`, `DeletedBy` | bit, datetime2 null, int null | soft delete |

Constraints: unique `Name` (filtered to `IsDeleted = 0`); CHECK `StartDate < EndDate`; CHECK `(IsCurrent = 1) = (Status = 'Current')`; existing filtered unique index on `IsCurrent = 1` stays. Overlap is checked in the service (SQL has no clean range-overlap constraint).

**Migration pre-check:** `SELECT COUNT(*) FROM AcademicYears WHERE IsCurrent = 1` must be 1, and the `Status` values in use must be listed first; the `Up` maps them, the `Down` drops the new columns and the CHECKs. After it: `dotnet ef migrations add Check` must produce an empty migration.

## 7. Endpoints

| Method and path | Purpose | Notes |
|---|---|---|
| `GET /api/academic-years` | list, newest first | `?status=`; small, not paged. Also serves the dropdowns on Timetable, Exams, Fees |
| `GET /api/academic-years/{id}` | one year | includes counts: classes, active enrollments, periods |
| `POST /api/academic-years` | create | 201, validator + overlap check |
| `PUT /api/academic-years/{id}` | edit | `rowVersion` required, 409 on stale |
| `POST /api/academic-years/{id}/set-current` | make current | 409 if not Upcoming; 422 if dates are wrong |
| `POST /api/academic-years/{id}/close` | close | 409 with the count of Active enrollments |
| `POST /api/academic-years/{id}/copy-periods?fromId=` | copy periods | returns `copied`; 409 if the target already has periods |
| `DELETE /api/academic-years/{id}` | soft delete | 409 if it has any dependants |

Errors use the standard envelope (`NOT_FOUND`, `CONFLICT`, `CONCURRENCY_CONFLICT`, `BUSINESS_RULE_VIOLATION`, `VALIDATION_ERROR`).

## 8. Permissions

- New module `AcademicYears`: `View`, `Create`, `Edit`, `SetCurrent`, `Close`, `Delete`. Non-read actions need `View` (existing dependency rule).
- Admin: all. Supervisor: `View` only. Clerk, Teacher: `View` only, because the year dropdown is needed on their screens. Student, Parent: none.
- The read endpoint should accept any of `AcademicYears.View`, `Timetable.View`, `Timetable.ViewScoped`, `Exams.View`, ... (composite policy, same pattern as `Attendance.Read`), so a Teacher without `AcademicYears.View` still loads the Timetable. Simpler alternative: give every staff role `AcademicYears.View` (decision 5).
- Needs: catalog entries, role seed rows, `ExpectedRoleMatrix` and `AccessMatrixTests` rows, and the catalog-sync migration for the new permissions.

## 9. Frontend

A Settings-area page "Academic Years": table (name, dates, status badge, counts), Add/Edit modal, row actions Set Current, Close, Copy Periods (confirm dialogs with the consequences above), Delete. Same fixed tabs, searchable selects, toasts as the rest of the app. No new shared components.

## 10. Tests to write

Create overlap refused; start before end; name unique among live rows; set-current moves the flag and never leaves two current rows; two simultaneous set-current calls leave exactly one current; close refused with Active enrollments, allowed without; closed year cannot be edited; copy periods refuses a non-empty target and copies exactly the source periods; delete refused with classes; stale rowVersion gives 409; each role's access (AccessMatrix rows).

## Decisions for you

1. **Status vocabulary:** `Upcoming / Current / Closed` with `IsCurrent` kept as a guarded mirror. OK? (Recommended.) Or drop `IsCurrent` and use `Status = 'Current'` for the unique index.
2. **When a new year becomes current, what happens to the old one?** Option A (recommended): it is no longer current but is not closed either; it becomes `Closed` only through an explicit Close. That needs a fourth state (for example `Ended`) between Current and Closed. Option B: setting current automatically closes the previous one (refused if it still has Active enrollments, which blocks the switch until promotion exists). Which?
3. **May the current year be closed directly,** or must another year be made current first? Recommended: another year first, so the school always has a current year.
4. **Reopen a closed year:** never (recommended), or Admin-only with a reason?
5. **Who may read the year list:** every staff role gets `AcademicYears.View`, or composite policies on the endpoint?
6. **Promotion:** Close is blocked while students are still Active in the year, and the promotion/rollover module does not exist. Until it does, the school can set a new year current only by leaving the old year un-closed. Acceptable for now, or is promotion the next module after Timetable?
7. **Roll-over helpers:** should "create new year" also offer copying classes/sections and subjects from the previous year (large, needs its own design), or only periods now (this doc)?
8. **Names and dates:** is `YYYY-YYYY` the required name format, and may the year start in August/September (UAE) with an end in June/July? Confirm the real dates for 2026-2027 (seed) and 2027-2028.
9. **Seed row:** confirm its `Status` value in the database before the migration is written (section 1).
