# Attendance controller: what each endpoint actually does

Source: `backend/SchoolManagement.API/Controllers/AttendanceController.cs`, route prefix `api/attendance`.
Written for a developer with about two years of experience who is learning this codebase.

## How this file was made

- **Everything here was read from the code**: the controller, `AttendanceService`, `AttendanceRules`, `AttendanceRepository`, `DataScopeQueries`, the validator, the DTOs, the two entities and their configurations, `CompositePolicies`, and the React service.
- **I did not run the API or the database.** There is no SQL Server in my environment. So **every example below is invented**: shapes, field names, error codes and messages come from the code, but ids, names and numbers are made up.
- Tests: `AttendanceLockTests` covers the 24-hour lock (23 hours allowed, 25 hours refused, Admin allowed). The access matrix covers the permission of each endpoint. `DataScopeTests` covers who sees which attendance. **Nothing tests the save rules** (every student needs a status, only the class teacher marks daily, no future dates, the percentage formula).
- **Not verified** marks anything I could not confirm from the code alone.
- The doubts I noticed are at the end, labelled **Blocker**, **Should fix** or **Question**.

## The four endpoints at a glance

| # | Method + URL | Permission needed | Called by (React) |
|---|---|---|---|
| 1 | `GET /api/attendance/roster` | `Attendance.View` **or** `Attendance.ViewScoped` | `AttendancePage` |
| 2 | `PUT /api/attendance` | `Attendance.Manage` **or** `Attendance.Mark` | `AttendancePage` |
| 3 | `GET /api/attendance/students/{studentId}` | `Attendance.View` **or** `Attendance.ViewScoped` | student pages |
| 4 | `GET /api/attendance/summary` | `Attendance.View` **or** `Attendance.ViewScoped` | `AttendancePage` summary tab |

"Or" comes from the composite policies `Attendance.Read` and `Attendance.Write` (`CompositePolicies`). The dependencies add `ClassSections.View` and `Subjects.View` to `Attendance.Mark` and `Attendance.Manage`.

## Background you need before the endpoints

### The same request pipeline as Auth

Correlation id, Serilog, `GlobalExceptionMiddleware`, JWT, `MustChangePasswordMiddleware`, then the policy. See `auth.md`.

### Sessions and records

- An **`AttendanceSession`** is one register: **a class + a date**, and optionally **a subject**. `SubjectId = null` means **daily** attendance; a subject means **subject-wise** attendance.
- An **`AttendanceRecord`** is one student's status inside a session. One row per student per session (unique index).
- Statuses (exact text): `Present`, `Absent`, `Late`, `Half Day`, `Leave`.
- There is no delete. A register can only be saved and re-saved.
- Sessions and records follow their class: if the class is deleted, they are hidden (query filter).
- `AttendanceRecord` is not audited (`[AuditIgnore]`); the session carries `CreatedBy`/`UpdatedBy`.

### Two kinds of "who may do this"

1. **Who may see**: `Attendance.View` sees everything. `Attendance.ViewScoped` is limited by the **data scope**: the caller's own child or self, the classes where they are **class teacher** (current year), and the subjects they **teach** (current year). A subject teacher sees only their subject's sessions, not the student's other classes. Out of scope answers **404**, exactly like something that does not exist.
2. **Who may mark** (`EvaluateAccessAsync`): see endpoint 2.

### What "today" means

`AttendanceService.Today` is the **UTC date**, not the school's date. The school's time zone is not used here (see *Should fix* 1).

### Percentage formula (one place: `AttendanceRules.Percentage`)

Present = 1, Late = 1, Half Day = 0.5, Absent = 0. **Leave is left out** of the bottom number. Result is rounded to 1 decimal, and is `null` when nothing counts. Only **daily** attendance is counted, so subject-wise rows never double count.

---

## 1. `GET /api/attendance/roster`

### 1a. Purpose and caller
The register for one class on one date (optionally one subject): students, saved statuses, and whether the caller may edit.

### 1b. Request
Query string: `classSectionId` (int), `date` (`yyyy-MM-dd`), `subjectId` (optional). A missing `date` becomes `default` and gives 422.

### 1c. Response
`200 OK`, `AttendanceRosterResponse`: `classSectionId, className, date, subjectId, subjectName, isSaved, savedAtUtc, savedBy, canEdit, cannotEditReason, previousSessionDate, students[]`. Each student: `studentId, rollNumber, name, admNo, isActiveInClass, status, remarks, last7[]`.

`last7` holds the statuses of the previous (up to) 7 registers of the same kind, oldest first; `null` means the student had no record that day.

### 1d. Logic, in order
1. Policy `Attendance.Read`. Build the caller (`AttendanceActor`: user id, `CanManageAll` = holds `Attendance.Manage`, `IsAdmin`) and the data scope.
2. `date` must be given, else **422** `Date is required.`
3. Class must exist (404 `Class section could not be found.`). If a subject id is given, it must exist (404) and belong to the class (**422** `This subject does not belong to the selected class.`).
4. Scope check: a scoped caller who is not that class's class teacher (or that subject's teacher) gets the same **404**.
5. Work out `canEdit` and the reason (endpoint 2, step 3), without failing.
6. Load the saved session (if any), the roster students (**Active students of the class, plus anyone already recorded in this register**), the last 7 registers and their statuses (two queries), and the name of who last saved.

### 1e. Business rules and where each is enforced
| Rule | Where |
|---|---|
| Roster = Active students of the class + students already recorded | `GetRosterStudentsAsync` |
| A student who left the class but has a record shows with `isActiveInClass = false` | service |
| Students are sorted by roll number length, then text (so "2" before "10") | repository |
| Reading is allowed for any date, even the future (it just cannot be edited) | no date check on the read path |

### 1f. Errors
| Status | Code | When |
|---|---|---|
| 403 | `FORBIDDEN` | no read permission |
| 404 | `NOT_FOUND` | class or subject missing, or out of the caller's scope |
| 422 | `BUSINESS_RULE_VIOLATION` | no date; subject not in the class |

### 1g. Concurrency and retries
Read only. `canEdit` is a snapshot: the answer can change before the user saves.

### 1h. Why is it built this way?
One call gives the screen everything it needs, including why a register is locked.

### Example (invented)
```json
{ "classSectionId": 12, "className": "Class 5 A (2026-2027)", "date": "2026-10-19", "subjectId": null,
  "isSaved": true, "savedAtUtc": "2026-10-19T04:12:00Z", "savedBy": "meera.nair",
  "canEdit": true, "cannotEditReason": null, "previousSessionDate": "2026-10-18",
  "students": [ { "studentId": 301, "rollNumber": "1", "name": "Aisha Khan", "admNo": "ADM-2026-0001",
                  "isActiveInClass": true, "status": "Present", "remarks": null,
                  "last7": ["Present", "Absent", null, "Present", "Late", "Present", "Present"] } ] }
```

---

## 2. `PUT /api/attendance`

### 2a. Purpose and caller
Saves the whole register in one go: creates it the first time, replaces the statuses afterwards.

### 2b. Request
`SaveAttendanceRequest`:

| Field | Rule |
|---|---|
| `classSectionId` | > 0 |
| `date` | required (not the default date) |
| `subjectId` | optional, > 0 when given |
| `records` | not empty; no student twice |
| `records[].studentId` | > 0 |
| `records[].status` | `Present`, `Absent`, `Late`, `Half Day` or `Leave` |
| `records[].remarks` | optional, max 250 |

### 2c. Response
`200 OK`, the saved register (same shape as endpoint 1). It is **200**, not 201, even when a new register was created.

### 2d. Logic, in order
1. Validator (400 `VALIDATION_ERROR`).
2. Class and subject checks (as endpoint 1).
3. **Who may mark** (`EvaluateAccessAsync`), in this order:
   1. If the caller does **not** hold `Attendance.Manage`: they must be a teacher (else 403 `You are not allowed to mark attendance.`). For **daily** attendance they must be the class's class teacher (403 `Only the class teacher can take daily attendance for this class.`). For a **subject** they must be that subject's teacher (403 `Only the teacher assigned to this subject can take its attendance.`).
   2. **No future date**: **422** `Attendance cannot be marked for a future date.`
   3. A non-Admin may only touch **today and yesterday**: otherwise **422** `Only an Admin can change attendance older than 1 day.`
   4. A non-Admin cannot change a register **more than 24 hours after it was first saved** (measured from `CreatedAt`): **422** `This register was saved more than 24 hours ago. Only an Admin can change it now.`
4. Load the existing session **with tracking** (for update).
5. The set of allowed students = Active students of the class + anyone already recorded. Anyone submitted outside it gives **422** `Some students do not belong to this class.`
6. **Complete register rule**: every **Active** student of the class must have a status in the request, else **422** `3 student(s) have no status yet: A, B, C and 1 more.`
7. New register: insert the session and all records in one `SaveChanges` (one implicit transaction). Existing register: update the status and remarks of existing rows, add rows for new students, stamp `UpdatedAt/UpdatedBy`, save.
8. Rebuild and return the roster.

### 2e. Business rules and where each is enforced
| Rule | Where |
|---|---|
| One register per class+date (daily) and per class+date+subject | two filtered unique indexes |
| One record per student per register | unique index `(SessionId, StudentId)` |
| Status is one of five values | validator and `CK_AttendanceRecords_Status` |
| Records are never deleted: a student dropped from the request keeps the old record | service only updates or adds |
| Admin may change any past date and any age of register | `IsAdmin` |
| `Attendance.Manage` holders (Admin, Supervisor, Clerk per the code comment) may mark any class; only Admin escapes the date window and lock | `CanManageAll` vs `IsAdmin` |

### 2f. Errors
| Status | Code | When |
|---|---|---|
| 400 | `VALIDATION_ERROR` | request shape |
| 403 | `FORBIDDEN` | not that class's/subject's teacher |
| 404 | `NOT_FOUND` | class or subject missing |
| 409 | `CONFLICT` | two people created the same register at once |
| 422 | `BUSINESS_RULE_VIOLATION` | future date, too old, locked, students outside the class, missing statuses, subject not in class |

### 2g. Concurrency and retries
- **Creating** the same register twice at once: the second hits the unique index and `AttendanceRepository.SaveChangesAsync` turns it into **409** `Attendance for this class and date was just saved by someone else. Reload and try again.`
- **Editing** an existing register: there is no row version, so two simultaneous edits overwrite each other (last wins).
- Repeating the same request is safe: it just re-sets the same statuses.

### 2h. Why is it built this way?
Saving the whole register at once keeps it complete (no half-marked class) and keeps the rules in one transaction.

### Example (invented)
```json
{ "classSectionId": 12, "date": "2026-10-19", "subjectId": null,
  "records": [ { "studentId": 301, "status": "Present", "remarks": null },
               { "studentId": 302, "status": "Late", "remarks": "Bus delay" } ] }
```
If student 303 is Active in the class and missing from the list:
```json
{ "success": false, "message": "1 student(s) have no status yet: Rahul Das.", "errorCode": "BUSINESS_RULE_VIOLATION", "traceId": "0HN7K2..." }
```

---

## 3. `GET /api/attendance/students/{studentId}`

### 3a. Purpose and caller
One student's history and summary. Used on student screens and by a parent for their own child.

### 3b. Request
`studentId` in the URL; optional `from` and `to` (`yyyy-MM-dd`).

### 3c. Response
`200 OK`, `StudentAttendanceResponse`: `studentId, name, admNo, from, to, summary, records[]`. `summary` = `present, absent, late, halfDay, leave, total, percentage` (**daily only**). Each record: `date, mode` (`Daily` or `Subject Wise`), `subjectName, status, remarks`.

### 3d. Logic, in order
1. Policy `Attendance.Read`; scope resolved.
2. Student must be visible in the scope, else **404** `Student could not be found.` (missing and out of scope look the same).
3. `to` defaults to today (UTC), `from` defaults to 29 days earlier. `from` after `to` gives 422; a range over **366 days** gives 422.
4. Load the records the scope may see, newest first. A subject teacher only gets their own subject's rows.
5. Summary from the **daily** rows only; the list contains daily and subject-wise rows.

### 3e. Business rules
| Rule | Where |
|---|---|
| Summary counts daily attendance only | service |
| Max range 366 days | `AttendanceRules.MaxRangeDays` |
| A student sees the sessions of every class they were ever in (not just the current class) | the query is by student, not by class |

### 3f. Errors
403; 404 (no student or not in scope); 422 (range problems).

### 3g. Concurrency and retries
Read only.

### 3h. Why is it built this way?
Subject-wise rows are shown but never counted, so a student is not penalised twice for one day.

### Example (invented)
```json
{ "studentId": 301, "name": "Aisha Khan", "admNo": "ADM-2026-0001", "from": "2026-09-20", "to": "2026-10-19",
  "summary": { "present": 18, "absent": 1, "late": 2, "halfDay": 0, "leave": 1, "total": 22, "percentage": 95.2 },
  "records": [ { "date": "2026-10-19", "mode": "Daily", "subjectName": null, "status": "Present", "remarks": null } ] }
```

---

## 4. `GET /api/attendance/summary`

### 4a. Purpose and caller
Per-student totals and percentage for a class over a date range (daily attendance only).

### 4b. Request
Query string: `classSectionId`, `from`, `to` (both required, `yyyy-MM-dd`).

### 4c. Response
`200 OK`, `ClassAttendanceSummaryResponse`: `classSectionId, className, from, to, daysMarked, classPercentage, students[]` where each student has `studentId, rollNumber, name, admNo, counts`.

### 4d. Logic, in order
1. Policy; scope.
2. Both dates are required (422) and the range must be valid (`from` ≤ `to`, at most 366 days).
3. Class exists (404). A scoped caller must be the **class teacher** of the class: a subject teacher gets 404 here, because the summary is the whole class's daily attendance.
4. Grouped counts per student and status (one query); the number of daily registers taken in the range (`daysMarked`); the students (Active students of the class plus anyone with a record in the range).
5. Class percentage = the same formula applied to the **totals** of all students.

### 4e. Business rules
| Rule | Where |
|---|---|
| Daily registers only | repository |
| Class percentage is weighted by days, not an average of the students' percentages | service |
| A student who left the class mid-range still appears if they have records | repository |

### 4f. Errors
403; 404; 422 (missing date, bad range).

### 4g. Concurrency and retries
Read only. The numbers can change while someone is still saving.

### 4h. Why is it built this way?
One aggregate query per class is far cheaper than loading every record.

### Example (invented)
```json
{ "classSectionId": 12, "className": "Class 5 A (2026-2027)", "from": "2026-10-01", "to": "2026-10-19",
  "daysMarked": 14, "classPercentage": 93.4, "students": [ { "studentId": 301, "rollNumber": "1", "name": "Aisha Khan",
  "admNo": "ADM-2026-0001", "counts": { "present": 13, "absent": 0, "late": 1, "halfDay": 0, "leave": 0, "total": 14, "percentage": 100.0 } } ] }
```

---

## Doubts and possible problems

### Blocker
None found.

### Should fix
1. **"Today" and the date window use UTC, not the school's time zone.** The school is in Abu Dhabi (UTC+4). Between 00:00 and 04:00 local time, the UTC date is still yesterday, so "today's" register is rejected as a future date and "yesterday" is treated as today. Probably unnoticed during school hours, but it is wrong. The fix is the same `SchoolTimeZones.Today(...)` already used for admissions. The 24-hour lock uses real UTC elapsed time and is fine either way. **Waiting for your decision.**
2. **Editing an existing register has no concurrency check.** Two staff saving the same register overwrite each other. Create is protected (409); edit is not. Add a row version on `AttendanceSessions` if this matters.
3. **Marking is allowed for any class, even an Inactive or past-year one**, by `Attendance.Manage` holders (and by a class teacher of an old class). Nothing checks `ClassSectionRules.EnsureActive` or the academic year.
4. **`AttendanceRecord.UpdatedAt` is never updated on edit.** It is set once by the database default. The session's `UpdatedAt` is updated, so only the per-student timestamp is stale. Also records are not audited, so a changed status leaves no history of who changed it from what to what.
5. **Missing tests.** The save rules (complete roster, class-teacher-only, future date, 2-day window, unknown students) and the percentage formula have no tests.
6. **Date missing on the roster call gives 422 "Date is required."** instead of 400 from model binding. Fine, but inconsistent with the save call (400).

### Question
1. **Should a teacher be able to mark attendance for a date before the student joined the class**, or for a student who joined after that date? The roster uses the student's *current* class, not where they were on that date. A student who moved class shows in the new class for old dates (Active) and in the old class only if already recorded.
2. **Should parents and students be able to read attendance at all?** The scope logic supports it (own child, self). Confirm which roles actually hold `Attendance.ViewScoped`.
3. **Should "Leave" count as present in the percentage?** Today it is excluded from the bottom number, so approved leave neither helps nor hurts.
4. **Lock of 24 hours after first save, plus "today and yesterday"**: for a register first saved yesterday morning, a teacher is locked out by the evening even though the date rule would still allow it. Confirm this double rule is intended.
5. **Should attendance for a student who is `Inactive` or `Left` be hidden from the roster completely?** They show only if they already have a record.
6. **Public holidays and weekends:** nothing stops a register on a day the school is closed, and `daysMarked` counts it. Is a holiday calendar planned?
