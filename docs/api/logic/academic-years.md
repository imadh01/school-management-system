# Academic Years controller: what each endpoint actually does

Source: `backend/SchoolManagement.API/Controllers/AcademicYearsController.cs`, route prefix `api/academic-years`.
Written for a developer with about two years of experience who is learning this codebase.

## How this file was made

- **Everything here was read from the code I wrote and checked against the design**: the controller, `AcademicYearService`, `AcademicYearRepository`, `AcademicYearRules`, the validators, the DTOs, the `AcademicYear` entity and its configuration, the two migrations, and the places that read "the current year".
- **I did not run the API or the database.** The user ran the build, the migrations and the tests in Visual Studio. Every JSON example is **invented**.
- Tests: `AcademicYearValidatorsTests` (7, no database) and `AcademicYearRulesTests` (20, real SQL Server), plus 8 rows in `AccessMatrixTests`.
- The decisions behind the rules are in `docs/design/academic-years.md` (section "Decisions made").

## The endpoints at a glance

| # | Method + URL | Permission needed | Called by (React) |
|---|---|---|---|
| 1 | `GET /api/academic-years?status=` | any signed-in user | every year drop-down, Academic Years page |
| 2 | `GET /api/academic-years/{id}` | `AcademicYears.View` | (not used by the page yet) |
| 3 | `POST /api/academic-years` | `AcademicYears.Create` | Add year dialog |
| 4 | `PUT /api/academic-years/{id}` | `AcademicYears.Edit` | Edit year dialog |
| 5 | `POST /api/academic-years/{id}/set-current` | `AcademicYears.SetCurrent` | Set current dialog |
| 6 | `POST /api/academic-years/{id}/close` | `AcademicYears.Close` | Close year dialog |
| 7 | `POST /api/academic-years/{id}/copy-periods?fromId=` | `AcademicYears.CopyPeriods` | Copy periods dialog |
| 8 | `DELETE /api/academic-years/{id}` | `AcademicYears.Delete` | Academic Years page |

The page itself is `/academic-years` and needs `AcademicYears.View`. **Only Admin holds any `AcademicYears.*` permission by default** (no role is granted them by the migration; the Roles page could grant them to a custom role).

## Background you need before the endpoints

### Life of a year
```
Upcoming --set current--> Current --another year set current--> Ended --close--> Closed
```
- **Upcoming:** being set up. May hold classes and periods.
- **Current:** exactly one. Defaults and teacher data scope follow it.
- **Ended:** another year took over. Data is untouched.
- **Closed:** read-only history. No reopen.
`IsCurrent` mirrors `Status = 'Current'`. A CHECK constraint (`CK_AcademicYears_CurrentMirror`) rejects the two disagreeing, a filtered unique index allows one `IsCurrent = 1` row, and `CK_AcademicYears_Status` limits the values.

### What "current year" is used for (important)
Switching the current year does **more than change defaults**:

| Where | Effect |
|---|---|
| `DataScopeQueries` (class teacher sections, taught subjects, taught sections) | A **scoped teacher** sees only classes, subjects and students of the current year. After a switch they see the new year's, which starts empty. |
| `ClassSectionService.CreateAsync` | default year of a new class |
| `TimetableService` (default year, working-day check) | default year of periods and a teacher's week; the Settings working-day check looks at the current year |
| `TimetablePage.tsx` | preselects it in the drop-down |
Nothing is deleted or rewritten by a switch. `AcademicYearRulesTests` has a test that shows the teacher-scope effect.

### The lock
Create, edit, set current, close and delete run in `AcademicYearRepository.ExecuteLockedAsync`: one transaction holding `sp_getapplock` named `AcademicYears`. The overlap and "one current year" checks read other rows, so these must not interleave. Copy Periods uses the existing Timetable lock for the target year instead (locks do not nest). A lock not granted in 10 seconds gives 409.

### Audit
The audit interceptor writes one `AuditLogs` row per changed record, so Set Current writes one for the old year (status to Ended) and one for the new year (status to Current).

### Closed years
A Closed year is refused for: edit, new classes (`ClassSectionService.CreateAsync`) and every Timetable write (periods, lessons, Copy From). **Not covered:** editing or deleting classes that already exist in a Closed year.

---

## 1. `GET /api/academic-years`
Any signed-in user (like Countries): the list feeds every drop-down. Optional `?status=` (Upcoming, Current, Ended, Closed; anything else gives 422). Newest first. Each row: `id, name, startDate, endDate, isCurrent, status, rowVersion, closedAt, classCount, activeEnrollmentCount, periodCount`. Counts are live records only.

```json
{ "id": 1, "name": "2026-2027", "startDate": "2026-06-01", "endDate": "2027-03-31", "isCurrent": true,
  "status": "Current", "rowVersion": "AAAAAAAB3Q0=", "closedAt": null,
  "classCount": 12, "activeEnrollmentCount": 340, "periodCount": 8 }
```

## 2. `GET /api/academic-years/{id}`
One year with its counts. 404 `Academic year could not be found.`

## 3. `POST /api/academic-years`
- **Request:** `name` (required, max 20, no format rule), `startDate`, `endDate` (end after start).
- **Response:** `201`, the year as Upcoming, not current, zero counts.
- **Rules (in the lock):** duplicate name (any case) gives **409**; dates overlapping another live year give **422** (both end dates count as inside, so a shared date overlaps).

## 4. `PUT /api/academic-years/{id}`
- **Request:** same fields plus `rowVersion`.
- **Rules:** Closed gives **409**. Upcoming may change name and both dates. Current and Ended may change name and **end date only** (a different start date gives **422**). Name and overlap rules as for create. The new dates must cover every record already saved in the year (attendance days, enrollment start and end dates), else **422**. A stale `rowVersion` gives **409** `CONCURRENCY_CONFLICT`.

## 5. `POST /api/academic-years/{id}/set-current`
- **Request:** `rowVersion` and `expectedCurrentYearId` (the year the confirm dialog named as current; null = none).
- **Rules (in the lock):** only an **Upcoming** year (else **409**); if the actual current year is not `expectedCurrentYearId`, **409** and nothing changes. Then, in the same transaction and in this order: the old current year becomes **Ended** (saved first, so the unique index is never violated), the new one becomes **Current**.
- **Concurrency:** two calls at once leave exactly one current year: one wins, the other gets 409 (tested).
- **Consequence:** teachers see the new year's classes (see "What current year is used for"). The React dialog says so and names both years.

## 6. `POST /api/academic-years/{id}/close`
- **Request:** `rowVersion` and `confirmName` (must equal the year's name exactly, case-sensitive, else **422**).
- **Rules:** only an **Ended** year. The current year gives **409** (make another current first); Upcoming and Closed give **409**. Refused with **409** while any student has an Active enrollment in it. Sets `Closed`, `ClosedAt` (UTC) and `ClosedBy`. No reopen.

## 7. `POST /api/academic-years/{id}/copy-periods?fromId=`
- Copies label, start, end, kind and order of every live period of year `fromId` into year `id`. Returns `{ "copied": 3 }`.
- **Rules:** same year gives **422**; target must be Upcoming or Current (else **409**); target must have **no** periods (else **409**: copying never merges); source with no periods gives **422**. The source can have any status. Lessons are not copied.
- `fromId` missing or 0 gives **400** `VALIDATION_ERROR`.

## 8. `DELETE /api/academic-years/{id}`
Only an **Upcoming** year with no class sections and no periods (else **409**). Soft delete; the name can be used again. A year that has ever been Current is never Upcoming, so it can never be deleted.

## Errors in one place
| Status | Code | Typical cause |
|---|---|---|
| 400 | `VALIDATION_ERROR` | field rules, missing `fromId` |
| 403 | `FORBIDDEN` | missing permission |
| 404 | `NOT_FOUND` | unknown or deleted year |
| 409 | `CONFLICT` | wrong status, duplicate name, active students, not empty, current year changed |
| 409 | `CONCURRENCY_CONFLICT` | stale `rowVersion` |
| 422 | `BUSINESS_RULE_VIOLATION` | overlap, dates exclude records, wrong typed name, bad `status` filter |

## Doubts and open questions
### Should fix
1. **Closed years:** existing classes in a Closed year can still be edited or deleted; only creation is blocked.
2. **Delete race:** a class created in the same instant as a delete could slip in. Very low risk.
3. **Set current from Ended** is refused. If a year is made current by mistake there is no way back to the old one yet.

### Question
1. **Name format and the real dates** of 2026-2027 and 2027-2028 (the school will send them). The name has no `YYYY-YYYY` rule today.
2. **Promotion** must exist before March 2027: Close is blocked while students are Active, and nothing moves them yet.
3. **Attendance "today" time zone** is undecided; Set Current does not look at today's date at all.
