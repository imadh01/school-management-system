# Users controller: what each endpoint actually does

Source: `backend/SchoolManagement.API/Controllers/UsersController.cs`, route prefix `api/users`.
Written for a developer with about two years of experience who is learning this codebase.

## How this file was made

- **Everything here was read from the code**: the controller, `UserService`, `UserAccountService`, `UserRepository`, the validators, the DTOs, `UserConfiguration` and `StaffProfileConfiguration`, and the names of the tests.
- **I did not run the API or the database.** There is no SQL Server in my environment. So **every example below is invented**: shapes, field names, error codes and messages come from the code, but ids, names and numbers are made up.
- Tests exist: `UsersApiTests` (create, teacher record, Student/Parent refused, employee number once, missing fields, stale edit, role change, deactivate, reset password, unlock, delete, self-protection, `NeedsName`), `UserServiceTests`, `UserAccountRulesTests`, `AccountSecurityTests`, `UserUniquenessTests`, `EmployeeNumberTests` and `CreateUserRequestValidatorTests`. I did not run them in this session.
- **Not verified** marks anything I could not confirm from the code alone.
- The doubts I noticed are at the end, labelled **Blocker**, **Should fix** or **Question**.

## The eight endpoints at a glance

| # | Method + URL | Permission needed | Called by (React) |
|---|---|---|---|
| 1 | `GET /api/users` | `Users.View` | `UsersPage` |
| 2 | `GET /api/users/{id}` | `Users.View` | `UserFormPage`, user detail |
| 3 | `POST /api/users` | `Users.Create` | `UserFormPage` (new) |
| 4 | `PUT /api/users/{id}` | `Users.Edit` | `UserFormPage` (edit) |
| 5 | `PATCH /api/users/{id}/status` | `Users.ChangeStatus` | `UsersPage` |
| 6 | `POST /api/users/{id}/reset-password` | `Users.ResetPassword` | `UsersPage` |
| 7 | `POST /api/users/{id}/unlock` | `Users.ChangeStatus` | `UsersPage` |
| 8 | `DELETE /api/users/{id}` | `Users.Delete` | `UsersPage` |

My own details are on the Me controller (see `me.md`), not here.

## Background you need before the endpoints

### The same request pipeline as Auth

Correlation id, Serilog, `GlobalExceptionMiddleware`, JWT, `MustChangePasswordMiddleware`, then the permission policy. See `auth.md` for login, tokens and lockout.

### What a "user" is here

A **staff login**: a `Users` row (username, e-mail, password hash, status, role) plus a `StaffProfiles` row (full name, gender, mobile, joining date, employee number, ...) plus addresses. Teachers also get a `Teachers` row. **Student and Parent logins do not exist yet** (the roles are refused).

### One role per user

The table is many-to-many, but the service keeps **one role per user**. A role change replaces the old one.

### The account service (`UserAccountService`) and its safety rules

Everything that changes **what a user may do** (status, password, role, delete) goes through one place that applies these rules:

| Rule | Message (shortened) | Status |
|---|---|---|
| Not your own account | `You cannot {change the status of / reset the password of / delete} your own account. Ask another administrator...` | 422 |
| Never the **last active Admin** | `'admin' is the last active Admin. Make another user an active Admin first...` | 422 |
| Role must exist, be active, not Student/Parent | `Role '...' does not exist.` / `is inactive` / `The Student role cannot be given yet...` | 422 |
| You cannot give a role with permissions you do not hold yourself | `You cannot give the 'X' role: it has permissions you do not hold yourself (...)` | 403 |
| A teacher who still teaches cannot be deleted or moved to another role | `Cannot delete this account: the teacher still teaches 3 subject(s)...` | 422 |

All changes run in a transaction behind one **database lock** (`sp_getapplock`, name `UserAccountChanges`). Two admins deactivating each other at the same moment queue up, so the school cannot end up with no Admin. A lock timeout (10 s) gives 409 `Another account change is being saved right now. Please try again.`

After a status/password/role/delete change the user's **sessions end at once**: a new `SecurityStamp` makes the current access token useless, the refresh tokens are revoked, and the permission cache is cleared.

### Concurrency

`Users.RowVersion` is a `rowversion`. The edit and status requests send it back; `NewVersion()` is called by every account change, so an edit form opened before a status change gets a 409 when saved.

### Soft delete and audit

`Users.IsDeleted` plus a query filter. Username and e-mail are unique only among **non-deleted** users (filtered unique indexes), so a deleted person's username can be reused. `AuditableEntitySaveChangesInterceptor` writes the audit rows.

---

## 1. `GET /api/users`

### 1a. Purpose and caller
The Users list.

### 1b. Request
None (no paging or filters; the page filters in the browser).

### 1c. Response
`200 OK`, array of `UserListItemResponse`: `id, username, fullName, needsName, email, mobile, role, gender, status, joiningDate, lastLoginAt, isLocked, mustChangePassword, teacherId, employeeNumber`.

### 1d. Logic, in order
1. Policy `Users.View`. 2. One projection query (no password hash, no addresses), newest id first. `isLocked` = lockout end is in the future (UTC now). `role` is the user's first role.

### 1e. Business rules
| Rule | Where |
|---|---|
| Deleted users hidden | query filter |
| `needsName` is true when the full name is still the username copied in by the migration | repository |

### 1f. Errors
403.

### 1g. Concurrency and retries
Read only.

### 1h. Why is it built this way?
A projection keeps secrets out of the list and the query small.

### Example (invented)
```json
[ { "id": 31, "username": "meera.nair", "fullName": "Meera Nair", "needsName": false, "email": "meera@school.example",
    "mobile": "+971501234567", "role": "Teacher", "gender": "Female", "status": "Active",
    "joiningDate": "2024-08-15", "lastLoginAt": "2026-10-19T04:00:00Z", "isLocked": false,
    "mustChangePassword": false, "teacherId": 7, "employeeNumber": "EMP-0007" } ]
```

---

## 2. `GET /api/users/{id}`

### 2a. Purpose and caller
One user in full, to fill the edit form.

### 2b. Request
`id` in the URL.

### 2c. Response
`200 OK`, `UserResponse`: `id, username, email, status, roles[], mustChangePassword, isLocked, lastLoginAt, createdAt, teacherId, profile{...}, addresses[], rowVersion`.

### 2d. Logic, in order
1. Policy. 2. Load user, roles, profile and addresses (country and region), one split query. Missing gives **404** `User {id} was not found.` 3. Look up the teacher id and map.

### 2e. Business rules
The password hash and the security stamp are never returned.

### 2f. Errors
403, 404.

### 2g. Concurrency and retries
Returns the `rowVersion` the edit must send back.

### 2h. Why is it built this way?
The edit screen needs everything in one call.

### Example (invented)
`GET /api/users/999` → `404` `User 999 was not found.`

---

## 3. `POST /api/users`

### 3a. Purpose and caller
Creates a staff login with its profile (and a teacher record for the Teacher role).

### 3b. Request
`CreateUserRequest`:

| Field | Rule |
|---|---|
| `username` | required, max 50, letters/digits/`.`/`_`/`-` only |
| `email` | required, max 100, valid e-mail |
| `password` | required; at least 8 characters with an upper-case letter, a lower-case letter, a digit and a special character |
| `roleName` | required |
| `profile.fullName` | required, max 150 |
| `profile.gender` | required, `Male` or `Female` |
| `profile.dateOfBirth` | optional, must be in the past |
| `profile.mobile` | required, valid phone (`PhoneNumberRules`) |
| `profile.joiningDate` | required |
| `profile.qualification` | optional, max 150 |
| `profile.employeeNumber` | optional, max 30; **blank = generated** |
| `profile.bloodGroup` | optional, max 10 |
| `profile.emergencyContactName` / `Phone` | optional (max 150 / valid phone) |
| `addresses` | optional list, each address validated |
| `specialization` | optional, max 100 (used for the Teacher role) |

### 3c. Response
`201 Created`, `Location: api/users/{id}`, `UserResponse`.

### 3d. Logic, in order
1. Validator (400 with a field map).
2. Username, e-mail and (if typed) employee number must be unused: **409** `Username 'x' is already taken.` / `Email 'x' is already registered.` / `Employee number 'x' is already used by another staff member.`
3. **Role is prepared** (exists, active, not Student/Parent, you hold all its permissions).
4. Addresses are built (countries/regions checked).
5. Build the user: password hashed, status **Active**, `MustChangePassword = true` (the user chooses their own at first sign-in).
6. **In one transaction**: if the employee number is blank, take the account-changes lock, read the highest `EMP-nnnn` (deleted staff included, so a number is never handed out twice) and use the next; set the role (adds the `Teachers` row for Teacher); insert user, role, profile, addresses and teacher in one save.
7. Return the full user.

### 3e. Business rules and where each is enforced
| Rule | Where |
|---|---|
| Username/e-mail unique among live users | pre-check; filtered unique indexes |
| Employee number unique among live staff | pre-check; `UX_StaffProfiles_EmployeeNumber` |
| New numbers are race-safe | account-changes lock + read inside the transaction |
| Status starts Active, password temporary | service |
| Only roles you hold the permissions of | `GuardCallerHoldsRoleAsync` (so only an Admin can create an Admin) |
| No Student/Parent logins | `PrepareRoleAsync` |

### 3f. Errors
| Status | Code | When |
|---|---|---|
| 400 | `VALIDATION_ERROR` | field rules |
| 403 | `FORBIDDEN` | no `Users.Create`; role has permissions you do not hold |
| 409 | `CONFLICT` | username/e-mail/employee number taken (also from the unique index if two admins race: `A user with this username, email or employee number was just created by someone else.`); lock timeout |
| 422 | `BUSINESS_RULE_VIOLATION` | role missing/inactive/Student/Parent |

### 3g. Concurrency and retries
Two identical creates: the second is a 409. The temporary password is never returned or logged.

### 3h. Why is it built this way?
Everything for a new employee is created in one save, so there are no half-created accounts.

### Example (invented)
```json
{ "username": "meera.nair", "email": "meera@school.example", "password": "Temp#2026a", "roleName": "Teacher",
  "profile": { "fullName": "Meera Nair", "gender": "Female", "dateOfBirth": "1990-04-02", "mobile": "+971501234567",
               "joiningDate": "2026-08-15", "employeeNumber": null }, "specialization": "Mathematics" }
```

---

## 4. `PUT /api/users/{id}`

### 4a. Purpose and caller
Saves the whole edit form: account, profile, addresses and role.

### 4b. Request
`UpdateUserRequest`: `username`, `email`, `roleName`, `profile` (same rules as create), `addresses` (**`null` = keep the stored ones**), and a required `rowVersion`.

### 4c. Response
`200 OK`, the updated `UserResponse`.

### 4d. Logic, in order
1. Validator (400).
2. Load the user (404) and `Expect(rowVersion)`: stale form gives **409** `CONCURRENCY_CONFLICT`.
3. Username/e-mail/employee-number uniqueness excluding this user (409).
4. Only a **different** role goes through the role checks.
5. Copy username, e-mail and the profile; `NewVersion()`.
6. **Transaction**: if the role changed, `ApplyRoleChangeAsync` (not your own account; account-changes lock; not the last active Admin; new `SecurityStamp`; Teacher record created or removed); replace the addresses if sent; save.
7. A role change **ends the user's sessions**; otherwise only the permission cache is cleared (so the new name/e-mail show).

### 4e. Business rules
| Rule | Where |
|---|---|
| Stale edits refused | row version |
| Role change rules (self, last Admin, teaching teacher, permissions held) | `UserAccountService` |
| Teacher who still teaches cannot change role | `RefuseWhileTeachingAsync` (422) |
| A blank employee number **clears** the stored one | `Apply(...)` (see *Should fix* 1) |

### 4f. Errors
400, 403, 404, 409 (`CONFLICT` or `CONCURRENCY_CONFLICT`), 422.

### 4g. Concurrency and retries
Row version on the user; the lock is only taken when the role changes.

### 4h. Why is it built this way?
One save per form matches the screen and keeps the account and its profile consistent.

### Example (invented)
Changing yourself from Admin to Teacher: `422` `You cannot change the role of your own account. Ask another administrator, or use My Account...`

---

## 5. `PATCH /api/users/{id}/status`

### 5a. Purpose and caller
Activate, deactivate or suspend an account.

### 5b. Request
`{ "status": "Active|Inactive|Suspended", "rowVersion": "..." }`.

### 5c. Response
`200 OK`, the updated `UserResponse`.

### 5d. Logic, in order
1. Validator (400). 2. Through the account service: not yourself; lock; load; row version check (409); the last active Admin cannot be made non-Active; set status; new `SecurityStamp`; save. 3. **Sessions end at once** (for any new status, including Active).

### 5e. Business rules
Last-Admin and self rules as in the background section.

### 5f. Errors
400, 403, 404, 409, 422.

### 5g. Concurrency and retries
Row version plus the lock. A repeat with the old version gets 409.

### 5h. Why is it built this way?
Deactivation must take effect immediately, not when the token expires.

### Example (invented)
`{ "status": "Inactive", "rowVersion": "AAAAAAAAB9E=" }`

---

## 6. `POST /api/users/{id}/reset-password`

### 6a. Purpose and caller
An administrator sets a temporary password for someone who forgot theirs.

### 6b. Request
`{ "newPassword": "..." }`: same strength rule as create. **No row version.**

### 6c. Response
`204 No Content`.

### 6d. Logic, in order
1. Validator. 2. Account service: not yourself (**422** `...Use My Account...`); lock; load (404); hash the password, `MustChangePassword = true`, new stamp; save. 3. Sessions end (reason `PasswordChanged`).

### 6e. Business rules
The user must change it at the next sign-in (`MustChangePasswordMiddleware` blocks other calls until they do).

### 6f. Errors
400, 403, 404, 422.

### 6g. Concurrency and retries
Serialised by the lock. Repeating sets another temporary password.

### 6h. Why is it built this way?
The administrator knows the temporary password, so the user must replace it.

### Example (invented)
`{ "newPassword": "Temp#2026b" }`

---

## 7. `POST /api/users/{id}/unlock`

### 7a. Purpose and caller
Clears a lockout after too many wrong passwords (5 failed attempts lock the account).

### 7b. Request
`id` in the URL.

### 7c. Response
`204 No Content`.

### 7d. Logic, in order
1. Policy `Users.ChangeStatus`. 2. `AuthService.UnlockUserAsync`: load (404 `User {id} not found.`), clear `LockoutEnd` and `AccessFailedCount`, save, clear the permission cache.

### 7e. Business rules
It uses the status permission; it does not change the status. No self-protection and no session end (nothing about what the user may do changes).

### 7f. Errors
403, 404.

### 7g. Concurrency and retries
No row version, no lock. Repeating is harmless.

### 7h. Why is it built this way?
Replaces the old `POST /api/auth/unlock/{id}`.

### Example (invented)
`POST /api/users/31/unlock` → `204`.

---

## 8. `DELETE /api/users/{id}`

### 8a. Purpose and caller
Removes a login, its profile and its teacher record.

### 8b. Request
`id` in the URL.

### 8c. Response
`204 No Content`.

### 8d. Logic, in order
1. Through the account service: not yourself; lock; load (404); **last active Admin cannot be deleted**.
2. If the user is a teacher: refuse while they still teach (**422** `Cannot delete this account: the teacher still teaches...`), else soft-delete the teacher row.
3. Soft-delete the profile and the user; new stamp; save; end all sessions.

### 8e. Business rules
| Rule | Where |
|---|---|
| Soft delete; the username can be reused | filtered unique indexes |
| A deleted user stays named in history (uploads, audit) | repositories use `IgnoreQueryFilters` for names |
| An employee number is released by delete in the index but never **generated** again | `GetMaxEmployeeSerialAsync` counts deleted staff |

### 8f. Errors
403, 404, 422.

### 8g. Concurrency and retries
No row version, but the lock serialises account changes. A repeat returns 404.

### 8h. Why is it built this way?
Soft delete keeps who-did-what readable in old records.

### Example (invented)
`DELETE /api/users/31` while Meera still teaches Mathematics: `422` `Cannot delete this account: the teacher still teaches 1 subject(s) and is class teacher of 1 class(es). Remove the assignments on the Teachers page first.`

---

## Doubts and possible problems

### Blocker
None found.

### Should fix
1. **A blank employee number on edit erases the stored one.** `UpdateAsync` copies the profile as sent, so an API call that sends `employeeNumber: null` clears it (the form always sends the current value, so only direct callers hit it). New numbers are generated on create only. Either ignore a blank on edit or generate one.
2. **Reset password and delete have no row version**, so a stale screen can reset or delete a user that changed meanwhile. They are still protected by the lock and the self/last-Admin rules.
3. **Date of birth "in the past" uses the UTC date.** Minor.
4. **Unlock has no audit trail beyond the generic audit row**, and anyone with `Users.ChangeStatus` can unlock themselves. Acceptable, but confirm.
5. **Permission-held rule can block a legitimate Admin-lite.** A user who holds `Users.Create` but not every permission of a role cannot create that role. That is intended (no privilege escalation). Check that each non-Admin role that holds `Users.Create` can create the roles you expect.

### Question
1. **When Student and Parent logins arrive**, will they be created here or from the student/parent screens? The service refuses the roles today with the message "come with the portal".
2. **Should the temporary password be shown anywhere?** It is typed by the administrator, never generated or returned.
3. **Should deactivating a teacher also move their subjects/class to unassigned?** Today the assignments stay (see `teachers.md`).
4. **A typed employee number equal to a deleted staff member's is accepted** (the index ignores deleted rows) while the generator never reuses deleted numbers. Is reusing intended?
5. **No paging or search on the list** (same as the other master lists).
6. **Who may create an Admin?** Only someone holding the whole permission catalog, in practice an Admin. Confirm this is the rule you want.
