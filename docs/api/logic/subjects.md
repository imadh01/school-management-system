# Subjects controller: what each endpoint actually does

Source: `backend/SchoolManagement.API/Controllers/SubjectsController.cs`, route prefix `api/subjects`.
Written for a developer with about two years of experience who is learning this codebase.

## How this file was made

- **Everything here was read from the code**: the controller, `SubjectService`, `SubjectRepository`, the two validators, the `Subject` entity and `SubjectConfiguration`, the DTOs, the React service and form, and the places that use subjects (teacher assignment, attendance, data scope, class delete).
- **I did not run the API or the database.** There is no SQL Server in my environment. So **every example below is invented**: shapes, field names, error codes and messages come from the code, but ids, names and numbers are made up.
- **Not verified** marks anything I could not confirm from the code alone.
- There is **no test file for subjects**. The only coverage is the access matrix (permission per endpoint) and the data-scope tests that read subjects. Nothing tests the marks rules, the code rules or the duplicate rule.
- The doubts I noticed are at the end, labelled **Blocker**, **Should fix** or **Question**.

## The five endpoints at a glance

| # | Method + URL | Permission needed | Called by (React) |
|---|---|---|---|
| 1 | `GET /api/subjects` | `Subjects.View` | `SubjectsPage`, class detail, attendance and teacher-assignment drop-downs |
| 2 | `GET /api/subjects/{id}` | `Subjects.View` | `SubjectFormPage` (edit) |
| 3 | `POST /api/subjects` | `Subjects.Create` | `SubjectFormPage` (new) |
| 4 | `PUT /api/subjects/{id}` | `Subjects.Edit` | `SubjectFormPage` (edit) |
| 5 | `DELETE /api/subjects/{id}` | `Subjects.Delete` | `SubjectsPage` |

There is no scoping: anyone with `Subjects.View` sees every subject. There is no status-toggle endpoint (the form's Active switch is saved with PUT).

## Background you need before the endpoints

### The same request pipeline as Auth

Correlation id, Serilog, `GlobalExceptionMiddleware`, JWT, `MustChangePasswordMiddleware`, then the permission policy. See `auth.md`. Refused permission is **403** with the standard error body.

### What a subject is

One row per **subject in one class section** (so "Mathematics" in Class 5 A is a different row from "Mathematics" in Class 5 B, and from the same class in another year). It holds:

- `Name` and `Code` (for example `MATH`),
- the class section,
- `Type`: `Theory`, `Practical` or `Both`,
- the marks: `MaxMarks` / `PassMarks` for Theory and Practical; `TheoryMax/Pass` and `PracticalMax/Pass` for Both,
- `Status` (`Active` or `Inactive`),
- the teacher (`TeacherId`, `null` = nobody).

### Who sets the teacher

Not this controller. The teacher is assigned and removed from the Teachers screens (`/api/teachers/{id}/subjects`, see `teachers.md`). A subject can have **one** teacher. This controller only returns the teacher's id and name, and neither create nor edit can change it.

### Marks storage (one set at a time)

The service wipes the unused set every time: for `Both` the single `MaxMarks`/`PassMarks` become `null`; for the other types the four theory/practical values become `null`. The database check `CK_Subjects_MarksByType` enforces the same.

### Soft delete, audit

- Deleted subjects are hidden by a query filter that also hides subjects **of a deleted class section**.
- `AuditableEntitySaveChangesInterceptor` stamps created/updated/deleted at and by and writes `AuditLogs` rows.
- **There is no `RowVersion` on `Subjects`.** Two people editing at once: the last save wins.

---

## 1. `GET /api/subjects`

### 1a. Purpose and caller

Returns every subject. It feeds the Subjects list and the subject drop-downs.

### 1b. Request

No body and no query string. No paging, filter or sort option: the filters run in the browser.

### 1c. Response

`200 OK`, a JSON array of `SubjectResponse`:
`id, name, code, classSectionId, classSectionName, type, maxMarks, passMarks, theoryMax, theoryPass, practicalMax, practicalPass, status, teacherId, teacherName`.

`classSectionName` is the class's display name, for example `Class 5 A (2026-2027)`.

### 1d. Logic, in order

1. Policy `Subjects.View`.
2. One query: subjects with class section, academic year and teacher (teacher, user and staff profile for the name). Sorted by **year start (newest first)**, grade (null as 0), class name, section, then subject name.
3. Map each row.

### 1e. Business rules and where each is enforced

| Rule | Where |
|---|---|
| Deleted subjects and subjects of deleted classes are hidden | query filter on `Subjects` |

### 1f. Errors

| Status | When |
|---|---|
| 401 | no or invalid token |
| 403 | no `Subjects.View` |

### 1g. Concurrency and retries

Read only, safe to retry.

### 1h. Why is it built this way?

One joined query is enough at school size, so no paging.

### Example (invented)

```json
[
  { "id": 41, "name": "Mathematics", "code": "MATH", "classSectionId": 12,
    "classSectionName": "Class 5 A (2026-2027)", "type": "Theory",
    "maxMarks": 100, "passMarks": 33, "theoryMax": null, "theoryPass": null,
    "practicalMax": null, "practicalPass": null, "status": "Active",
    "teacherId": 7, "teacherName": "Meera Nair" }
]
```

---

## 2. `GET /api/subjects/{id}`

### 2a. Purpose and caller

One subject, to fill the edit form.

### 2b. Request

`id` in the URL (integer).

### 2c. Response

`200 OK`, one `SubjectResponse`.

### 2d. Logic, in order

1. Policy `Subjects.View`.
2. Load the subject with its class and teacher. Missing, deleted, or in a deleted class gives **404** `Subject could not be found.`

### 2e. Business rules

Same filter as endpoint 1.

### 2f. Errors

| Status | Code | When |
|---|---|---|
| 403 | `FORBIDDEN` | no `Subjects.View` |
| 404 | `NOT_FOUND` | unknown or deleted id |

### 2g. Concurrency and retries

Read only. The response has **no row version**, because the table has none.

### 2h. Why is it built this way?

The form needs the stored marks and the teacher name for the header.

### Example (invented)

```json
{ "success": false, "message": "Subject could not be found.", "errorCode": "NOT_FOUND", "traceId": "0HN7K2..." }
```

---

## 3. `POST /api/subjects`

### 3a. Purpose and caller

Adds a subject to a class. Called by the new-subject form.

### 3b. Request

JSON `CreateSubjectRequest`:

| Field | Rule |
|---|---|
| `name` | required, max 100 |
| `code` | optional, max 20; blank = generated |
| `classSectionId` | required, greater than 0 |
| `type` | required: `Theory`, `Practical` or `Both` |
| `maxMarks`, `passMarks` | required when `type` is not `Both` |
| `theoryMax`, `theoryPass`, `practicalMax`, `practicalPass` | all four required when `type` is `Both` |
| `isActive` | boolean |

The validator only checks that the marks are **present**. It does not check they are positive or that pass is not above max (see *Should fix* 1 and 2).

### 3c. Response

`201 Created`, `Location: api/subjects/{id}`, body `SubjectResponse` (teacher is `null`).

### 3d. Logic, in order

1. Controller runs `CreateSubjectRequestValidator`. Failure gives **400** `VALIDATION_ERROR` with a field map.
2. **Work out the code** (`ResolveCode`): if `code` was sent, trim and upper-case it. Otherwise use the fixed list (`Mathematics` → `MATH`, `English` → `ENG`, `Science` → `SCI`, `Computer Science` → `CS`, `Art` → `ART`). Otherwise take the **first four letters of the name**, upper-cased (shorter names are used whole).
3. If the same code already exists in that class: **409** `Subject Code + Class must be unique. This combination already exists.`
4. Build the subject, fill only the marks set that fits the type, insert.
5. Reload with class and teacher and return it.

### 3e. Business rules and where each is enforced

| Rule | Where |
|---|---|
| Code unique per class | service pre-check; final guard is the unique index on `(Code, ClassSectionId)` filtered to `IsDeleted = 0` (a deleted subject's code can be reused) |
| Name is **not** unique | nothing: two subjects named "Mathematics" with different codes are allowed |
| Marks match the type | validator (presence) and `CK_Subjects_MarksByType` |
| Pass not above max | database check `CK_Subjects_PassNotAboveMax` only |
| Status is Active or Inactive | `CK_Subjects_Status` |
| The class section must exist | foreign key only (see *Should fix* 3) |

### 3f. Errors

| Status | Code | When |
|---|---|---|
| 400 | `VALIDATION_ERROR` | name/type/marks-presence rules |
| 403 | `FORBIDDEN` | no `Subjects.Create` |
| 409 | `CONFLICT` | code already used in that class |
| 500 | `INTERNAL_SERVER_ERROR` | class id does not exist, or pass above max (see doubts) |

### 3g. Concurrency and retries

Two creates of the same code at once: both pass the pre-check, the unique index rejects the second, and `AddAsync` does not translate that error, so the second caller gets a **500**. A retry after a timeout may return 409 if the first call succeeded.

### 3h. Why is it built this way?

Generating a code from the name keeps the form short; the school can still type its own.

### Example (invented)

```json
{ "name": "Social Studies", "code": null, "classSectionId": 12, "type": "Both",
  "maxMarks": null, "passMarks": null,
  "theoryMax": 80, "theoryPass": 26, "practicalMax": 20, "practicalPass": 7, "isActive": true }
```

The generated code is `SOCI`. If "Social Science" already exists in that class it also got `SOCI`, and the answer is:

```json
{ "success": false, "message": "Subject Code + Class must be unique. This combination already exists.",
  "errorCode": "CONFLICT", "traceId": "0HN7K2..." }
```

---

## 4. `PUT /api/subjects/{id}`

### 4a. Purpose and caller

Saves the edit form: everything except the teacher.

### 4b. Request

`UpdateSubjectRequest`: the same fields and the same validator rules as create (the messages for missing marks are the default FluentValidation text, not the friendly ones). **No row version.**

### 4c. Response

`200 OK`, the updated `SubjectResponse`.

### 4d. Logic, in order

1. Validator (400 on failure).
2. Load the subject (404 if missing).
3. Work out the code the same way as create, then check it is unique in the **requested** class, excluding this subject: **409** otherwise.
4. Copy name, code, **class**, type, status and the right marks set; save; reload; return.

Note the code is **recalculated on every save**: if the code box is blank, it falls back to the generated one, even when the subject had a custom code. The form fills the box with the current code, so this normally does not happen.

### 4e. Business rules and where each is enforced

Same as create, plus:

| Rule | Where |
|---|---|
| The class can be changed | no rule stops it; the assigned teacher stays on the subject (see *Should fix* 4) |

### 4f. Errors

| Status | Code | When |
|---|---|---|
| 400 | `VALIDATION_ERROR` | field rules |
| 403 | `FORBIDDEN` | no `Subjects.Edit` |
| 404 | `NOT_FOUND` | unknown id |
| 409 | `CONFLICT` | code already used in the target class |
| 500 | `INTERNAL_SERVER_ERROR` | target class does not exist, or pass above max |

### 4g. Concurrency and retries

No version check: two editors overwrite each other silently. Repeating the same PUT gives the same result.

### 4h. Why is it built this way?

It mirrors create, so the form is the same for both.

### Example (invented)

```json
{ "name": "Mathematics", "code": "MATH", "classSectionId": 13, "type": "Theory",
  "maxMarks": 100, "passMarks": 35, "theoryMax": null, "theoryPass": null,
  "practicalMax": null, "practicalPass": null, "isActive": true }
```

---

## 5. `DELETE /api/subjects/{id}`

### 5a. Purpose and caller

Removes a subject created by mistake. Called from the Subjects list.

### 5b. Request

`id` in the URL, no body.

### 5c. Response

`204 No Content`.

### 5d. Logic, in order

1. Policy `Subjects.Delete`.
2. Load the subject (404 if missing).
3. Soft delete: `IsDeleted = true`, `DeletedAt`. The interceptor adds `DeletedBy` and the audit row.

**There is no dependency check.** A subject with an assigned teacher, or with attendance sessions already taken, is deleted without a word.

### 5e. Business rules and where each is enforced

| Rule | Where |
|---|---|
| Soft delete only | service |
| The code can be reused after delete | filtered unique index |
| The teacher's workload drops | automatic: the teacher's subject count is a query that excludes deleted subjects |

### 5f. Errors

| Status | Code | When |
|---|---|---|
| 403 | `FORBIDDEN` | no `Subjects.Delete` |
| 404 | `NOT_FOUND` | unknown id, or already deleted |

### 5g. Concurrency and retries

A second delete returns 404.

### 5h. Why is it built this way?

Simple soft delete. Unlike class sections, it does not protect history (see *Should fix* 5).

### Example (invented)

`DELETE /api/subjects/41` returns `204` with no body.

---

## Doubts and possible problems

### Blocker

None found in this controller.

### Should fix

1. **Marks are not range-checked.** A negative number, or `0` max, passes the validator. The form uses `min=0` in the browser only.
2. **Pass above max gives a 500 instead of a 400.** The database check `CK_Subjects_PassNotAboveMax` rejects it, but the validator does not, so the user sees a server error. The form checks it in the browser (`checkMarks`), so only direct API calls hit it. Add a rule to both validators.
3. **A class section that does not exist gives a 500** (foreign-key error). A **deleted** class passes the foreign key (soft delete keeps the row) and the subject is created but is immediately invisible, because the query filter hides subjects of deleted classes. An **Inactive** class is accepted too. Add a check: class must exist and be Active (as admissions and students already do with `ClassSectionRules.EnsureActive`).
4. **Changing a subject's class keeps its teacher.** The teacher assignment rule says a teacher's subjects must be in a class they were assigned to; moving the subject skips that. Either clear `TeacherId` on a class change or block the change when a teacher is assigned.
5. **Delete has no dependency check.** A subject with a teacher or with attendance sessions is deleted silently, and subject-wise attendance sessions then point to a hidden subject (the subject name disappears from attendance history). Class sections refuse delete in the same situation. Decide: block (409) or allow.
6. **Duplicate-create race returns 500.** `SubjectRepository.AddAsync` does not translate the unique-index error to 409 as the student, parent and user repositories do. Low risk, small fix.
7. **No row version.** Concurrent edits overwrite silently. Class sections, students and parents already use `rowVersion`.
8. **No tests.** Nothing covers code generation, the unique code rule, the marks sets or delete. The code generator is the most error-prone part.

### Question

1. **Code generation collides easily.** "Social Studies" and "Social Science" both become `SOCI`; "Physical Education" and "Physics" become `PHYS` and `PHYS`. The user must then type a code, but the error message does not say that a code was auto-generated. Do you want a nicer message, or a longer fallback?
2. **Should the same subject name be allowed twice in one class** (with different codes)? Today yes.
3. **Is `Type` (`Theory`/`Practical`/`Both`) enough for the Exams module later?** The marks live on the subject, not on an exam, so one pass mark applies to all exams. Confirm that is the plan.
4. **Subjects are copied class by class.** There is no "copy the subjects from last year's section" action, so every new year means retyping them. Is a bulk copy wanted?
5. **Is the `Both` type's total max (theory + practical) shown anywhere?** The API returns the parts only.
6. **Deleting a subject frees its code but not its history.** Is that the intended behaviour for reports?
