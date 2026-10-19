# Parents controller: what each endpoint actually does

Source: `backend/SchoolManagement.API/Controllers/ParentsController.cs`, route prefix `api/parents`.
Written for a developer with about two years of experience who is learning this codebase.

## How this file was made

- **Everything here was read from the code**: the controller, `ParentService`, `StudentGuardianService`, `StudentGuardianRules`, the validators, `ParentRepository`, `StudentGuardianRepository`, `DataScopeQueries`, the entity configurations, and the React `parentService.ts` and pages.
- **I did not run the API or the database** (no SQL Server in my environment). So **every example request and response below is invented**: shapes, field names, error codes and messages come from the code, the values do not.
- "Tests exist" means a test file covering that rule is in `backend/SchoolManagement.Tests`. I did not run them.
- **Not verified** marks anything I could not confirm from the code alone.
- Doubts and possible bugs are at the end, labelled **Should fix** or **Question**. No **Blocker** in this controller.

## The nine endpoints at a glance

| # | Method + URL | Permission needed | Called by (React) |
|---|---|---|---|
| 1 | `GET /api/parents` | `Parents.View` or `Parents.ViewScoped` | `ParentsPage` |
| 2 | `GET /api/parents/{id}` | `Parents.View` or `Parents.ViewScoped` | `ParentDetailPage`, `ParentFormPage` |
| 3 | `POST /api/parents` | `Parents.Create` | `ParentFormPage` |
| 4 | `PUT /api/parents/{id}` | `Parents.Edit` | `ParentFormPage` |
| 5 | `DELETE /api/parents/{id}` | `Parents.Delete` | `ParentsPage`, `ParentDetailPage` |
| 6 | `GET /api/parents/{id}/students` | `Parents.View` or `Parents.ViewScoped` | `ParentDetailPage` |
| 7 | `POST /api/parents/{id}/students` | `Parents.Edit` | `LinkChildDialog` |
| 8 | `DELETE /api/parents/{id}/students/{studentId}` | `Parents.Edit` | `ParentDetailPage` |
| 9 | `POST /api/parents/{id}/students/{studentId}/primary` | `Parents.Edit` | `ParentDetailPage` |

The same parent-student link can also be managed from the student side (`/api/students/{id}/guardians`, see `students.md` endpoints 7 to 9). The React `parentService.ts` defines those three calls but no page uses them; the UI manages links only from the parent page.

## Background you need before the endpoints

### The same request pipeline as Auth

Correlation id, Serilog request logging, `GlobalExceptionMiddleware`, HTTPS redirect, CORS, JWT authentication, `MustChangePasswordMiddleware`, authorization, then the action (details in `auth.md`). `[Authorize]` on the class means anonymous calls get **401**.

### Two read shapes: full and scoped

Reads accept `Parents.View` (everything, full shape `ParentResponse`) or `Parents.ViewScoped` (reduced shape `ParentContactResponse`: `id`, `name`, `mobile` only). If a caller holds both, View wins. The scope is resolved on every request by `DataScopeResolver`, never cached.

A scoped caller sees a parent when (`DataScopeQueries.VisibleTo` for parents):

1. it is the caller's **own** parent record (`Parent.UserId` equals the caller), or
2. the parent is a guardian of a student the caller **teaches** (class or subject teacher of the student's current class, current academic year), or of the caller themself when the caller is a student.

Deliberately **not** included: the other guardians of the caller's own children. A parent sees only themself. A missing id and an out-of-scope id give the **same 404** body (`Parent {id} does not exist.`).

### What a Parent is

A person with a name, a mobile (required), optional email, occupation, nationality, country of residence, time zone, language and contact preferences, WhatsApp number, work details, a `billingContact` flag, five notification switches (attendance, exams, fees, notices, discipline), a status (`Active` or `Inactive`, check `CK_Parents_Status`) and addresses. A parent may have a login account (`UserId`, one login per parent, filtered unique index `UX_Parents_UserId`); the login is created elsewhere (Users and Roles), not by this controller.

`MobileKey` is a stored computed column (a cleaned form of the mobile) with a **non-unique** index: families may share a number, and matching a new application to an existing parent is confirmed by staff (see `admissions.md`).

### The link between a parent and a student

Table `StudentGuardians`, primary key `(StudentId, ParentId)`, with `RelationType` (`Father`, `Mother`, `Guardian`, check constraint) and `IsPrimaryContact`. A filtered unique index allows **at most one primary contact per student**. The link table has a query filter that hides links whose student or parent is soft-deleted.

### Soft delete, audit, concurrency

`Parent` has `IsDeleted/DeletedAt/DeletedBy` and a global query filter. The audit interceptor stamps created/updated/deleted by and writes the audit-log row. `Parent.RowVersion` (SQL `rowversion`) is returned as base64 `rowVersion` and must be sent back on update.

---

## 1. `GET /api/parents`

### 1a. Purpose and caller

The parents list page.

### 1b. Request

No body, no query string. Searching and filtering happen in the browser.

### 1c. Response

`200 OK`, array, newest first (`ORDER BY Id DESC`). Full callers get `ParentResponse` (all fields above, `addresses[]`, `rowVersion`). Scoped callers get `ParentContactResponse` (`id, name, mobile`).

### 1d. Logic, in order

1. Resolve the scope for `Parents.View` / `Parents.ViewScoped`.
2. `ParentRepository.GetAllAsync(scope)`: no tracking, scope filter in the SQL, addresses included with country and region (split query).
3. Map to the full or the contact shape.

### 1e. Business rules and where each is enforced

| Rule | Where |
|---|---|
| Deleted parents never appear | global query filter |
| Scoped callers see only themselves and parents of students they teach | `VisibleTo` |
| Scoped callers get name and mobile only | `ParentService.GetAllScopedAsync` |

### 1f. Errors

| Status | Code | When |
|---|---|---|
| 401 | none | no or invalid token |
| 403 | `FORBIDDEN` | holds neither read permission |
| 403 | `PASSWORD_CHANGE_REQUIRED` | the user must change their password |

### 1g. Concurrency and retries

Read-only; safe to retry.

### 1h. Why is it built this way?

One shape per permission, chosen by the controller, keeps the service simple and makes it impossible for a scoped caller to receive an email or address by accident. There is no paging; **not verified** how the page behaves with thousands of parents.

### Example (invented)

```json
[ { "id": 9, "name": "George Thomas", "email": "george@example.com", "mobile": "+971509876543",
    "status": "Active", "billingContact": true, "notifyFees": true, "addresses": [],
    "rowVersion": "AAAAAAAAB7c=" } ]
```

---

## 2. `GET /api/parents/{id}`

### 2a. Purpose and caller

One parent. Used by the detail page and to fill the edit form.

### 2b. Request

`id` in the path.

### 2c. Response

`200 OK`, `ParentResponse` (full) or `ParentContactResponse` (scoped).

### 2d. Logic, in order

1. Resolve scope.
2. Unrestricted: `GetByIdAsync` (with addresses, tracked). Missing or deleted: **404**.
3. Scoped: `GetVisibleByIdAsync(id, scope)`; none gives the same **404**.

### 2e. Business rules and where each is enforced

Same scope rule as endpoint 1. Unlike student detail reads, **no sensitive-read audit row** is written (parents have no masked fields).

### 2f. Errors

404 `NOT_FOUND`, 403 `FORBIDDEN`.

### 2g. Concurrency and retries

Read-only. Keep the `rowVersion` for the update.

### 2h. Why is it built this way?

Missing and hidden look identical so a teacher cannot probe for parents outside their classes.

### Example (invented)

`GET /api/parents/9` as a teacher of the child returns `{ "id": 9, "name": "George Thomas", "mobile": "+971509876543" }`. A teacher of a different class gets:

```json
{ "success": false, "message": "Parent 9 does not exist.", "errorCode": "NOT_FOUND", "traceId": "0HN7M1..." }
```

---

## 3. `POST /api/parents`

### 3a. Purpose and caller

Creates a parent. Called by the parent form page (the "Add parent" screen). Parents are also created from the admission flow; that path is documented in `admissions.md`.

### 3b. Request

`CreateParentRequest`. Required: `name` (max 100) and `mobile` (a phone number, max 20). Optional, each checked against its column width (the limits live in `ParentFieldLimits` and a test compares them with the EF model):

| Field | Rule |
|---|---|
| `email`, `workEmail` | valid email address, max 100 |
| `mobile`, `whatsapp`, `workPhone` | phone format (digits, spaces, `+ - ( ) .`, 5 to 20 characters, same rule as guardians and pickup persons, `PhoneNumberRules`) and max 20 characters of raw text; `whatsapp` and `workPhone` may be empty |
| `occupation`, `employer`, `jobTitle` | max 100 |
| `nationality`, `countryOfResidence`, `timezone` | max 50 |
| `preferredLanguage`, `preferredContactMethod` | max 30 |

Booleans without a "not sent" state: `emergencyOnly`, `notifyAttendance`, `notifyExams`, `notifyFees`, `notifyNotices`, `notifyDiscipline`, `billingContact`. Also `addresses[]`.

Addresses: each is validated, at most one per type, and exactly one must be primary (when any are sent).

### 3c. Response

`201 Created`, `Location: api/parents/{id}`, body `ParentResponse`.

### 3d. Logic, in order

1. Validator (400).
2. `AddressService.BuildAsync` checks countries and regions and builds the address rows (404/400 style errors from there if a country is unknown).
3. Create the `Parent` with status `Active` and the addresses, one `SaveChanges`.
4. Return the full response.

### 3e. Business rules and where each is enforced

| Rule | Where |
|---|---|
| Name and mobile are required | validator |
| New parents start `Active` | service |
| Several parents may share one mobile | by design (non-unique `IX_Parents_MobileKey`) |
| One address per type, exactly one primary | `ValidateAddresses` |

### 3f. Errors

| Status | Code | When |
|---|---|---|
| 400 | `VALIDATION_ERROR` | validator rules |
| 404 | `NOT_FOUND` | an address country or region id does not exist (**not verified** which exception `AddressService` throws) |
| 403 | `FORBIDDEN` | missing `Parents.Create` |

A text longer than its column, a malformed email or a malformed phone is now a **400** with the field name as the key (for example `"Occupation": ["The length of 'Occupation' must be 100 characters or fewer. You entered 130 characters."]`), not a 500 (fixed 2026-10-19; tests `ParentRequestValidatorTests`, `ParentFieldLimitsTests`).

### 3g. Concurrency and retries

No duplicate detection: sending the same request twice creates two parents. The React form disables the button while saving; the API itself does not guard against it.

### 3h. Why is it built this way?

Duplicates are allowed on purpose because two real parents can share a phone; the admission screen asks staff to confirm a match instead.

### Example (invented)

```json
{ "name": "Mary Thomas", "mobile": "+971501112233", "email": "mary@example.com",
  "emergencyOnly": false, "notifyAttendance": true, "notifyExams": true, "notifyFees": true,
  "notifyNotices": true, "notifyDiscipline": true, "billingContact": false }
```

---

## 4. `PUT /api/parents/{id}`

### 4a. Purpose and caller

Saves the parent form.

### 4b. Request

`UpdateParentRequest`: the same fields and the same length and format rules as create (endpoint 3), plus `status` (`Active` or `Inactive`) and `rowVersion`. `addresses`: `null` keeps the stored ones, a list replaces them (an empty list clears them).

### 4c. Response

`200 OK` with the full `ParentResponse` (new `rowVersion`).

### 4d. Logic, in order

1. Validator (400): row version shape, name, mobile, email, status, addresses.
2. Load the parent (404). `Expect(parent, rowVersion)`: stale gives **409**.
3. Build the new addresses first (so nothing is half-written if one is invalid).
4. Overwrite all scalar fields from the request.
5. No addresses sent: one `SaveChanges`. Addresses sent: a transaction that replaces them (`AddressService.ReplaceAsync`) and saves.
6. Return the response.

### 4e. Business rules and where each is enforced

| Rule | Where |
|---|---|
| Someone else's newer save is not overwritten | `rowversion` + `IConcurrencyGuard` |
| Status is Active or Inactive | validator + `CK_Parents_Status` |
| Address replacement is all-or-nothing | transaction |

Setting a parent `Inactive` has **no other effect** in this controller: links, logins and notifications are untouched (**BUSINESS DECISION REQUIRED**: what should Inactive mean?).

### 4f. Errors

400 `VALIDATION_ERROR` (including over-long text and malformed email or phone, with the field named), 404 `NOT_FOUND`, 409 `CONCURRENCY_CONFLICT`, 403 `FORBIDDEN`.

### 4g. Concurrency and retries

A double submit gives the second call a 409 because the `rowVersion` it carries is now old.

### 4h. Why is it built this way?

Everything on the form is sent every time (a full replace, not a patch), which is simple and, with the row version, safe.

### Example (invented)

```json
{ "rowVersion": "AAAAAAAAB7c=", "name": "George Thomas", "mobile": "+971509876543", "status": "Active",
  "emergencyOnly": false, "notifyAttendance": true, "notifyExams": true, "notifyFees": true,
  "notifyNotices": true, "notifyDiscipline": false, "billingContact": true, "addresses": null }
```

---

## 5. `DELETE /api/parents/{id}`

### 5a. Purpose and caller

Soft-deletes a parent. Called from the list and the detail page, after a confirmation.

### 5b. Request

`id` in the path.

### 5c. Response

`204 No Content`.

### 5d. Logic, in order

1. Load the parent (404).
2. Count links to students who are not deleted. Any link gives **409**: "This parent is linked to N student(s). Unlink them first, then delete the parent."
3. Set `IsDeleted` and `DeletedAt` (the interceptor fills `DeletedBy`); save.

### 5e. Business rules and where each is enforced

| Rule | Where |
|---|---|
| A parent with children linked cannot be deleted | `ParentService.DeleteAsync` |
| Deleted parents disappear from every read | query filter |

The parent's **login account** (if any) is not touched; see the questions at the end.

### 5f. Errors

404 `NOT_FOUND`, 409 `CONFLICT`, 403 `FORBIDDEN`.

### 5g. Concurrency and retries

A second call gets 404.

### 5h. Why is it built this way?

Deleting a parent who is the only guardian would leave a child with nobody to contact, so the user must unlink first and decide consciously.

### Example (invented)

```json
{ "success": false, "message": "This parent is linked to 2 students. Unlink them first, then delete the parent.",
  "errorCode": "CONFLICT", "traceId": "0HN7M2..." }
```

---

## 6. `GET /api/parents/{id}/students`

### 6a. Purpose and caller

A parent's children, for the "Linked children" panel on the detail page.

### 6b. Request

`id` in the path.

### 6c. Response

`200 OK`, array of `LinkedStudentResponse`: `studentId, studentName, admNo, classSectionName, rollNumber, relationType, isPrimaryContact`.

### 6d. Logic, in order

1. Resolve the parent scope.
2. `GetVisibleByIdAsync`: unknown or out-of-scope parent is **404** (it used to return an empty list).
3. `GetForParentAsync(parentId, scope)`: the links, restricted to students the caller may see. A teacher opening a parent sees only the children they teach, not siblings in other classes.

### 6e. Business rules and where each is enforced

Scope applied twice: once to the parent, once to the children (`Students.VisibleTo`).

### 6f. Errors

404 `NOT_FOUND`, 403 `FORBIDDEN`.

### 6g. Concurrency and retries

Read-only.

### 6h. Why is it built this way?

Reusing the student scope means this endpoint can never reveal a child the caller could not open directly.

### Example (invented)

```json
[ { "studentId": 41, "studentName": "Anu Thomas", "admNo": "ADM-2026-0007",
    "classSectionName": "Class 5 - A (2026-27)", "rollNumber": "5A003",
    "relationType": "Father", "isPrimaryContact": true } ]
```

---

## 7. `POST /api/parents/{id}/students`

### 7a. Purpose and caller

Links a child to this parent. Called by `LinkChildDialog` (student picked from the student list).

### 7b. Request

`LinkStudentRequest`: `studentId` (greater than 0), `relationType` (`Father`, `Mother` or `Guardian`), `isPrimaryContact`.

### 7c. Response

`201 Created`, `Location: api/parents/{id}/students/{studentId}`, body `LinkedStudentResponse`.

### 7d. Logic, in order

1. Validator (400).
2. Load the student (404 "Student X does not exist.").
3. Load the parent (404 "Parent X does not exist.").
4. `LinkExistsAsync`: already linked gives **409**.
5. If `isPrimaryContact` is true and the student already has a primary contact: **409** naming the current primary ("Student X already has a primary contact (Name). Link this guardian without the primary flag, then use "Set as primary" to switch.").
6. Insert the link and save. A conflicting row saved by someone else between step 5 and now is turned into a **409** by the repository.

This is the same service method set as the student-side link (`students.md` endpoint 8), so both sides behave identically.

### 7e. Business rules and where each is enforced

| Rule | Where |
|---|---|
| A parent and student are linked once | primary key + pre-check |
| Relation is Father, Mother or Guardian | validator + check constraint |
| One primary contact per student | service pre-check (409 with a message) + filtered unique index as the final guard (fixed 2026-10-19; test `GuardianLinkRulesTests`) |

### 7f. Errors

| Status | Code | When |
|---|---|---|
| 400 | `VALIDATION_ERROR` | bad relation type, student id |
| 404 | `NOT_FOUND` | student or parent missing |
| 409 | `CONFLICT` | already linked; the student already has a primary contact; a conflicting link saved at the same moment |

### 7g. Concurrency and retries

Two identical calls: one 201, one 409. Two different parents linked as primary for one student at the same moment: one 201, one 409 (never a 500).

### 7h. Why is it built this way?

A link is a separate record with its own columns, so it has its own endpoints; `Parents.Edit` guards it because it changes what the parent can see through scoped access.

### Example (invented)

```json
{ "studentId": 41, "relationType": "Mother", "isPrimaryContact": false }
```

---

## 8. `DELETE /api/parents/{id}/students/{studentId}`

### 8a. Purpose and caller

Unlinks a child. Called from the detail page.

### 8b. Request

Two ids in the path.

### 8c. Response

`204 No Content`.

### 8d. Logic, in order

`StudentGuardianService.UnlinkAsync(studentId, parentId)`: find the link (404 "No guardian link exists between student X and parent Y." if none), remove it, save.

### 8e. Business rules and where each is enforced

None beyond `Parents.Edit`. Removing the primary contact or the last guardian is allowed (same open question as in `students.md`).

### 8f. Errors

404 `NOT_FOUND`, 403 `FORBIDDEN`.

### 8g. Concurrency and retries

A second call returns 404.

### 8h. Why is it built this way?

The parent URL and the student URL both call the same service method, so the rule lives in one place.

### Example (invented)

`DELETE /api/parents/9/students/41` returns `204`.

---

## 9. `POST /api/parents/{id}/students/{studentId}/primary`

### 9a. Purpose and caller

Makes this parent the **primary contact** of that student. Called from the "Linked children" list.

### 9b. Request

Two ids in the path. No body.

### 9c. Response

`204 No Content`.

### 9d. Logic, in order

`StudentGuardianService.SetPrimaryAsync` calls `StudentGuardianRepository.SetPrimaryAsync(studentId, parentId)`, which runs in **one transaction**:

1. Load all links of the student.
2. No link for this parent: return "not found" without changing anything, and the service answers **404** ("No guardian link exists between student X and parent Y.").
3. Clear `IsPrimaryContact` on the other links that have it, and **save** (this frees the one primary slot).
4. Set it on this parent's link (unless it already is) and save.
5. Commit. If any step fails, everything rolls back and the old primary is still in place.

### 9e. Business rules and where each is enforced

| Rule | Where |
|---|---|
| Only one primary per student | the order above plus the filtered unique index `UX_StudentGuardians_Student_Primary` |
| The link must exist | repository returns false, service throws `NotFoundException` (fixed 2026-10-19) |
| The old primary is never cleared without the new one being set | one transaction (fixed 2026-10-19) |

Why two saves: SQL Server checks a unique index per statement, and one `SaveChanges` orders its updates by primary key, so switching to a parent with a lower id would try to set the new primary before clearing the old one. Tests on a real database: `GuardianPrimaryContactTests`.

### 9f. Errors

404 `NOT_FOUND` (no such link, which includes an unknown or deleted student or parent), 403 `FORBIDDEN`.

### 9g. Concurrency and retries

Repeating the call is harmless: setting the parent who is already primary changes nothing.

### 9h. Why is it built this way?

Switching the primary is all-or-nothing, so the student is never left with no primary because of a half-done switch.

### Example (invented)

`POST /api/parents/9/students/41/primary` returns `204`; the next `GET /api/parents/9/students` shows `"isPrimaryContact": true` for student 41. If parent 9 is not linked to student 41:

```json
{ "success": false, "message": "No guardian link exists between student 41 and parent 9.",
  "errorCode": "NOT_FOUND", "traceId": "0HN7M3..." }
```

---

## Doubts and possible problems

### Fixed on 2026-10-19 (these were "Should fix" in the first version of this file)

1. **Setting primary for a link that does not exist silently removed the current primary.** It now returns 404 and changes nothing (endpoint 9).
2. **Linking could return 500.** Both link endpoints now answer 404 for an unknown student or parent and 409 for a second primary contact (endpoint 7; student side in `students.md` endpoint 8).
3. **Missing length and format validation on parents.** Create and edit now check every text field against its column width, and the email and phone fields for format, with the field named in the 400 (endpoints 3 and 4). A consequence to be aware of: `mobile` now has to match the phone format (5 to 20 characters of digits, spaces and `+ - ( ) .`). A parent stored earlier with a different format will get a 400 on `mobile` the next time someone edits and saves them.
4. **`Set primary` could trip the unique index** because of the update order. Fixed by the two-step transaction in endpoint 9. This has been written as a real-database test (`GuardianPrimaryContactTests`); see the report for what has and has not been run.

### Should fix

None open in this controller at the moment.

### Question

1. What should `Inactive` mean for a parent (stop notifications, block login, hide from pickers)? Today nothing reacts to it.
2. Deleting a parent leaves their login account (`Users` row) in place. Should the account be disabled when the parent is deleted?
3. Should creating a parent warn about an existing parent with the same mobile (as admissions does)?
4. No paging on the list (same as students).
