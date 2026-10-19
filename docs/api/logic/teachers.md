# Teachers controller: what each endpoint actually does

Source: `backend/SchoolManagement.API/Controllers/TeachersController.cs`, route prefix `api/teachers`.
Written for a developer with about two years of experience who is learning this codebase.

## How this file was made

- **Everything here was read from the code**: the controller, `TeacherService`, `TeacherRepository`, the three validators, the `Teacher` entity and `TeacherConfiguration`, the DTOs, the React service, and the places that use teachers (users, subjects, class sections, data scope).
- **I did not run the API or the database.** There is no SQL Server in my environment. So **every example below is invented**: shapes, field names, error codes and messages come from the code, but ids and names are made up.
- **Not verified** marks anything I could not confirm from the code alone.
- Tests: the access matrix covers the permission of all nine endpoints, and `DataScopeTests` builds teachers, class teachers and subject teachers to test what they may see. **No test exercises the assignment rules in this controller** (one teacher per subject, class-teacher rules, delete refusal).
- The doubts I noticed are at the end, labelled **Blocker**, **Should fix** or **Question**.

## The nine endpoints at a glance

| # | Method + URL | Permission needed | Called by (React) |
|---|---|---|---|
| 1 | `GET /api/teachers` | `Teachers.View` | `TeachersPage`, drop-downs |
| 2 | `GET /api/teachers/{id}` | `Teachers.View` | `TeacherDetailPage`, `TeacherFormPage` |
| 3 | `PUT /api/teachers/{id}` | `Teachers.Edit` | `TeacherFormPage` |
| 4 | `DELETE /api/teachers/{id}` | `Teachers.Delete` | `TeachersPage`, `TeacherDetailPage` |
| 5 | `GET /api/teachers/{id}/assignments` | `Teachers.View` | `TeacherDetailPage` |
| 6 | `POST /api/teachers/{id}/subjects` | `Teachers.Assign` | `AssignSubjectsDialog` |
| 7 | `DELETE /api/teachers/{id}/subjects/{subjectId}` | `Teachers.Assign` | `TeacherDetailPage` |
| 8 | `PUT /api/teachers/{id}/class-teacher` | `Teachers.Assign` | `TeacherDetailPage` |
| 9 | `DELETE /api/teachers/{id}/class-teacher/{classSectionId}` | `Teachers.Assign` | `TeacherDetailPage` |

There is **no `POST /api/teachers`**: a teacher is created on the Users page (`POST /api/users` with the Teacher role, see `users.md`). The permission catalog adds dependencies: `Teachers.Assign` needs `Subjects.View` and `ClassSections.View`.

## Background you need before the endpoints

### The same request pipeline as Auth

Correlation id, Serilog, `GlobalExceptionMiddleware`, JWT, `MustChangePasswordMiddleware`, then the permission policy. See `auth.md`. Refused permission is **403**.

### What a teacher is

A `Teachers` row is only a thin profile: `UserId` (one login per teacher) and `Specialization`. Everything else is borrowed:

- **Name and phone** are the staff profile's `FullName` and `Mobile` (one copy, not two).
- **Username, e-mail and status** belong to the login (`Users`).
- **Subjects** are stored on the subject (`Subjects.TeacherId`).
- **Class teacher** is stored on the class section (`ClassSections.ClassTeacherId`).

So the "assignments" are not a table of their own; they are those two foreign keys.

### Two different "teacher of a class" ideas

- **Subject teacher**: teaches one or more subjects of a class. A subject has **one** teacher.
- **Class teacher** (homeroom): one per class section. The teacher must already teach at least one subject in that class.

### Soft delete and audit

Deleted teachers are hidden by a query filter (also when the login is deleted). `AuditableEntitySaveChangesInterceptor` writes the audit rows. **`Teachers` has no `RowVersion`.**

---

## 1. `GET /api/teachers`

### 1a. Purpose and caller
The Teachers list.

### 1b. Request
No body, no query string, no paging.

### 1c. Response
`200 OK`, array of `TeacherResponse`: `id, userId, name, username, email, phone, specialization, status, classCount, subjectCount, classTeacherOfCount`.

### 1d. Logic, in order
1. Policy `Teachers.View`.
2. Load all teachers with login and staff profile, sorted by full name.
3. `GetStatsAsync`: **two** grouped queries (subjects per teacher, class-teacher count per teacher). No N+1.
4. Map; a teacher with no assignments gets zeros.

### 1e. Business rules
| Rule | Where |
|---|---|
| Deleted teachers hidden | query filter |
| Counts cover **all years**, not just the current one | `GetStatsAsync` has no year filter |

### 1f. Errors
401 no token, 403 no `Teachers.View`.

### 1g. Concurrency and retries
Read only.

### 1h. Why is it built this way?
Two grouped queries are cheaper than loading every subject per teacher.

### Example (invented)
```json
[ { "id": 7, "userId": 31, "name": "Meera Nair", "username": "meera.nair", "email": "meera@school.example",
    "phone": "+971501234567", "specialization": "Mathematics", "status": "Active",
    "classCount": 2, "subjectCount": 3, "classTeacherOfCount": 1 } ]
```

---

## 2. `GET /api/teachers/{id}`

### 2a. Purpose and caller
One teacher, for the detail page and the edit form.

### 2b. Request
`id` in the URL.

### 2c. Response
`200 OK`, one `TeacherResponse`.

### 2d. Logic, in order
1. Policy `Teachers.View`. 2. Load the teacher; missing gives **404** `Teacher {id} does not exist.` 3. Look up the stats and map. (Note: the stats call loads the counts for **all** teachers and picks one; see *Question* 5.)

### 2e. Business rules
Same as endpoint 1.

### 2f. Errors
403 `FORBIDDEN`; 404 `NOT_FOUND`.

### 2g. Concurrency and retries
Read only. No row version is returned.

### 2h. Why is it built this way?
Reuses the list's mapper.

### Example (invented)
```json
{ "success": false, "message": "Teacher 999 does not exist.", "errorCode": "NOT_FOUND", "traceId": "0HN7K2..." }
```

---

## 3. `PUT /api/teachers/{id}`

### 3a. Purpose and caller
Saves the teaching profile: name, phone, specialization. **Not** the login (username, e-mail, status, password): those are changed on the Users page.

### 3b. Request
`UpdateTeacherRequest`:

| Field | Rule |
|---|---|
| `name` | required, max 150 |
| `phone` | optional; same phone rule as the staff profile mobile (`UserRequestRules.IsOptionalPhone`) |
| `specialization` | optional, max 100 |

### 3c. Response
`200 OK`, the updated `TeacherResponse`.

### 3d. Logic, in order
1. Validator (400 on failure).
2. Load the teacher (404 if missing).
3. Set `Specialization` (blank becomes `null`).
4. Write the trimmed name to `StaffProfile.FullName` and the phone to `StaffProfile.Mobile` (the profile is created if it was missing).
5. **`User.NewVersion()`**: bumps the login's row version, so a Users-page edit form opened earlier gets a 409 and must reload.
6. Save, reload stats, return.

### 3e. Business rules
| Rule | Where |
|---|---|
| Name and phone live once, on the staff profile | service |
| Editing here invalidates the open Users form for the same person | `NewVersion()` |

### 3f. Errors
400 `VALIDATION_ERROR`; 403; 404 `NOT_FOUND`.

### 3g. Concurrency and retries
There is **no version check on this endpoint**, so two editors of the teaching profile overwrite each other. Repeating the same PUT is harmless.

### 3h. Why is it built this way?
One copy of the name avoids the teacher list and the user list disagreeing.

### Example (invented)
```json
{ "name": "Meera Nair", "phone": "+971501234567", "specialization": "Mathematics" }
```

---

## 4. `DELETE /api/teachers/{id}`

### 4a. Purpose and caller
Deletes the teacher **and their login**.

### 4b. Request
`id` in the URL.

### 4c. Response
`204 No Content`.

### 4d. Logic, in order
1. Policy `Teachers.Delete`.
2. Load the teacher (404 if missing).
3. Stats: if the teacher has any subject or is class teacher of any class: **409** `Cannot delete 'Meera Nair' — still assigned to 3 subject(s) and class teacher of 1 class(es). Remove the assignments first.`
4. Otherwise `IUserAccountService.DeleteAsync(userId)`: in one transaction it deletes the login, the staff profile and this teacher record, and ends the teacher's sessions. That same method guards the **last active Admin** and the **account-change lock** (see `users.md`).

### 4e. Business rules
| Rule | Where |
|---|---|
| Cannot delete while assigned | service (counts **all years**, including past ones) |
| Deleting a teacher deletes the login | `UserAccountService` |

### 4f. Errors
403; 404; 409 `CONFLICT` (assignments exist, or the account rule refuses).

### 4g. Concurrency and retries
The assignment check and the delete are separate steps; someone could assign a subject in between (tiny window). A repeat returns 404.

### 4h. Why is it built this way?
A teacher record has no meaning without the login, so both go together.

### Example (invented)
```json
{ "success": false, "message": "Cannot delete 'Meera Nair' — still assigned to 3 subject(s) and class teacher of 1 class(es). Remove the assignments first.", "errorCode": "CONFLICT", "traceId": "0HN7K2..." }
```

---

## 5. `GET /api/teachers/{id}/assignments`

### 5a. Purpose and caller
What this teacher teaches, grouped by class. Detail page.

### 5b. Request
`id` in the URL.

### 5c. Response
`200 OK`, array of `TeacherAssignmentResponse`: `classSectionId, classSectionName, academicYearName, isClassTeacher, subjects[{ subjectId, name, code }]`.

### 5d. Logic, in order
1. Policy `Teachers.View`. 2. Teacher must exist (404). 3. Load the teacher's subjects and the classes they are class teacher of. 4. Group by class, sort newest year first, then grade, name, section; subjects by name.

### 5e. Business rules
A class where the teacher is class teacher but has **no subject** does not appear here (the list is built from subjects). The rules below prevent that state, except after the cleanup gaps in *Should fix* 4.

### 5f. Errors
403; 404.

### 5g. Concurrency and retries
Read only. Every assignment call below returns this same list, so the screen refreshes from the response.

### 5h. Why is it built this way?
Assignments are derived data, so one builder serves all the endpoints.

### Example (invented)
```json
[ { "classSectionId": 12, "classSectionName": "Class 5 A (2026-2027)", "academicYearName": "2026-2027",
    "isClassTeacher": true, "subjects": [ { "subjectId": 41, "name": "Mathematics", "code": "MATH" } ] } ]
```

---

## 6. `POST /api/teachers/{id}/subjects`

### 6a. Purpose and caller
Gives a teacher some subjects of one class, optionally making them the class teacher too.

### 6b. Request
`AssignSubjectsRequest`: `classSectionId` (> 0), `subjectIds` (not empty, each > 0), `makeClassTeacher` (default `false`).

### 6c. Response
`200 OK`, the teacher's updated assignment list.

### 6d. Logic, in order
1. Validator (400).
2. Teacher exists (404); class exists (404 `Class section {id} does not exist.`).
3. Distinct subject ids; if any id does not exist: **404** `One or more subjects do not exist.`
4. Every subject must belong to that class, else **409** `These subjects do not belong to Class 5 A (2026-2027): ...`
5. **One teacher per subject**: a subject already taught by someone else gives **409** `Already taught by another teacher: Mathematics (Rahul Das).` It does not reassign silently.
6. If `makeClassTeacher`: the class must have no other class teacher, else **409** `Class 5 A (...) already has a class teacher (Rahul Das). Remove them as class teacher first.`
7. Set `TeacherId` on the subjects (and `ClassTeacherId` on the class), save, return the list.

### 6e. Business rules
| Rule | Where |
|---|---|
| A subject has one teacher | service |
| One class teacher per class | service; plus the class section's row version |
| Assigning the same subject again to the same teacher is allowed | the check is `TeacherId != id` |
| **Inactive teacher, Inactive class, Inactive subject, or past-year class are not checked** | nothing |

### 6f. Errors
400, 403, 404, 409 as above.

### 6g. Concurrency and retries
Subjects have **no row version**, so two admins assigning the same subject to different teachers at the same instant: the last save wins and the first admin is not told. The class teacher write is protected by the class section's row version (a clash gives 409 `CONCURRENCY_CONFLICT`). A repeat of the same request is harmless.

### 6h. Why is it built this way?
Blocking instead of reassigning avoids taking a subject from a teacher by accident.

### Example (invented)
```json
{ "classSectionId": 12, "subjectIds": [41, 42], "makeClassTeacher": true }
```

---

## 7. `DELETE /api/teachers/{id}/subjects/{subjectId}`

### 7a. Purpose and caller
Takes one subject away from the teacher.

### 7b. Request
Both ids in the URL, no body.

### 7c. Response
`200 OK`, the updated assignment list.

### 7d. Logic, in order
1. Teacher exists (404). 2. Subject exists (404 `Subject {id} does not exist.`). 3. It must be this teacher's, else **404** `This subject is not assigned to this teacher.`
4. **Side effect**: if this was the teacher's **last subject in that class** and they are its class teacher, the class teacher is cleared too (mirrors the prototype).
5. `TeacherId = null`, save, return.

### 7e. Business rules
| Rule | Where |
|---|---|
| Removing the last subject drops the class-teacher role | service |

### 7f. Errors
403; 404 (teacher, subject, or not assigned).

### 7g. Concurrency and retries
Repeating the call returns 404 (not assigned any more). No version check.

### 7h. Why is it built this way?
A class teacher who teaches nothing in the class is invalid under the rule in endpoint 8.

### Example (invented)
`DELETE /api/teachers/7/subjects/41` returns the remaining list; if Mathematics was the only subject in Class 5 A, `isClassTeacher` is gone too because the class disappears from the list.

---

## 8. `PUT /api/teachers/{id}/class-teacher`

### 8a. Purpose and caller
Makes the teacher the class teacher of a class.

### 8b. Request
`{ "classSectionId": 12 }` (> 0).

### 8c. Response
`200 OK`, the updated assignment list.

### 8d. Logic, in order
1. Validator. 2. Teacher and class exist (404).
3. The teacher must teach **at least one subject** in the class, else **409** `Assign at least one subject in Class 5 A (...) before making this teacher its class teacher.`
4. The class must have no other class teacher, else **409** (same message as endpoint 6).
5. Set `ClassTeacherId`, save, return.

### 8e. Business rules
| Rule | Where |
|---|---|
| Class teacher must teach in the class | service |
| One class teacher per class | service + class section row version |
| A teacher can be class teacher of several classes | no rule stops it |
| Setting yourself again is a no-op | allowed |

### 8f. Errors
400, 403, 404, 409.

### 8g. Concurrency and retries
Protected by the class section's row version: a concurrent change gives 409 `CONCURRENCY_CONFLICT`.

### 8h. Why is it built this way?
The prototype stored the class-teacher flag on an assignment, so it only existed next to a subject.

### Example (invented)
```json
{ "classSectionId": 12 }
```

---

## 9. `DELETE /api/teachers/{id}/class-teacher/{classSectionId}`

### 9a. Purpose and caller
Removes the class-teacher role (the subjects stay).

### 9b. Request
Both ids in the URL.

### 9c. Response
`200 OK`, the updated assignment list.

### 9d. Logic, in order
1. Teacher and class exist (404). 2. The class's class teacher must be this teacher, else **409** `This teacher is not the class teacher of Class 5 A (...).` 3. Clear it, save, return.

### 9e. Business rules
Only the current class teacher can clear it.

### 9f. Errors
403, 404, 409.

### 9g. Concurrency and retries
A repeat returns 409 (not the class teacher any more), which the screen should treat as already done.

### 9h. Why is it built this way?
It makes "not the class teacher" an explicit error instead of a silent no-op.

### Example (invented)
`DELETE /api/teachers/7/class-teacher/12`

---

## Doubts and possible problems

### Blocker
None found.

### Should fix
1. **Inactive things can be assigned.** The assign endpoints do not check that the teacher's login is Active, the class is Active, the subject is Active, or that the class belongs to the current academic year. Data-scope queries only look at current-year classes, so an assignment in a past or future year is silently useless for access.
2. **No row version on `Subjects`.** Two simultaneous assigns of one subject (endpoint 6) can both pass the "one teacher" check; the last write wins with no warning.
3. **Delete counts every year.** A teacher who taught in 2024 is refused delete forever unless an admin unassigns old subjects one by one. Decide whether past years should count.
4. **Class-teacher cleanup gaps.** The "last subject removed also removes class teacher" rule runs only in endpoint 7. Other ways to lose that subject do not run it: deleting the subject (`subjects.md`), or moving the subject to another class. The class is then left with a class teacher who teaches nothing in it.
5. **No tests for the assignment rules.** One-teacher-per-subject, class-teacher rules and the delete refusal have no test; only permissions and data scope are covered.
6. **Name/phone edits have no version check** (endpoint 3), while the Users page does. They also bump the login's version, so a user editing in the other tab is surprised by a 409.

### Question
1. **Should a teacher be allowed to be class teacher of more than one class** in the same year? Nothing stops it today.
2. **Should endpoint 6 allow re-assigning** with an explicit "take over" flag instead of making the admin unassign first?
3. **When a teacher's login is set Inactive on the Users page, should their assignments stay?** Today they stay and the teacher still shows in drop-downs.
4. **`Phone` and `Email` are visible to everyone with `Teachers.View`.** Is that fine for roles such as Parent or Student (if they ever hold it)?
5. **`GET /api/teachers/{id}` loads the stats for every teacher** to read one row. Harmless now; worth a single-teacher query if the list grows.
6. **There is no list filter or paging**, same as the other master lists.
