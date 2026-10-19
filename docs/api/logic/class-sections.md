# Class sections controller: what each endpoint actually does

Source: `backend/SchoolManagement.API/Controllers/ClassSectionsController.cs`, route prefix `api/class-sections`.
Written for a developer with about two years of experience who is learning this codebase.

## How this file was made

- **Everything here was read from the code**: the controller, `ClassSectionService`, `ClassSectionRepository`, `ClassSectionRules`, the two validators, the `ClassSection` entity and `ClassSectionConfiguration`, plus the React service and the pages that call it.
- **I did not run the API or the database.** There is no SQL Server in my environment. So **every example below is invented**: shapes, field names, error codes and messages come from the code, but ids, names and numbers are made up.
- "Tests exist" means a test file covering the rule is in `backend/SchoolManagement.Tests`. I did not run them in this session.
- **Not verified** marks anything I could not confirm from the code alone.
- The doubts I noticed are at the end, labelled **Should fix** or **Question**. There is no **Blocker** in this controller.

## The six endpoints at a glance

| # | Method + URL | Permission needed | Called by (React) |
|---|---|---|---|
| 1 | `GET /api/class-sections` | `ClassSections.View` | `ClassSectionsPage`, and the class drop-downs on the admission form, student edit, attendance, subjects and teacher assignment pages |
| 2 | `GET /api/class-sections/{id}` | `ClassSections.View` | `ClassSectionDetailPage`, `ClassSectionFormPage` (edit) |
| 3 | `POST /api/class-sections` | `ClassSections.Create` | `ClassSectionFormPage` (new) |
| 4 | `PUT /api/class-sections/{id}` | `ClassSections.Edit` | `ClassSectionFormPage` (edit) |
| 5 | `PATCH /api/class-sections/{id}/status` | `ClassSections.Edit` | `ClassSectionsPage`, `ClassSectionDetailPage` |
| 6 | `DELETE /api/class-sections/{id}` | `ClassSections.Delete` | `ClassSectionsPage`, `ClassSectionDetailPage` |

There is no scoping here: anyone who holds `ClassSections.View` sees every class. (Students have a scoped read; classes do not.)

## Background you need before the endpoints

### The same request pipeline as Auth

Every call goes through correlation id, Serilog request logging, `GlobalExceptionMiddleware`, JWT authentication, `MustChangePasswordMiddleware`, then the permission policy on the action. See `auth.md` for the details. A refused permission is **403** with the standard error body.

### What a class section is

One row per **class + section + academic year**, for example "Class 5", section "A", year "2026-2027". It holds the capacity, the room, the stage / medium / stream, the status (`Active` or `Inactive`) and the class (homeroom) teacher.

- `DisplayName` is computed, never stored: `"{Name} {Section} ({AcademicYear})"`, for example `Class 5 A (2026-2027)`.
- `Code` is computed too: `"Class 5"` + `"A"` becomes `C5A`.
- Years are separate rows: "Class 5 A" in 2026-2027 and in 2027-2028 are two different sections. Students are tied to one section (so one year). Promotion to the next year is not built.

### Seats

`Enrolled` in the response is the number of students in the section whose status is **Active**. `Inactive` and `Left` students do not take a seat. Everything that gives out a seat (create student, enrol, move class, reactivate, restore) takes the app lock `ClassSectionSeats:{id}` (`sp_getapplock`) and checks the count inside the same transaction.

### Soft delete, audit and concurrency

- Deleted sections are hidden by a query filter (`IsDeleted = 0`).
- `AuditableEntitySaveChangesInterceptor` stamps created/updated/deleted at and by, and writes an `AuditLogs` row for every create, edit and delete.
- `ClassSections.RowVersion` is a SQL Server `rowversion`. The edit page sends it back and `PUT` checks it (`IConcurrencyGuard.Expect`).

### Who sets the class teacher

Not this controller. The class teacher is assigned from the Teachers screens (`/api/teachers/{id}/class-teacher/...`, see `teachers.md`). This controller only returns the teacher's id and name.

---

## 1. `GET /api/class-sections`

### 1a. Purpose and caller

Returns every class section. It feeds the Classes list and every screen that needs a class drop-down.

### 1b. Request

No body, no query string. There is **no paging, filtering or sorting option**: the filters on the Classes page run in the browser.

### 1c. Response

`200 OK`, a JSON array of `ClassSectionResponse`:
`id, name, section, code, grade, stage, medium, stream, capacity, enrolled, building, floor, room, status, displayName, academicYearId, academicYearName, classTeacherId, classTeacherName, rowVersion`.

`rowVersion` is a base64 string.

### 1d. Logic, in order

1. Policy check `ClassSections.View`.
2. `GetAllAsync`: all non-deleted sections with their academic year and class teacher (teacher, user and staff profile are loaded for the name). Sorted by **year start date, newest first**, then grade (null counts as 0, so LKG/UKG come first), then name, then section.
3. `GetEnrolledCountsAsync`: **one** query that counts Active students grouped by section. So there is no N+1.
4. The service maps each row, taking `enrolled` from the dictionary (0 when the section has no Active students).

### 1e. Business rules and where each is enforced

| Rule | Where |
|---|---|
| Only non-deleted sections | query filter on `ClassSections` |
| `Enrolled` counts Active students only | `ClassSectionRepository.GetEnrolledCountsAsync` |

### 1f. Errors

| Status | When |
|---|---|
| 401 | no or invalid token |
| 403 | no `ClassSections.View` |

### 1g. Concurrency and retries

Read only. Safe to retry. The count can be a moment old by the time the screen shows it.

### 1h. Why is it built this way?

The list is small at school scale (tens of sections), so one query for the sections and one for the counts is simpler than paging.

### Example (invented)

```json
[
  { "id": 12, "name": "Class 5", "section": "A", "code": "C5A", "grade": 5, "stage": "Primary",
    "medium": "English", "stream": "General", "capacity": 30, "enrolled": 28,
    "building": "Main", "floor": 1, "room": "105", "status": "Active",
    "displayName": "Class 5 A (2026-2027)", "academicYearId": 3, "academicYearName": "2026-2027",
    "classTeacherId": 7, "classTeacherName": "Meera Nair", "rowVersion": "AAAAAAAAB9E=" }
]
```

---

## 2. `GET /api/class-sections/{id}`

### 2a. Purpose and caller

One section, with its current strength. Used by the detail page and to fill the edit form.

### 2b. Request

`id` in the URL (integer; a non-number does not match the route and gives 404).

### 2c. Response

`200 OK`, one `ClassSectionResponse` (same shape as endpoint 1).

### 2d. Logic, in order

1. Policy `ClassSections.View`.
2. `GetByIdAsync` (with year and class teacher). Missing or deleted gives **404** `Class section {id} does not exist.`
3. `CountActiveStudentsAsync` for this section.
4. Map and return.

### 2e. Business rules and where each is enforced

Same as endpoint 1.

### 2f. Errors

| Status | Code | When |
|---|---|---|
| 403 | `FORBIDDEN` | no `ClassSections.View` |
| 404 | `NOT_FOUND` | unknown or deleted id |

### 2g. Concurrency and retries

Read only. The returned `rowVersion` is what the edit page must send back.

### 2h. Why is it built this way?

The detail page needs the live strength, so it uses the same Active-student count as the list.

### Example (invented)

`GET /api/class-sections/999` returns:

```json
{ "success": false, "message": "Class section 999 does not exist.", "errorCode": "NOT_FOUND", "traceId": "0HN7K2..." }
```

---

## 3. `POST /api/class-sections`

### 3a. Purpose and caller

Creates a section. Called by the new-class form.

### 3b. Request

JSON `CreateClassSectionRequest`:

| Field | Rule |
|---|---|
| `name` | required, max 50 |
| `section` | required, max 10 |
| `grade` | optional, 1 to 12 |
| `stage` | one of `Pre-Primary`, `Primary`, `Secondary` |
| `medium` | one of `English`, `Hindi`, `Arabic` |
| `stream` | one of `General`, `Science`, `Commerce` |
| `capacity` | **required**, 1 to 200 |
| `building` | optional, max 50 |
| `floor` | optional, 0 to 20 |
| `room` | optional, max 30 |
| `academicYearId` | optional, greater than 0; **omitted = the year marked current** |
| `isActive` | default `true` |

### 3c. Response

`201 Created`, `Location: api/class-sections/{id}`, body `ClassSectionResponse` with `enrolled = 0`.

### 3d. Logic, in order

1. Controller runs `CreateClassSectionRequestValidator`. Failure gives **400** `VALIDATION_ERROR` with a field map.
2. Pick the academic year: the given id, or the one with `IsCurrent`. If none is found the answer is **404** (`Academic year {id} does not exist.` or `No academic year is currently marked as active.`).
3. Trim `name` and `section`. If the same year already has a section with that name and letter: **409** `'Class 5 A' already exists for 2026-2027.`
4. Insert with `Status = Active` or `Inactive` from `isActive`.
5. Return the created row (the year is attached in memory, so there is no second query).

### 3e. Business rules and where each is enforced

| Rule | Where |
|---|---|
| Name + section unique per year | pre-check `ExistsAsync`; final authority: unique index on `(AcademicYearId, Name, Section)` filtered to `IsDeleted = 0` (a deleted section does not block re-creating the same name) |
| Capacity 1 to 200 | validator; database check `CK_ClassSections_Capacity` (`NULL` or `> 0`) |
| Floor 0 or more | validator; `CK_ClassSections_Floor` |
| Status is Active or Inactive | `CK_ClassSections_Status` |
| Section belongs to exactly one year | foreign key, `DeleteBehavior.Restrict` |

### 3f. Errors

| Status | Code | When |
|---|---|---|
| 400 | `VALIDATION_ERROR` | any validator rule |
| 403 | `FORBIDDEN` | no `ClassSections.Create` |
| 404 | `NOT_FOUND` | the academic year does not exist, or none is current |
| 409 | `CONFLICT` | same name and section already exist in that year |

### 3g. Concurrency and retries

Two people creating the same section at the same moment: the pre-check passes for both, then the unique index rejects the second. `AddAsync` calls `SaveChanges` directly and **does not turn that SQL error into a 409**, so the second caller would get a **500**. See *Should fix* 1. A retry after a timeout may return 409 if the first call actually succeeded.

### 3h. Why is it built this way?

Defaulting to the current year keeps the form short. The friendly pre-check gives a readable message; the index is the real guard.

### Example (invented)

```json
{ "name": "Class 5", "section": "A", "grade": 5, "stage": "Primary", "medium": "English",
  "stream": "General", "capacity": 30, "building": "Main", "floor": 1, "room": "105" }
```

If it already exists in the current year:

```json
{ "success": false, "message": "'Class 5 A' already exists for 2026-2027.", "errorCode": "CONFLICT", "traceId": "0HN7K2..." }
```

---

## 4. `PUT /api/class-sections/{id}`

### 4a. Purpose and caller

Saves the edit form. It cannot change the academic year or the class teacher.

### 4b. Request

JSON `UpdateClassSectionRequest`: the same fields as create (without `academicYearId`) plus `isActive` and a **required** `rowVersion`. The validator checks the row version is well formed (`RowVersionRules.IsValid`) and applies the same field rules as create.

### 4c. Response

`200 OK`, the updated `ClassSectionResponse` (with a new `rowVersion`).

### 4d. Logic, in order

1. Validator (400 on failure).
2. Load the section (404 if missing).
3. `IConcurrencyGuard.Expect(section, request.RowVersion)`: sets the original row version, so a stale form makes SQL Server update zero rows and the save fails with **409** `CONCURRENCY_CONFLICT` (handled by `GlobalExceptionMiddleware`).
4. Name + section must stay unique in the section's own year (excluding itself): **409** otherwise.
5. Count Active students. If the new `capacity` is lower: **409** `Capacity cannot be lower than the current strength (N students).`
6. Copy the fields, set `Status` from `isActive`, save, return.

### 4e. Business rules and where each is enforced

| Rule | Where |
|---|---|
| Stale edit refused | `rowversion` plus `IConcurrencyGuard` (test: `RowVersionTests.A_second_class_save_with_an_out_of_date_version_is_refused`) |
| Capacity cannot drop below current strength | service (counts Active students) |
| Name + section unique in the year | service pre-check and the filtered unique index |
| Setting `isActive = false` is allowed with students inside | no rule; the students stay, the section stops accepting new ones (`ClassSectionRules.EnsureActive`) |

### 4f. Errors

| Status | Code | When |
|---|---|---|
| 400 | `VALIDATION_ERROR` | any validator rule, or a malformed `rowVersion` |
| 403 | `FORBIDDEN` | no `ClassSections.Edit` |
| 404 | `NOT_FOUND` | unknown id |
| 409 | `CONFLICT` | duplicate name+section, capacity below strength |
| 409 | `CONCURRENCY_CONFLICT` | someone saved this class after it was loaded |

### 4g. Concurrency and retries

The row version makes a lost update impossible: the second saver is told to reload. Renaming a section does not rename existing students' roll numbers; only **new** generated roll numbers use the new name (`NumberFormats.RollPrefix`).

### 4h. Why is it built this way?

Capacity is checked against Active students because only they take seats. The year is fixed after creation because students and subjects belong to it.

### Example (invented)

Section 12 has 28 Active students and the form lowers capacity to 25:

```json
{ "success": false, "message": "Capacity cannot be lower than the current strength (28 students).",
  "errorCode": "CONFLICT", "traceId": "0HN7K2..." }
```

---

## 5. `PATCH /api/class-sections/{id}/status`

### 5a. Purpose and caller

Switches a section between Active and Inactive from the list or detail page without opening the form.

### 5b. Request

`{ "isActive": true }` (`ChangeClassSectionStatusRequest`). There is **no validator and no `rowVersion`**.

### 5c. Response

`200 OK`, the updated `ClassSectionResponse`.

### 5d. Logic, in order

1. Policy `ClassSections.Edit`.
2. Load the section (404 if missing).
3. Set `Status`, save.
4. Count Active students and return the response.

### 5e. Business rules and where each is enforced

| Rule | Where |
|---|---|
| An Inactive section cannot accept new admissions or students | `ClassSectionRules.EnsureActive`, called when a section is chosen for registering, enrolling, creating a student or moving/reactivating one |
| Existing students are not touched | no code moves them |

### 5f. Errors

| Status | Code | When |
|---|---|---|
| 403 | `FORBIDDEN` | no `ClassSections.Edit` |
| 404 | `NOT_FOUND` | unknown id |

### 5g. Concurrency and retries

No version check, so the last write wins. Harmless here: the value is a single flag and repeating the same call gives the same result. Calling it also bumps the row version, so an edit form opened before the toggle will get a 409 when saved (the user must reload).

### 5h. Why is it built this way?

A one-click toggle for a one-field change.

### Example (invented)

```json
{ "isActive": false }
```

---

## 6. `DELETE /api/class-sections/{id}`

### 6a. Purpose and caller

Deletes a section that was created by mistake. Called from the list and detail pages.

### 6b. Request

`id` in the URL, no body.

### 6c. Response

`204 No Content`.

### 6d. Logic, in order

1. Policy `ClassSections.Delete`.
2. Load the section (404 if missing).
3. `GetDependenciesAsync` counts: students in the section (**any status**), admissions that applied for or were allotted to it, and subjects.
4. If any count is above zero: **409** `Cannot delete 'Class 5 A (2026-2027)' — it still has 28 student(s), 3 admission(s), 4 subject(s). Mark it Inactive instead.`
5. Otherwise soft delete: `IsDeleted = true`, `DeletedAt`. The audit interceptor adds `DeletedBy` and the audit row.

### 6e. Business rules and where each is enforced

| Rule | Where |
|---|---|
| A section with students, admissions or subjects cannot be deleted | service (`GetDependenciesAsync`) |
| Deleting the section does not delete anything else | soft delete only; the foreign keys are `Restrict` |
| A deleted section frees its name | unique index is filtered on `IsDeleted = 0` |

### 6f. Errors

| Status | Code | When |
|---|---|---|
| 403 | `FORBIDDEN` | no `ClassSections.Delete` |
| 404 | `NOT_FOUND` | unknown id |
| 409 | `CONFLICT` | dependencies exist |

### 6g. Concurrency and retries

The dependency check and the delete are not in one transaction, so a student could be added between them. The window is tiny. A second delete of the same id returns 404.

### 6h. Why is it built this way?

Class history must not disappear, so the only way to retire a used class is to mark it Inactive. Students with status `Left` still count, which means a class that ever had a student can never be deleted.

### Example (invented)

```json
{ "success": false,
  "message": "Cannot delete 'Class 5 A (2026-2027)' — it still has 28 student(s), 3 subject(s). Mark it Inactive instead.",
  "errorCode": "CONFLICT", "traceId": "0HN7K2..." }
```

---

## Doubts and possible problems

### Should fix

1. **Duplicate create race returns 500.** `ClassSectionRepository.AddAsync` does not catch SQL errors 2601/2627 the way the student, parent and user repositories do. Two simultaneous creates of the same name + section would give the second caller a 500 instead of the 409 message. Low risk (one person creates classes), small fix.

### Question

1. **There is no endpoint to manage academic years.** Create defaults to "the year marked current", and the only way to have one is seeded data. How will a new year be created and made current next year?
2. **Should a section in a closed or past academic year accept new students or edits?** Today only `Active` / `Inactive` is checked, not the year.
3. **Should the status toggle (endpoint 5) take the row version**, like the edit form, so a stale page cannot flip it?
4. **Should deactivating a section with Active students be blocked or warn?** It is allowed silently now.
5. **Capacity is required (1 to 200) on create and edit, but the column and the seat rule allow `NULL` meaning "no limit".** Is "no limit" still wanted? Old rows may have it.
6. **The list is unpaged and filtered in the browser.** Fine at this size; confirm that is the plan.
7. **`Name` is free text** ("Class 5", "LKG", "Grade 5"). Roll numbers and the `Code` are built from it, so inconsistent names give inconsistent prefixes. Should names come from a Masters list?
