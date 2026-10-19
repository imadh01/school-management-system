# Students controller: what each endpoint actually does

Source: `backend/SchoolManagement.API/Controllers/StudentsController.cs`, route prefix `api/students`.
Written for a developer with about two years of experience who is learning this codebase.

## How this file was made

- **Everything here was read from the code**: the controller, `StudentService`, `StudentGuardianService`, `StudentPhotoService`, `StudentEnrollmentRules`, `ClassSectionRules`, `StudentGuardianRules`, the validators, `StudentRepository`, `StudentGuardianRepository`, `DataScopeQueries`, the entity configurations, `GlobalExceptionMiddleware`, and the React `studentService.ts` and pages that call it.
- **I did not run the API or the database.** There is no SQL Server in my environment. So **every example request and response below is invented**: shapes, field names, error codes and messages come from the code, but ids, names, dates and trace ids are made up.
- "Tests exist" means a test file or class covering that rule is in `backend/SchoolManagement.Tests`. I did not run them.
- **Not verified** marks anything I could not confirm from the code alone.
- The doubts and possible bugs I noticed are at the end, labelled **Should fix** or **Question**. There is no **Blocker** in this controller.

## The fourteen endpoints at a glance

| # | Method + URL | Permission needed | Called by (React) |
|---|---|---|---|
| 1 | `GET /api/students` | `Students.View` or `Students.ViewScoped` | `StudentsPage`, class detail page, `LinkChildDialog` |
| 2 | `GET /api/students/{id}` | `Students.View` or `Students.ViewScoped` | `StudentDetailPage`, `StudentEditPage` |
| 3 | `POST /api/students` | `Students.Create` | **no caller found** (students are created by confirming/enrolling an admission) |
| 4 | `PUT /api/students/{id}` | `Students.Edit` | `StudentEditPage` |
| 5 | `PUT /api/students/{id}/identity` | `Students.Edit` **and** `Students.ViewSensitive` | `StudentEditPage` |
| 6 | `GET /api/students/{id}/enrollments` | `Students.View` or `Students.ViewScoped` | `StudentDetailPage` |
| 7 | `GET /api/students/{id}/guardians` | `Students.View` or `Students.ViewScoped` | no direct caller found; guardians come inside endpoint 2 |
| 8 | `POST /api/students/{id}/guardians` | `Parents.Edit` | no caller found on the student side (the parent side uses `/api/parents/{id}/students`) |
| 9 | `DELETE /api/students/{id}/guardians/{parentId}` | `Parents.Edit` | no caller found |
| 10 | `GET /api/students/{id}/photo` | `Students.View` or `Students.ViewScoped` | `useStudentPhoto` hook |
| 11 | `PUT /api/students/{id}/photo` | `Students.Edit` | `StudentPhotoField` |
| 12 | `DELETE /api/students/{id}/photo` | `Students.Edit` | `StudentPhotoField` |
| 13 | `POST /api/students/{id}/leave` | `Students.Edit` | `StudentsPage`, `StudentDetailPage` |
| 14 | `DELETE /api/students/{id}` | `Students.Delete` | no caller found in the React code |

"No caller found" means I searched `frontend/school-management-ui/src` and found none. The endpoint still works and has an access-matrix test row.

## Background you need before the endpoints

### The same request pipeline as Auth

Every call goes through correlation id, Serilog request logging, `GlobalExceptionMiddleware`, HTTPS redirect, CORS, JWT authentication (including the security-stamp check), `MustChangePasswordMiddleware`, then authorization, then the action. The full list is in `auth.md` ("The request pipeline for every Auth call"). The class has `[Authorize]`, so an anonymous call is **401** before anything below happens.

### Two read shapes: full and scoped

Reads accept two permissions (the policy `CompositePolicies.StudentsRead` is "View OR ViewScoped").

| Caller holds | `DataScope` | Rows returned | Shape |
|---|---|---|---|
| `Students.View` (also if both) | Unrestricted | every student | full: `StudentSummaryResponse` / `StudentResponse` |
| only `Students.ViewScoped` | `RelatedTo(userId)` | only related students | reduced: `StudentScopedSummaryResponse` / `StudentScopedResponse` |

"Related" is the union of three relationships, whatever the role is called (`DataScopeQueries.VisibleTo`):

1. **Teacher:** the student's current class section is one the user teaches (class teacher or subject teacher, current academic year).
2. **Parent:** a `StudentGuardians` link from a `Parent` whose `UserId` is the caller.
3. **Student:** the `Students` row whose `UserId` is the caller.

An id that does not exist and an id that is out of scope give the **same 404** with the same body (`Student {id} does not exist.`), so the API never confirms that a hidden student exists.

### Sensitive data: who sees what

- `Students.ViewSensitive` is a separate permission. The **controller** reads it (`CanViewSensitiveAsync`, from the permission cache, because the JWT carries no permissions) and passes a `bool includeSensitive` to the service. The service never decides it.
- Without it: Aadhaar, passport and Emirates ID show only the last 4 characters (`********9012`, `SensitiveDataMasker.MaskTail`) and `identity.isMasked` is `true`; the **allergy text** is `null` (`hasAllergies` still says whether there is any).
- Opening a student's detail writes a **"Read" audit row** (`ISensitiveReadLog`) naming the sections shown (`Health`, `Identity` or `Identity (masked)`; for scoped callers `Health (allergy flag)`). List rows and write responses do not write one.

### Status values and what they mean for seats

`StudentStatuses`: `Active`, `Inactive`, `Left` (database check `CK_Students_Status`). A **seat** is an `Active` student in a section (`CountActiveStudentsAsync`), so `Inactive` and `Left` both free the seat. An `Inactive` student keeps their open class period and roll number; setting them back to `Active` takes a seat again and is checked like a class move (endpoint 4).

### Academic history (`StudentEnrollments`)

Each class stay is one row with a start date, optional end date, roll number and status (`Active`, `Left`, `Transferred`, and others from `EnrollmentStatuses`). Only `StudentEnrollmentRules` creates or closes rows. At most one `Active` period exists per student and **roll numbers are unique among Active periods in a section** (filtered unique index `UX_StudentEnrollments_Section_Roll_Active`). The columns `RollNumber` and `ClassSectionId` on `Students` are a **copy of the current placement**; a student who left keeps their last values there.

### Soft delete and audit

`Student` has `IsDeleted/DeletedAt/DeletedBy`; a global query filter hides deleted rows. `AuditableEntitySaveChangesInterceptor` stamps `CreatedAt/By`, `UpdatedAt/By`, fills `DeletedBy` for a soft delete, and writes the audit-log row. Passwords and similar columns are masked by `[AuditMasked]`; `RowVersion` is ignored.

### Concurrency token

`Students.RowVersion` is a SQL Server `rowversion`. The responses carry it as base64 in `rowVersion`; `PUT /api/students/{id}` must send it back. If the row changed meanwhile the service throws a 409 `CONCURRENCY_CONFLICT`.

---

## 1. `GET /api/students`

### 1a. Purpose and caller

The student list. Called by the Students page, the class-section detail page (filters its students) and the "link a child" dialog on the parent pages.

### 1b. Request

No body, no query string. Filtering, searching and paging are done **in the browser**: the endpoint always returns the whole visible list.

### 1c. Response

`200 OK`, an array, newest student first (`ORDER BY Id DESC`).

- Full shape (`StudentSummaryResponse`): `id, admNo, rollNumber, classSectionId, classSectionName, admissionDate, status, photoUrl, firstName, middleName, lastName, gender, dateOfBirth, mobile, category, transportRequired, nationality, curriculumTrack, house, ealCode, allergies, admissionRegNo, admissionId, hasAllergies`.
- Scoped shape (`StudentScopedSummaryResponse`): `id, admNo, rollNumber, classSectionId, classSectionName, status, photoUrl, firstName, middleName, lastName, gender, dateOfBirth, house, hasAllergies`. No mobile, category, nationality, allergy text.

`photoUrl` is **not** the stored value. It is `/api/students/{id}/photo` when a stored picture exists, otherwise `null` (see endpoint 10).

### 1d. Logic, in order

1. Controller resolves the scope: `DataScopeResolver.ResolveAsync(Students.View, Students.ViewScoped)`.
2. Unrestricted: `StudentService.GetAllAsync(includeSensitive)`. Scoped: `GetAllScopedAsync(scope)`.
3. `StudentRepository.GetSummariesAsync(scope)` runs one `SELECT` with `AsNoTracking`, the scope filter inside the same SQL, and only the columns above (it joins class section, academic year, allergies from the health row, and the admission reg number).
4. The service builds the display name `"Class 5 - A (2026-27)"` with `ClassSection.BuildDisplayName`, converts the photo key to the route, and hides allergy text unless `includeSensitive`.

### 1e. Business rules and where each is enforced

| Rule | Where |
|---|---|
| Deleted students never appear | global query filter |
| A scoped caller sees only related students | `VisibleTo` in the SQL |
| Allergy text only with `Students.ViewSensitive` | `StudentService.GetAllAsync` |
| Sensitive reads are not audited for list rows | by design (comment in `StudentService`) |

### 1f. Errors

| Status | Code | When |
|---|---|---|
| 401 | none | no or invalid token |
| 403 | `FORBIDDEN` | holds neither `Students.View` nor `Students.ViewScoped` |
| 403 | `PASSWORD_CHANGE_REQUIRED` | the user must change their password first |

### 1g. Concurrency and retries

Read-only; safe to retry.

### 1h. Why is it built this way?

The whole list in one call keeps the React page simple (sorting, search and filters are instant) and is fine for one school. **Not verified:** there is no paging, so a school with many thousands of students would send a large response; this has not been measured.

### Example (invented)

```http
GET /api/students
Authorization: Bearer eyJ...
```

```json
[
  {
    "id": 41, "admNo": "ADM-2026-0007", "rollNumber": "5A003", "classSectionId": 12,
    "classSectionName": "Class 5 - A (2026-27)", "admissionDate": "2026-04-02", "status": "Active",
    "photoUrl": "/api/students/41/photo", "firstName": "Anu", "middleName": null, "lastName": "Thomas",
    "gender": "Female", "dateOfBirth": "2016-08-14", "mobile": "+971501234567", "category": "General",
    "transportRequired": false, "nationality": "Indian", "curriculumTrack": null, "house": "Blue",
    "ealCode": null, "allergies": null, "admissionRegNo": "REG-2026-0007", "admissionId": 7,
    "hasAllergies": true
  }
]
```

(`allergies` is `null` here because the caller lacks `Students.ViewSensitive`, while `hasAllergies` is `true`.)

---

## 2. `GET /api/students/{id}`

### 2a. Purpose and caller

One student with everything: profile, addresses, health, identity, pickup persons, guardians. Opened by the detail page and the edit page.

### 2b. Request

`id` in the path (integer). No body.

### 2c. Response

`200 OK` with `StudentResponse` (full) or `StudentScopedResponse` (scoped).

Full shape groups: placement (`admNo, rollNumber, classSectionId, classSectionName, admissionDate, status, photoUrl`), name and basics, `addresses[]`, category/religion/previous school, transport, nationality and language fields, custody and media consent, house/EAL/fee concession, `admissionRegNo, admissionId`, `health{}`, `identity{}` (with `isMasked` and `emiratesIdNumber`), `pickupPersons[]`, `guardians[]` (primary contact first), `rowVersion`, `hasAllergies`.

Scoped shape: placement, names, gender, date of birth, house, `health{hasAllergies}` and `guardians[]` as contacts (name, mobile, relation, primary flag only).

### 2d. Logic, in order

1. Resolve the scope (as in endpoint 1).
2. Unrestricted: load the student with `GetDetailByIdAsync` (class section, academic year, admission, health, identity document, pickup persons, addresses with country and region, and **only the Active enrollment**; one split query). Missing or deleted: **404**.
3. Load the guardian links (`StudentGuardians` with `Parent`) and build the response; sensitive fields are filled or masked as described above.
4. Write the "Read" audit row.
5. Scoped: first `GetAccessAsync(id, scope)`. `StudentAccess.None` (missing or out of scope) is the same 404. Then the guardian list is filtered by `StudentGuardianRules.VisibleLinks`: a teacher of the student or the student themself sees all guardians; a caller who is related **only as a parent** sees **only their own link**, so a separated co-parent stays private.

### 2e. Business rules and where each is enforced

| Rule | Where |
|---|---|
| Missing and out-of-scope ids look identical | `GetAccessOrThrowAsync` |
| A parent never sees the other guardian | `StudentGuardianRules.VisibleLinks` |
| Identity and allergy text need `Students.ViewSensitive` | `ToResponseAsync` |
| Every detail read is audited | `ISensitiveReadLog.RecordAsync` |

### 2f. Errors

| Status | Code | When |
|---|---|---|
| 404 | `NOT_FOUND` | no such student, deleted, or out of the caller's scope |
| 403 | `FORBIDDEN` | no read permission |

### 2g. Concurrency and retries

Read-only. The `rowVersion` returned is what the edit form sends back.

### 2h. Why is it built this way?

One request gives the edit page everything it needs. The two shapes exist so a teacher or parent can be given limited access with a permission, without separate endpoints per role (decision recorded as batch F1).

### Example (invented)

```json
{
  "id": 41, "admNo": "ADM-2026-0007", "rollNumber": "5A003", "status": "Active",
  "classSectionId": 12, "classSectionName": "Class 5 - A (2026-27)",
  "firstName": "Anu", "lastName": "Thomas", "photoUrl": "/api/students/41/photo",
  "health": { "bloodGroup": "B+", "allergies": null, "medicalNotes": null },
  "identity": { "aadhaarNumber": "********9012", "passportNumber": null, "isMasked": true,
                "emiratesIdNumber": "***********4567" },
  "guardians": [ { "parentId": 9, "name": "George Thomas", "mobile": "+971509876543",
                   "relationType": "Father", "isPrimaryContact": true } ],
  "rowVersion": "AAAAAAAAB9E=", "hasAllergies": true
}
```

---

## 3. `POST /api/students`

### 3a. Purpose and caller

Creates a student directly, without an admission. **No React page calls it**; the normal route is the admission being confirmed and enrolled (see `admissions.md`). Kept for API completeness and tests.

### 3b. Request

JSON `CreateStudentRequest`. Required: `admNo` (max 30), `rollNumber` (max 20), `classSectionId`, `admissionDate`, `firstName`, `lastName` (max 50), `gender`, `dateOfBirth` (before today), `category` (max `FieldLimits.Category`), `admissionType` (`New` or `Transfer`). Optional: the rest (names, mobile, email, religion, nationality and language fields, custody, `mediaConsent`, house, EAL code, `feeConcessionPercent` 0 to 100, `health{}`, `pickupPersons[]` max 10, `addresses[]`). **The admission number and roll number are NOT generated here**; the caller supplies them.

Guardians are linked afterwards (endpoint 8) and identity numbers are set afterwards (endpoint 5).

### 3c. Response

`201 Created`, `Location: api/students/{id}`, body `StudentResponse` (full shape).

### 3d. Logic, in order

1. Controller: `CreateStudentRequestValidator`; failure returns **400** `VALIDATION_ERROR` with a field map.
2. `ExistsByAdmNoAsync`: duplicate admission number gives **409**.
3. `ExistsByRollNumberInClassAsync`: duplicate roll number among Active enrolments of that class gives **409**.
4. Load the class section (404 if it does not exist). Build the addresses (`AddressService.BuildAsync`, which validates country and region).
5. `ClassSectionRules.EnsureActive`: an inactive section gives **409**.
6. **Transaction:** take the app lock for that class (`sp_getapplock`, `ClassSectionSeats:{id}`), count Active students, `EnsureHasRoom` (a full section gives **409**; no capacity set means no limit), then create the student with status `Active`, an empty health row, the **first enrolment period** (starting on `admissionDate`), the health block, pickup persons and addresses, in **one `SaveChanges`**.
7. Return the full response.

### 3e. Business rules and where each is enforced

| Rule | Where |
|---|---|
| Admission number unique | pre-check in service, final authority: unique index on `Students.AdmNo` |
| Roll number unique among active students of a class | pre-check, final authority: `UX_StudentEnrollments_Section_Roll_Active` |
| Section must be Active and have room | `ClassSectionRules` inside the locked transaction |
| Two people cannot take the last seat | `sp_getapplock` on the class (test: `EnrolCapacityRaceTests`) |
| Every student has a first history period | service adds `StudentEnrollmentRules.StartNew` |
| Max 10 pickup persons | validator |

### 3f. Errors

| Status | Code | When |
|---|---|---|
| 400 | `VALIDATION_ERROR` | any validator rule |
| 404 | `NOT_FOUND` | class section does not exist |
| 409 | `CONFLICT` | duplicate AdmNo, duplicate roll number, inactive section, full section, or a unique-index race (message: "conflicts with a record just saved by someone else") |

### 3g. Concurrency and retries

The seat check is safe against two simultaneous creates. A retry after a timeout may return 409 for the admission number if the first call actually succeeded.

### 3h. Why is it built this way?

The friendly pre-checks give clear messages; the unique indexes are the real guard, and `SaveAsync` turns SQL errors 2601/2627 into the same 409.

### Example (invented)

```json
{ "admNo": "ADM-2026-0099", "rollNumber": "5A031", "classSectionId": 12, "admissionDate": "2026-10-19",
  "firstName": "Ravi", "lastName": "Menon", "gender": "Male", "dateOfBirth": "2016-02-03",
  "category": "General", "admissionType": "New", "transportRequired": false, "mediaConsent": true }
```

If section 12 already has 30 of 30 seats taken:

```json
{ "success": false, "message": "'Class 5 - A (2026-27)' is full (30/30).",
  "errorCode": "CONFLICT", "traceId": "0HN7K2..." }
```

---

## 4. `PUT /api/students/{id}`

### 4a. Purpose and caller

Saves the edit page: profile, placement (class, roll number, status), health, pickup persons, addresses. Called by `StudentEditPage`.

### 4b. Request

`UpdateStudentRequest`: `rowVersion` (required, valid base64), `rollNumber`, `classSectionId`, `status` (`Active`, `Inactive` or `Left`), the same profile fields as create, and optional `health`, `pickupPersons`, `addresses`.

**`null` means "leave as stored"; a supplied block replaces the stored one; an empty list clears it.** The admission number, admission date and identity numbers cannot be changed here.

### 4c. Response

`200 OK` with the full `StudentResponse` (new `rowVersion` inside).

### 4d. Logic, in order

1. Validator (400).
2. Load the student with details (404). `_concurrency.Expect(student, rowVersion)` (409 if it changed).
3. Load the target class section (404).
4. Work out what the edit means: `classChanged`, `rollChanged`, `becomesLeft` (status `Left`), and `needsFreshPeriod` (no open period, e.g. a returning student).
5. Refuse two combinations (**422**): moving class **and** marking Left in one edit; moving to a class in a **different academic year** (that is year-end promotion, not built here).
6. If the edit takes a seat (a class change, a returning student, or an `Inactive` student being set back to `Active`; never when marking Left) the work runs in a **transaction behind the class seat lock**. If addresses are replaced it also runs in a transaction.
7. Inside: for a class change, a returning student or a reactivated student `EnsureActive` and `EnsureHasRoom` (409; the count excludes the student, who is not `Active` yet); for a changed class, changed roll or returning student, check the roll number is free among Active enrolments of the target class (409). A reactivation with the same roll skips the roll check, because the student's own open period still holds it.
8. History: **Left** closes the open period with outcome `Left` (end date today); **returning** adds a new Active period starting today; **class change** closes the old period as `Transferred` ("Moved to ...") and starts a new one today (two saves, because only one Active period may exist); **roll-only change** just edits the current period.
9. Copy the placement onto `Students` (not when Left: a student who left keeps their last class and roll), set the status and the profile fields, then health, pickup persons and addresses as described.
10. **Allergy protection:** if the caller lacks `Students.ViewSensitive` the allergy text they sent is ignored and the stored text is kept (they never saw it, so what they send back is meaningless).
11. Save, return the full response.

### 4e. Business rules and where each is enforced

| Rule | Where |
|---|---|
| Edits are rejected if someone else saved first | `IConcurrencyGuard` + `rowversion` |
| A student moves class OR leaves, not both in one edit | service (`BusinessRuleException`) |
| Class moves stay inside the academic year | service |
| Target class must be Active, have room, and have the roll number free | `ClassSectionRules` + roll check |
| Setting an Inactive student back to Active takes a seat, so it is checked like a move (fixed 2026-10-19) | service (`reactivates`); test `StudentReactivationSeatTests` |
| Editing a student whose own class is full or inactive still works | checks run only on a move, a return or a reactivation |
| Allergy text is not wiped by a caller who cannot see it | service |
| One Active period per student | unique filtered index; hence the two-save step |

### 4f. Errors

| Status | Code | When |
|---|---|---|
| 400 | `VALIDATION_ERROR` | validator, including bad `rowVersion` and invalid status |
| 404 | `NOT_FOUND` | student or class section missing |
| 409 | `CONCURRENCY_CONFLICT` | `rowVersion` is stale |
| 409 | `CONFLICT` | inactive or full target section (this includes setting an Inactive student back to Active in a full or inactive class), roll number taken |
| 422 | `BUSINESS_RULE_VIOLATION` | class move plus Left; move to another academic year |

### 4g. Concurrency and retries

Protected by `rowVersion`, so a double click on Save gives the second call a 409 (its `rowVersion` is now old). The seat lock is held only for the transaction.

### 4h. Why is it built this way?

History is written by the same call that changes the class, in one transaction, so the placement copy on `Students` and the periods never disagree. The year-end move is deliberately not allowed here because a promotion also affects fees, results and rolls.

### Example (invented)

```json
{ "rowVersion": "AAAAAAAAB9E=", "rollNumber": "5B012", "classSectionId": 13, "status": "Active",
  "firstName": "Anu", "lastName": "Thomas", "gender": "Female", "dateOfBirth": "2016-08-14",
  "category": "General", "admissionType": "New", "transportRequired": false, "mediaConsent": true }
```

Moving to another year's class:

```json
{ "success": false, "errorCode": "BUSINESS_RULE_VIOLATION",
  "message": "A student can only be moved to a class in the same academic year here. Moving to the next year is done through year-end promotion.",
  "traceId": "0HN7K3..." }
```

---

## 5. `PUT /api/students/{id}/identity`

### 5a. Purpose and caller

Sets Aadhaar, Emirates ID, passport and visa details. Called by the edit page after the main save, only when the caller may see sensitive data.

### 5b. Request

`UpdateStudentIdentityRequest`: `rowVersion` (**required**, the student's version as returned by endpoint 2 or 4), `aadhaarNumber` (12 digits, spaces allowed), `emiratesIdNumber` (15 digits starting `784`, dashes and spaces allowed), `passportNumber` (max 30), `passportExpiry`, `visaType` (max 30), `visaExpiry`. The six identity values are optional; **each call replaces all six**, so a field left out is cleared.

### 5c. Response

`200 OK`, the full `StudentResponse` with `includeSensitive = true`. Its `rowVersion` is the **new** version: a successful identity save moves the student's version on, so the next save (identity or main form) must use it.

### 5d. Logic, in order

1. Two authorization policies must both pass: `Students.Edit` and `Students.ViewSensitive` (403 if either is missing).
2. Validator (400): `rowVersion` present and valid, Aadhaar and Emirates ID shape (`EmiratesIdRules`), lengths.
3. Load the student (404). `_concurrency.Expect(student, rowVersion)`: this sets the version the UPDATE must match and puts the student row in the UPDATE, so a stale version gives **409** `CONCURRENCY_CONFLICT` and nothing is written.
4. Strip spaces from Aadhaar; if another student has the same Aadhaar: **409**. Normalise the Emirates ID to 15 digits; same check: **409**.
5. Create the identity document row if there is none, set the six values, save (identity row and student row together), return the response with the new `rowVersion`.

### 5e. Business rules and where each is enforced

| Rule | Where |
|---|---|
| Aadhaar and Emirates ID unique across students | pre-check + unique indexes on `StudentIdentityDocuments` |
| Emirates ID is 15 digits, starts 784 | `EmiratesIdRules` (validator) |
| Only people allowed to see these numbers may set them | two policies on the action |
| Two people editing identity numbers cannot overwrite each other (fixed 2026-10-19) | `rowVersion` + `IConcurrencyGuard`; test `StudentIdentityConcurrencyTests` |

### 5f. Errors

| Status | Code | When |
|---|---|---|
| 400 | `VALIDATION_ERROR` | `rowVersion` missing or malformed, wrong Aadhaar or Emirates ID format, too long |
| 403 | `FORBIDDEN` | missing `Students.Edit` or `Students.ViewSensitive` |
| 404 | `NOT_FOUND` | student missing |
| 409 | `CONCURRENCY_CONFLICT` | the student was saved after this form was loaded (stale `rowVersion`) |
| 409 | `CONFLICT` | number already recorded for another student |

### 5g. Concurrency and retries

Protected by `rowVersion`, the same way as endpoint 4. The student row and the identity row are saved together, so after any identity save the old version is stale. Repeating the same request with the **old** version gives 409; use the version from the last response. The React edit page does this: it sends the version returned by the main save, and shows the version returned by the identity save.

### 5h. Why is it built this way?

Identity numbers are on their own endpoint and table so the main edit cannot overwrite them and so a different permission can guard them. The Emirates ID field is shown in the UI only when the school's country is the UAE (a frontend rule, not a backend one: the API accepts it for every school).

### Example (invented)

```json
{ "rowVersion": "AAAAAAAAB9E=", "aadhaarNumber": "1234 5678 9012", "emiratesIdNumber": "784-2016-1234567-1",
  "passportNumber": "P1234567", "passportExpiry": "2030-05-01" }
```

If someone saved the student after this form was opened:

```json
{ "success": false, "errorCode": "CONCURRENCY_CONFLICT",
  "message": "This record was changed by someone else after you opened it. Reload it and make your changes again.",
  "traceId": "0HN7K6..." }
```

---

## 6. `GET /api/students/{id}/enrollments`

### 6a. Purpose and caller

The student's class history, newest first. Shown on the detail page.

### 6b. Request

`id` in the path.

### 6c. Response

`200 OK`, array of `StudentEnrollmentResponse`: `id, academicYearId, academicYearName, classSectionId, classSectionName, rollNumber, startDate, endDate, status, remarks`.

### 6d. Logic, in order

1. Resolve scope; `GetAccessAsync`: missing or out of scope is **404**.
2. `GetEnrollmentsAsync`: no tracking, `ORDER BY StartDate DESC, Id DESC`.

### 6e. Business rules and where each is enforced

Same scope rule as endpoint 2. Read-only, nothing here changes history; history changes happen in endpoints 3, 4, 13.

### 6f. Errors

404 `NOT_FOUND`, 403 `FORBIDDEN`.

### 6g. Concurrency and retries

Read-only; safe to retry.

### 6h. Why is it built this way?

History is a separate list, not part of endpoint 2, because most screens do not need it.

### Example (invented)

```json
[
  { "id": 88, "academicYearName": "2026-27", "classSectionName": "Class 5 - B (2026-27)",
    "rollNumber": "5B012", "startDate": "2026-09-01", "endDate": null, "status": "Active", "remarks": null },
  { "id": 61, "academicYearName": "2026-27", "classSectionName": "Class 5 - A (2026-27)",
    "rollNumber": "5A003", "startDate": "2026-04-02", "endDate": "2026-09-01", "status": "Transferred",
    "remarks": "Moved to Class 5 - B (2026-27)" }
]
```

---

## 7. `GET /api/students/{id}/guardians`

### 7a. Purpose and caller

The student's parents/guardians. No React caller found; the same list already comes inside endpoint 2.

### 7b. Request

`id` in the path.

### 7c. Response

`200 OK`. Full callers: `StudentGuardianResponse[]` (`parentId, name, mobile, email, relationType, isPrimaryContact`). Scoped callers: `GuardianContactResponse[]` (no email).

### 7d. Logic, in order

1. Resolve scope. Unrestricted: `GetStudentAccessOrThrowAsync` (unknown student is **404**; it used to return an empty list) then load the links.
2. Scoped: access check, then `StudentGuardianRules.VisibleLinks` (a parent sees only their own link).
3. Order: primary contact first, then by parent name.

### 7e. Business rules and where each is enforced

As endpoint 2 (co-guardian privacy in `StudentGuardianRules`).

### 7f. Errors

404 `NOT_FOUND`, 403 `FORBIDDEN`.

### 7g. Concurrency and retries

Read-only.

### 7h. Why is it built this way?

A separate read for clients that only need contacts. **Not verified:** whether any other screen will use it.

### Example (invented)

```json
[ { "parentId": 9, "name": "George Thomas", "mobile": "+971509876543",
    "email": "george@example.com", "relationType": "Father", "isPrimaryContact": true } ]
```

---

## 8. `POST /api/students/{id}/guardians`

### 8a. Purpose and caller

Links an existing parent to a student. Needs `Parents.Edit` (not `Students.Edit`). No caller found on the student side.

### 8b. Request

`LinkGuardianRequest`: `parentId` (greater than 0), `relationType` (`Father`, `Mother` or `Guardian`), `isPrimaryContact` (bool).

### 8c. Response

`201 Created`, `Location: api/students/{id}/guardians/{parentId}`, body `StudentGuardianResponse`.

### 8d. Logic, in order

1. Validator (400).
2. Load the parent (404 "Parent X does not exist.").
3. Check the student exists (404 "Student X does not exist.").
4. `LinkExistsAsync`: already linked gives **409**.
5. If `isPrimaryContact` is true and the student already has a primary contact: **409** naming the current primary ("Student X already has a primary contact (Name). Link this guardian without the primary flag, then use "Set as primary" to switch.").
6. Insert the `StudentGuardians` row and save. If someone else saved a conflicting row between step 5 and now, the unique index fails and the repository turns it into a **409** too ("conflicts with one just saved by someone else").

### 8e. Business rules and where each is enforced

| Rule | Where |
|---|---|
| A parent is linked to a student once | primary key `(StudentId, ParentId)` + service pre-check |
| Relation is Father, Mother or Guardian | validator + check `CK_StudentGuardians_RelationType` |
| At most one primary contact per student | service pre-check (409 with a message) + filtered unique index `UX_StudentGuardians_Student_Primary` as the final guard (fixed 2026-10-19; test `GuardianLinkRulesTests`) |

### 8f. Errors

| Status | Code | When |
|---|---|---|
| 400 | `VALIDATION_ERROR` | bad relation type or parent id |
| 404 | `NOT_FOUND` | parent or student missing |
| 409 | `CONFLICT` | already linked; the student already has a primary contact; a conflicting link saved by someone else at the same moment |

### 8g. Concurrency and retries

Two identical calls: one gets 201, the other 409. Two different parents linked as primary at the same moment: one gets 201, the other 409 (never a 500).

### 8h. Why is it built this way?

Guardianship is a many-to-many link with its own columns, so it has its own endpoints; it is gated by `Parents.Edit` because it changes a parent's data.

### Example (invented)

```json
{ "parentId": 9, "relationType": "Father", "isPrimaryContact": true }
```

---

## 9. `DELETE /api/students/{id}/guardians/{parentId}`

### 9a. Purpose and caller

Removes the link between a student and a parent (the parent record stays). Needs `Parents.Edit`. No caller found.

### 9b. Request

Two ids in the path. No body.

### 9c. Response

`204 No Content`.

### 9d. Logic, in order

1. `GetLinkAsync`; none gives **404** ("No guardian link exists between student X and parent Y.").
2. Remove the row, save.

### 9e. Business rules and where each is enforced

No rule stops removing the **last** guardian or the **primary** contact; the student is simply left without one. **BUSINESS DECISION REQUIRED:** should a student always keep at least one guardian?

### 9f. Errors

404 `NOT_FOUND`, 403 `FORBIDDEN`.

### 9g. Concurrency and retries

A second identical call returns 404.

### 9h. Why is it built this way?

It is a plain hard delete of a link row; there is nothing else attached to it.

### Example (invented)

`DELETE /api/students/41/guardians/9` returns `204` with an empty body.

---

## 10. `GET /api/students/{id}/photo`

### 10a. Purpose and caller

Streams the student's picture. The browser cannot put the bearer token in an `<img src>`, so React fetches it with the API client as a blob and shows an object URL (`useStudentPhoto`).

### 10b. Request

`id` in the path.

### 10c. Response

`200 OK`, the image bytes with `Content-Type` `image/jpeg` or `image/png`, `X-Content-Type-Options: nosniff`, `Cache-Control: private, no-cache`.

### 10d. Logic, in order

1. Resolve scope; `GetAccessAsync`: missing or out of scope is **404**.
2. Load the student; if `PhotoUrl` is empty or still holds an old web address (`://`), **404** "This student has no photo."
3. Open the file from `IFileStorage` (404 if the file is gone), copy it to memory.
4. The content type comes from the stored key's extension (`.png` or JPEG).

### 10e. Business rules and where each is enforced

| Rule | Where |
|---|---|
| Same permission and data scope as the student's record | policy + `GetAccessAsync` |
| Files live outside the web root under random keys | `IFileStorage` |
| Browser must not guess the type | `nosniff` header |

### 10f. Errors

404 `NOT_FOUND` (no student, out of scope, no photo, file missing), 403 `FORBIDDEN`.

### 10g. Concurrency and retries

Read-only. `no-cache` means the browser asks again each time.

### 10h. Why is it built this way?

Going through the API (not a static folder) means a parent cannot fetch another child's picture by guessing a URL. **Not verified:** reading does not write a sensitive-read audit row.

### Example (invented)

`GET /api/students/41/photo` returns the picture bytes, `Content-Type: image/jpeg`.

---

## 11. `PUT /api/students/{id}/photo`

### 11a. Purpose and caller

Uploads or replaces the photo. Called by `StudentPhotoField` on the edit page.

### 11b. Request

`multipart/form-data` with one part named `file`. Limited by `[DocumentUploadLimit]` at the HTTP level (a request over the server limit gives 413 `PAYLOAD_TOO_LARGE`).

### 11c. Response

`204 No Content`. Afterwards the student's `photoUrl` is the route from endpoint 10.

### 11d. Logic, in order

1. No file part: **400** `VALIDATION_ERROR` with `errors.file`.
2. Load the student (404).
3. Empty file or over **2 MB**: **400** (`errors.File`).
4. Read the first bytes and detect the type from the **content** (`FileSignatures.Detect`), not the file name; only JPG and PNG pass, otherwise **400**.
5. Save the new file under a random key. Remember the old key. Set `PhotoUrl` to the new key and save; if saving fails, delete the new file so no orphan remains.
6. Delete the old file (errors here are swallowed: a leftover file is harmless).

### 11e. Business rules and where each is enforced

| Rule | Where |
|---|---|
| JPG or PNG only, checked by content | `FileSignatures` |
| Max 2 MB | `StudentPhotoService.MaxBytes` |
| The stored value is an opaque key, never a user-chosen path | `IFileStorage.SaveAsync` |

### 11f. Errors

400 `VALIDATION_ERROR` (no file, empty, too big, wrong type), 404 `NOT_FOUND`, 413 `PAYLOAD_TOO_LARGE`, 403 `FORBIDDEN`.

### 11g. Concurrency and retries

No `rowVersion`. Two uploads at once: the last save wins and the loser's file is deleted as the "old" one or stays as an orphan (rare; **not verified**).

### 11h. Why is it built this way?

It reuses the existing `PhotoUrl` column as a storage key, so this needed no migration. The old free-text "Photo URL" request field was removed (breaking change for API clients).

### Example (invented)

```http
PUT /api/students/41/photo
Content-Type: multipart/form-data; boundary=----x

------x
Content-Disposition: form-data; name="file"; filename="anu.jpg"
Content-Type: image/jpeg

<bytes>
------x--
```

A 3 MB upload returns `400` with `"errors": { "File": ["The photo is 3.0 MB. The largest photo allowed is 2.0 MB."] }`.

---

## 12. `DELETE /api/students/{id}/photo`

### 12a. Purpose and caller

Removes the photo. Called by `StudentPhotoField`.

### 12b. Request

`id` in the path.

### 12c. Response

`204 No Content`, also when there was no photo.

### 12d. Logic, in order

1. Load the student (404).
2. No stored key: return.
3. Set `PhotoUrl = null`, save, then delete the file (errors swallowed).

### 12e. Business rules and where each is enforced

None beyond `Students.Edit`.

### 12f. Errors

404 `NOT_FOUND`, 403 `FORBIDDEN`.

### 12g. Concurrency and retries

Idempotent: repeating gives 204 again.

### 12h. Why is it built this way?

The database is changed first and the file deleted after, so a failure never leaves a student pointing at a missing file.

### Example (invented)

`DELETE /api/students/41/photo` returns `204`.

---

## 13. `POST /api/students/{id}/leave`

### 13a. Purpose and caller

One-click "mark as Left" from the list row menu and the detail page. It is the shortcut for setting Status to Left on the edit page.

### 13b. Request

`id` in the path. No body.

### 13c. Response

`204 No Content`.

### 13d. Logic, in order

1. Load the student with details (404).
2. Already `Left`: **422** "This student has already left."
3. Close the open enrolment period with outcome `Left`, end date today.
4. Set status `Left`, save.

### 13e. Business rules and where each is enforced

| Rule | Where |
|---|---|
| Leaving ends the class period and frees the seat | `StudentEnrollmentRules.Close`, seats count Active only |
| The record, history and attendance are kept | nothing is deleted |
| Leaving twice is refused | service |

### 13f. Errors

404 `NOT_FOUND`, 422 `BUSINESS_RULE_VIOLATION`, 403 `FORBIDDEN`.

### 13g. Concurrency and retries

No `rowVersion`. A second call gets 422.

### 13h. Why is it built this way?

Students with history are never deleted (endpoint 14); they leave. "Today" is **UTC**, not the school's time zone (a `TODO(school time zone)` in `StudentEnrollmentRules`), so near midnight in Abu Dhabi the end date can be the previous day.

### Example (invented)

`POST /api/students/41/leave` returns `204`; a second call returns:

```json
{ "success": false, "message": "This student has already left.",
  "errorCode": "BUSINESS_RULE_VIOLATION", "traceId": "0HN7K4..." }
```

---

## 14. `DELETE /api/students/{id}`

### 14a. Purpose and caller

Soft-deletes a student created by mistake. Needs `Students.Delete`. No React caller found.

### 14b. Request

`id` in the path.

### 14c. Response

`204 No Content`.

### 14d. Logic, in order

1. Load the student (404).
2. `HasAcademicHistoryAsync`: any enrolment row **or** any attendance record gives **409** "...cannot be deleted. Set the status to 'Left' instead."
3. Set `IsDeleted = true` and `DeletedAt`; the interceptor fills `DeletedBy` and the audit row.

### 14e. Business rules and where each is enforced

| Rule | Where |
|---|---|
| Only history-free students can be deleted | service (test: `StudentDeleteRulesTests`) |
| Deleted rows are hidden everywhere | global query filter |
| A deleted student's AdmNo stays reserved | unique index covers deleted rows |

### 14f. Errors

404 `NOT_FOUND`, 409 `CONFLICT`, 403 `FORBIDDEN`.

### 14g. Concurrency and retries

A second call returns 404 (the row is now filtered out).

### 14h. Why is it built this way?

A student who was enrolled always has at least one enrolment row, so in practice this works only for rows with no periods (for example legacy or test data). Normal departures use endpoint 13. **BUSINESS DECISION REQUIRED:** whether this endpoint should exist at all for real data.

### Example (invented)

```json
{ "success": false, "errorCode": "CONFLICT",
  "message": "This student has academic history (class enrolments or attendance) and cannot be deleted. Set the status to 'Left' instead.",
  "traceId": "0HN7K5..." }
```

---

## Doubts and possible problems

### Fixed on 2026-10-19 (these were "Should fix" in the first version of this file)

1. **Guardian link could return 500.** `POST /api/students/{id}/guardians` (and the parent-side link, see `parents.md`) now answers 404 for an unknown student or parent and 409 for a second primary contact or a link saved by someone else at the same moment. Tests: `GuardianLinkRulesTests`.
2. **Reactivating an `Inactive` student skipped the seat check.** Setting an Inactive student back to Active in the same class now runs the same "class is Active and has room" check as a class move (endpoint 4). Tests: `StudentReactivationSeatTests`.
3. **`PUT .../identity` had no `rowVersion` check.** It now requires and checks `rowVersion` like endpoint 4 (endpoint 5). This adds a required field to the request, so API clients must send it; the React edit page already does. Tests: `StudentIdentityConcurrencyTests`.

### Should fix

None open in this controller at the moment.

### Question

1. Should deleting the **last guardian** or the **primary contact** be blocked (endpoint 9)?
2. Should `GET /api/students/{id}/photo` write a sensitive-read audit row, like the detail read does?
3. Is `DELETE /api/students/{id}` still wanted? In practice it only succeeds for students with no enrolment rows.
4. The list endpoint returns every student with no paging. Fine now; should it page or filter on the server before the school grows?
5. `POST /api/students` takes the admission number and roll number from the caller, while admissions generate them (`ADM-2026-0001`, `5A001`). Should direct creation also generate them, or should the endpoint be removed since no screen uses it?
6. The "today" used for enrolment dates is UTC, not the school time zone (`SchoolTimeZones` exists). Should `StudentEnrollmentRules.Today()` use it?

## Restore (deleted students)

- `GET /api/students/deleted` and `POST /api/students/{id}/restore`: need `Students.Delete` **and** the Admin role (checked in `StudentService`; anyone else gets 403).
- 404 unknown id. 409 if the student is not deleted, its login account has been deleted or is now linked to another live student (`UX_Students_UserId` is filtered by `IsDeleted`), or, for an Active student, the class is inactive or full (same seat lock and count as every other way of taking a seat).
- `AdmNo`, Emirates ID and Aadhaar stay reserved while a student is deleted (their unique indexes ignore `IsDeleted`), so they cannot clash on restore.
- Today a student can only be deleted when it has no class period and no attendance. Every student created through the app gets a class period, so in practice the Delete endpoint answers 409 and the deleted list stays empty. There is no screen for deleted students for that reason.
