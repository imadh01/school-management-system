# Timetable controller: what each endpoint actually does

Source: `backend/SchoolManagement.API/Controllers/TimetableController.cs` (route prefix `api/timetable`) and `AcademicYearsController.cs` (route `api/academic-years`).
Written for a developer with about two years of experience who is learning this codebase.

## How this file was made

- **Everything here was read from the code**: both controllers, `TimetableService`, `TimetableRepository`, `TimetableRules`, the two validators, the DTOs, the two entities and their configurations, and the hooks in `TeacherService`, `SubjectService` and `SchoolSettingsService`.
- **I did not run the API.** There is no SQL Server in my environment. The user ran the build and the tests in Visual Studio and reported they passed. Every JSON example below is **invented**: field names, error codes and messages come from the code, but ids, names and times are made up.
- **Tests that cover this module**: `TimetableRulesUnitTests` (pure rules and validators), `TimetableRulesTests` (22 database tests, including two parallel saves), and 10 rows in `AccessMatrixTests`.
- The doubts I noticed are at the end, labelled **Should fix** or **Question**.

## The endpoints at a glance

| # | Method + URL | Permission needed | Called by (React) |
|---|---|---|---|
| 1 | `GET /api/academic-years` | `Timetable.View` | `TimetablePage` year drop-down |
| 2 | `GET /api/timetable/periods?academicYearId=` | `Timetable.View` | Periods tab, grids |
| 3 | `POST /api/timetable/periods` | `Timetable.ManagePeriods` | `PeriodDialog` (new) |
| 4 | `PUT /api/timetable/periods/{id}` | `Timetable.ManagePeriods` | `PeriodDialog` (edit) |
| 5 | `DELETE /api/timetable/periods/{id}` | `Timetable.ManagePeriods` | Periods tab |
| 6 | `GET /api/timetable/classes/{classSectionId}` | `Timetable.View` | Class timetable tab |
| 7 | `PUT /api/timetable/classes/{classSectionId}/cells` | `Timetable.Edit` | `CellDialog` |
| 8 | `DELETE /api/timetable/classes/{classSectionId}/cells?day=&periodId=` | `Timetable.Edit` | `CellDialog` (Clear lesson) |
| 9 | `POST /api/timetable/classes/{classSectionId}/copy-from/{sourceClassSectionId}` | `Timetable.Edit` | `CopyFromDialog` |
| 10 | `GET /api/timetable/teachers/{teacherId}?academicYearId=` | `Timetable.View` **or** `Timetable.ViewScoped` | Teacher tab |

## Background you need before the endpoints

### The same pipeline as Auth
Correlation id, Serilog, `GlobalExceptionMiddleware`, JWT, `MustChangePasswordMiddleware`, then the permission policy. See `auth.md`. A refused permission is **403** with the standard error body. Controllers validate inline with FluentValidation and return **400** `VALIDATION_ERROR`.

### Two tables
- **`TimetablePeriods`**: the slots of a school day, **one set per academic year** ("Shared times": every class of the year uses the same periods). Columns: label, start and end (`time(0)`), `Kind` (`Teaching` or `Break`), sort order, `RowVersion`. Soft delete. Label and sort order are unique per year among live rows (filtered unique indexes).
- **`TimetableEntries`**: one lesson = class + period + day + subject + optional room. Soft delete. At most **one live lesson per class, day and period** (filtered unique index).

### The teacher is not stored on a lesson
A lesson points to a **subject**, and a subject has one teacher (`Subject.TeacherId`). So the teacher of a lesson is whoever teaches that subject. Assigning a teacher is done on the Teachers screens, not here.

### The clash rule
One teacher cannot be in two classes at the same day and period. It lives in one method, `ITimetableRepository.FindTeacherClashAsync`, used by Save cells, Copy From and the teacher-assignment hook. It compares **period ids** (same slot), not time overlap. Because periods are shared per year, that is the same thing today. If classes ever get different times, change that one method.

### The lock
Every change to periods or lessons runs inside `ExecuteLockedAsync(academicYearId, ...)`: a transaction holding `sp_getapplock` named `Timetable:{academicYearId}`. Two admins saving at once are served one after the other, so the clash check and the insert cannot interleave. The lock **does not nest**: code already inside it must not call another locked method. The repository also turns unique-index errors 2601/2627 into **409** `CONFLICT`. If the lock is not granted within 10 seconds the caller gets **409** `The timetable is being updated by someone else right now. Please try again.`

### Days shown
The grid shows the school's working days (`SchoolSettings.WorkingDays`, a bit mask: 31 = Mon-Fri) **plus any other day that holds a lesson**, Monday first, so a lesson is never hidden.

### Row version
Only periods carry `rowVersion`. A stale version fails at SaveChanges with `DbUpdateConcurrencyException`, which the middleware maps to **409** with code `CONCURRENCY_CONFLICT`. Lessons have no row version: the last save wins, guarded by the lock.

---

## 1. `GET /api/academic-years`

Read-only list of academic years for the year drop-down. Needs `Timetable.View` only because the Academic Years module is not built yet (see `docs/design/academic-years.md`). Returns `AcademicYearResponse[]`. 401/403 only. No paging.

## 2. `GET /api/timetable/periods`

- **Request**: optional query `academicYearId`. Missing means the **current** academic year; none current gives **422** `There is no current academic year.` Unknown year gives **404**.
- **Response**: `200`, `TimetablePeriodResponse[]` sorted by sort order: `id, academicYearId, label, startTime ("HH:mm"), endTime, kind, sortOrder, cellCount, rowVersion`. `cellCount` is how many live lessons use the period.
- **Logic**: resolve the year, load periods, load lesson counts per period, map.

```json
[ { "id": 3, "academicYearId": 1, "label": "Period 1", "startTime": "08:00", "endTime": "08:45",
    "kind": "Teaching", "sortOrder": 1, "cellCount": 12, "rowVersion": "AAAAAAAB3Q0=" } ]
```

## 3. `POST /api/timetable/periods`

- **Request** `CreateTimetablePeriodRequest`: `academicYearId` (>0), `label` (required, max 50), `startTime`, `endTime` ("HH:mm", end after start), `kind` (`Teaching`/`Break`), `sortOrder` (1 to 30).
- **Response**: `201 Created`, `Location: api/timetable/periods/{id}`, the period with `cellCount` 0.
- **Logic**: validator, year must exist (404), then **inside the lock**: load the year's periods and run the period rules (below), insert.
- **Period rules** (create and update): label unique in the year (case-insensitive) gives **409**; sort order unique gives **409**; the time range must not overlap another period gives **422**. Back-to-back (08:45 end, 08:45 start) is allowed.

| Status | Code | When |
|---|---|---|
| 400 | `VALIDATION_ERROR` | field rules |
| 403 | `FORBIDDEN` | no `Timetable.ManagePeriods` |
| 404 | `NOT_FOUND` | academic year missing |
| 409 | `CONFLICT` | duplicate label or order |
| 409 | `CONCURRENCY_CONFLICT` | (update only) stale row version |
| 422 | `BUSINESS_RULE_VIOLATION` | overlap |

## 4. `PUT /api/timetable/periods/{id}`

- **Request** `UpdateTimetablePeriodRequest`: same fields as create (no year) plus `rowVersion` (required, base64).
- **Logic**: load (404), `IConcurrencyGuard.Expect` the row version, then inside the lock re-run the period rules against the *other* periods and apply. **A period that already holds lessons cannot be changed to a Break** (**422**). Changing its times is allowed; lessons simply move with it.
- **Errors**: 400, 403, 404, 409 (stale version, duplicate), 422 (overlap, break with lessons).
- **Concurrency**: the stale-version failure is `DbUpdateConcurrencyException` (409 `CONCURRENCY_CONFLICT`). Repeating the same PUT after success fails with 409 because the version changed.

## 5. `DELETE /api/timetable/periods/{id}`

- **Response**: `204`.
- **Logic**: load (404); inside the lock count live lessons; if any, **409** `X is used by N lesson(s) and cannot be deleted. Clear them first.` Otherwise soft delete.
- The React Periods tab hides the Delete button when `cellCount > 0`.

## 6. `GET /api/timetable/classes/{classSectionId}`

- **Response** `ClassTimetableResponse`: `classSectionId, className, academicYearId, classRoom, days[] (0 = Sunday ... 6 = Saturday), periods[], cells[]`. Each cell: `entryId, dayOfWeek, periodId, subjectId, subjectName, subjectCode, teacherId, teacherName, room, effectiveRoom`. `effectiveRoom` is the cell's own room, else the class section's room.
- **Errors**: 403, 404 `Class could not be found.`

```json
{ "classSectionId": 12, "className": "Class 5 A (2026-2027)", "academicYearId": 1, "classRoom": "R-204",
  "days": [1,2,3,4,5], "periods": [ { "id": 3, "label": "Period 1", "kind": "Teaching" } ],
  "cells": [ { "entryId": 90, "dayOfWeek": 1, "periodId": 3, "subjectId": 41, "subjectName": "Mathematics",
               "subjectCode": "MATH", "teacherId": 7, "teacherName": "Meera Nair",
               "room": null, "effectiveRoom": "R-204" } ] }
```

## 7. `PUT /api/timetable/classes/{classSectionId}/cells`

- **Request** `{ "cells": [ { "dayOfWeek": 1, "periodId": 3, "subjectId": 41, "room": "Lab 1" } ] }`. 1 to 200 cells; no repeated day+period; day 0 to 6; room max 30. Cells not mentioned are left alone (add or replace, never remove).
- **Response**: `200`, the whole refreshed class timetable.
- **Logic**: class must exist (404). Inside the lock, for **each cell in order**:
  1. period belongs to the class's academic year, else **422**;
  2. period is `Teaching`, else **422** (a break takes no lesson);
  3. the day is a working day, else **422**;
  4. subject exists (404), belongs to **this class** (422), and is `Active` (422);
  5. **teacher clash** with any other class at that day and period gives **409** `X already teaches Class 6 B on Monday in Period 1.` (no teacher on the subject means no check);
  6. empty room becomes null; update the existing lesson or insert.
  Save once at the end, so **one bad cell rejects the whole request** and nothing is saved.
- **Errors**: 400, 403, 404, 409, 422.
- **Concurrency**: two admins saving clashing lessons at once: the lock serialises them, exactly one wins, the other gets 409 (covered by a test).

## 8. `DELETE /api/timetable/classes/{classSectionId}/cells?day=&periodId=`

- `day` is 0 to 6 and `periodId` > 0, else **400** (checked in the controller). Response `204`.
- Inside the lock: no lesson there gives **404** `There is no lesson in that period.`; otherwise soft delete.

## 9. `POST /api/timetable/classes/{classSectionId}/copy-from/{sourceClassSectionId}`

- **Response**: `200`, `{ "replaced": 28, "copied": 26, "skipped": [ { "dayOfWeek": 1, "periodLabel": "Period 3", "subjectName": "Art", "reason": "..." } ] }`.
- **Rules**: source and target must differ and be in the **same academic year** (else **422**). The target grid is **cleared first** (soft delete, saved) and then filled; the clear and the fill are one lock, but two SaveChanges, so the freed slots no longer count for the unique index.
- **Per source lesson**, it is skipped (and reported) if: the day is no longer a working day; the target has no subject with the **same code**; the matched subject's teacher already teaches another class at that time. Otherwise copied with the same room.
- The clear and the fill both run inside `ExecuteLockedAsync`'s single transaction, so they commit or roll back together: a failure leaves the old grid intact.

## 10. `GET /api/timetable/teachers/{teacherId}`

- **Policy**: `CompositePolicies.TimetableRead` (View or ViewScoped). The controller resolves the data scope: `Timetable.View` means unrestricted; `ViewScoped` only means "related to my user id".
- **Response** `TeacherTimetableResponse`: `teacherId, teacherName, academicYearId, days[], periods[], cells[]` where each cell has `dayOfWeek, periodId, classSectionId, className, subjectId, subjectName, effectiveRoom`.
- **Rule**: a scoped caller asking for **another** teacher gets **404** `Teacher could not be found.`, the same as an id that does not exist.
- Optional `academicYearId`, default current year (422 if none).

---

## Rules that live outside this controller

| Where | Rule |
|---|---|
| `TeacherService.AssignSubjectsAsync` | assigning a subject that is already on the timetable is refused (409) if it would put the teacher in two classes at once; unassigning is always allowed. Runs under the same lock. |
| `SubjectService.UpdateAsync` | moving a subject to another class is refused (409) while it has lessons. |
| `SubjectService.DeleteAsync` | deleting a subject is refused (409) while it has lessons. |
| `SchoolSettingsService.UpdateAsync` | removing a working day is refused (409) while the current year has lessons on that day. |

## Who can do what (seeded roles)

| Role | Permissions |
|---|---|
| Admin | all four (held in code) |
| Supervisor | `View`, `Edit` (no `ManagePeriods`) |
| Clerk | `View` |
| Teacher | `ViewScoped` (own week only) |
| Student, Parent | none |

`Timetable.Edit` and `ManagePeriods` imply `View` through `PermissionDependencies`.

## Doubts and possible problems

### Should fix
1. **Settings gap**: a lesson saved at the exact moment a working day is removed can slip through the Settings check (it is not under the timetable lock). The grid still shows any day that holds a lesson, so nothing is hidden. Low risk.
2. **Lessons have no row version**: concurrent edits of the same cell: last save wins (the lock only prevents clashes, not overwrites).
3. **`GET /api/academic-years` is guarded by `Timetable.View`** as a stopgap; move it to its own permission when the Academic Years module is built.

### Question
1. **Different Friday times** (UAE, possibly India): shared periods cannot express them. Planned: a `TimetablePeriodDayOverrides` table (period, day, start, end) and switching the clash check to time overlap. Needs a decision before it is built.
2. **Teacher on the lesson**: today derived from the subject. A subject taught by two teachers (for example split practical) is not possible.
3. **Copy From matches by subject code**; classes whose codes differ get everything skipped. Is a manual mapping wanted?
4. **No bulk "clear class timetable"** endpoint; clearing is lesson by lesson, or by Copy From.
