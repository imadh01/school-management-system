# Roles and Permissions controllers: what each endpoint actually does

Sources: `backend/SchoolManagement.API/Controllers/RolesController.cs` (prefix `api/roles`) and `PermissionsController.cs` (prefix `api/permissions`).
Written for a developer with about two years of experience who is learning this codebase.

## How this file was made

- **Everything here was read from the code**: both controllers, `RoleService`, `RoleRepository`, the four validators, the DTOs, `PermissionDependencies`, `RoleNames`, `PermissionCacheService` (only the line that filters inactive roles), and the names of the tests.
- **I did not run the API or the database.** There is no SQL Server in my environment. So **every example below is invented**: shapes, field names, error codes and messages come from the code, but ids and names are made up.
- Tests exist: `RolesApiTests` (create and read back, system names in any case, duplicate name, missing dependency, unknown permission, cannot grant what you do not hold, cannot change your own role, Admin role protected, system role rename refused, two admins editing gives 409, removal takes effect on the next request, deactivated role grants nothing, audit log, the catalog), `RoleMatrixSnapshotTests` and `ExpectedRoleMatrix` (the permissions of the six system roles are pinned) and `PermissionCatalogTests`. I did not run them in this session.
- **Not verified** marks anything I could not confirm from the code alone.
- The doubts I noticed are at the end, labelled **Blocker**, **Should fix** or **Question**.

## The seven endpoints at a glance

| # | Method + URL | Permission needed | Called by (React) |
|---|---|---|---|
| 1 | `GET /api/roles` | `Roles.View` | `RolesPage`, role drop-down on the user form |
| 2 | `GET /api/roles/{id}` | `Roles.View` | role detail / permission matrix |
| 3 | `POST /api/roles` | `Roles.Create` | role form |
| 4 | `PUT /api/roles/{id}` | `Roles.Edit` | role form |
| 5 | `PUT /api/roles/{id}/permissions` | `Roles.Edit` | permission matrix |
| 6 | `PATCH /api/roles/{id}/status` | `Roles.Edit` | roles list |
| 7 | `GET /api/permissions` | `Roles.View` | permission matrix |

There is **no delete**: roles are only deactivated.

## Background you need before the endpoints

### The same request pipeline as Auth

Correlation id, Serilog, `GlobalExceptionMiddleware`, JWT, `MustChangePasswordMiddleware`, then the permission policy. See `auth.md`.

### The permission model

- A **permission** is a string `Module.Action`, for example `Students.Edit`. The full list ("catalog") is **in code** (`Permissions.Definitions`) and mirrored in the `Permissions` table by migrations.
- A **role** is a named set of permissions. Six **system roles** are seeded: `Admin`, `Supervisor`, `Clerk`, `Teacher`, `Student`, `Parent`. You can add custom roles.
- **Admin holds the whole catalog in code**, not as rows. Its permission list is never stored or edited, so a new permission is automatically Admin's.
- A user has **one role** (service rule, see `users.md`). The user's permissions are the role's, read through a **permission cache** that is cleared whenever the role changes.
- A role that is **inactive grants nothing** (the cache query only reads active roles).

### Dependencies between permissions

A permission can require others, so a role never gets a broken screen (`PermissionDependencies`): every non-read action needs its module's **full View** (for example `Students.Edit` needs `Students.View`), and some need another module's View (for example `Subjects.Create` needs `ClassSections.View`; `Teachers.Assign` needs `Subjects.View` and `ClassSections.View`). A scoped read (`ViewScoped`) is not enough for a write, except where the service does its own scope check (`Attendance.Mark`).

### The six guards in `RoleService`

1. **Own role**: you cannot change a role you hold yourself (permissions, name or status). **403** `You cannot change the 'Supervisor' role because you hold it yourself.`
2. **No escalation**: every permission you add or remove must be one you hold. **403** `You cannot add or remove permissions you do not hold yourself: X, Y.`
3. **Admin role**: its permissions cannot be edited and it cannot be deactivated (422).
4. **System roles**: cannot be renamed; no custom role may take a system name in any letter case (422).
5. **Dependencies**: **422** `Some permissions need others granted with them: Students.Edit needs Students.View.`
6. **Concurrency**: every edit sends the `rowVersion`; a stale one gives **409** `CONCURRENCY_CONFLICT`. A permissions-only change still touches the role row, so the check always applies.

After a change is saved, the cache is cleared **for every user holding the role**, so the change takes effect on their next request. The cache lives in the API process, so this assumes **one API instance** (the code says so).

### Audit

`AuditableEntitySaveChangesInterceptor` writes audit rows; a test checks that permission changes appear in the audit log.

---

## 1. `GET /api/roles`

### 1a. Purpose and caller
The roles list and the role drop-down.

### 1b. Request
None.

### 1c. Response
`200 OK`, array of `RoleSummaryResponse`: `id, name, description, isSystem, isActive, hasAllPermissions, userCount, permissionCount`.

### 1d. Logic, in order
1. Policy `Roles.View`. 2. One query returns each role with its user count (non-deleted users) and permission count. 3. For `Admin`, `hasAllPermissions` is true and `permissionCount` is the size of the catalog.

### 1e. Business rules
Admin's counts come from the code, not the table.

### 1f. Errors
403.

### 1g. Concurrency and retries
Read only.

### 1h. Why is it built this way?
Summary only; the permission names are loaded by endpoint 2.

### Example (invented)
```json
[ { "id": 1, "name": "Admin", "description": "Full access", "isSystem": true, "isActive": true,
    "hasAllPermissions": true, "userCount": 2, "permissionCount": 98 } ]
```

---

## 2. `GET /api/roles/{id}`

### 2a. Purpose and caller
One role with its permission names, for the matrix.

### 2b. Request
`id` in the URL.

### 2c. Response
`200 OK`, `RoleResponse`: `id, name, description, isSystem, isActive, hasAllPermissions, permissions[], rowVersion`. For Admin, `permissions` is the whole catalog (sorted).

### 2d. Logic, in order
1. Policy. 2. Load with permissions; missing gives **404** `Role {id} could not be found.` 3. Map; permissions sorted by name.

### 2e. Business rules
None beyond the Admin special case.

### 2f. Errors
403, 404.

### 2g. Concurrency and retries
The `rowVersion` must be sent back by edits.

### 2h. Why is it built this way?
One call gives the matrix everything it needs.

### Example (invented)
`GET /api/roles/99` → `404` `Role 99 could not be found.`

---

## 3. `POST /api/roles`

### 3a. Purpose and caller
Creates a custom role.

### 3b. Request
`{ "name", "description", "permissions": [...] }`

| Field | Rule |
|---|---|
| `name` | required, max 30, letters/digits/spaces/hyphens, starts with a letter |
| `description` | optional, max 200 |
| `permissions` | required list (may be empty) |

### 3c. Response
`201 Created`, `Location: api/roles/{id}`, `RoleResponse`.

### 3d. Logic, in order
1. Validator (400).
2. Normalise the name (trim, collapse inner spaces).
3. Name must not be a system name (**422** `'clerk' is reserved for a system role.`, any case) and must be unused (**409** `A role named 'X' already exists.`).
4. Permissions: unknown names give **422** `Unknown permission(s): ...`; names missing from the table give **422** (a migration was not applied); dependencies checked (422); you must hold every one (403).
5. Insert as non-system, active. Nobody holds it yet, so no cache to clear.

### 3e. Business rules and where each is enforced
| Rule | Where |
|---|---|
| Name unique | service pre-check; unique index on `Roles.Name`; a race returns 409 `A role with this name was just created by someone else.` |
| No escalation | `GuardCallerHoldsAsync` |
| Custom roles are never system roles | service |

### 3f. Errors
400, 403, 409, 422.

### 3g. Concurrency and retries
Two identical creates: the second is 409.

### 3h. Why is it built this way?
A creator can never hand out more than they hold.

### Example (invented)
```json
{ "name": "Exam Cell", "description": "Marks and hall tickets", "permissions": ["Students.View", "Subjects.View"] }
```

---

## 4. `PUT /api/roles/{id}`

### 4a. Purpose and caller
Edits a role's name and description.

### 4b. Request
`{ "name", "description", "rowVersion" }` (same name/description rules as create; row version required).

### 4c. Response
`200 OK`, the updated `RoleResponse`.

### 4d. Logic, in order
1. Validator. 2. Load (404). 3. **Own role** guard (403). 4. System role: the name must be unchanged (**422** `'Teacher' is a system role and cannot be renamed.`); custom role with a new name: reserved/duplicate checks. 5. Row version check (409). 6. Save, clear the cache of users in the role.

### 4e. Business rules
Description of a system role can be changed (only the name is locked), except by a user who holds that role.

### 4f. Errors
400, 403, 404, 409, 422.

### 4g. Concurrency and retries
Row version. A stale form gets 409 and must reload.

### 4h. Why is it built this way?
Code looks up system roles by name, so their names cannot move.

### Example (invented)
`{ "name": "Exam Cell", "description": "Marks, results and hall tickets", "rowVersion": "AAAAAAAAB9E=" }`

---

## 5. `PUT /api/roles/{id}/permissions`

### 5a. Purpose and caller
Replaces the **whole** permission set of a role. Called by the permission matrix.

### 5b. Request
`{ "permissions": [...], "rowVersion": "..." }`

### 5c. Response
`200 OK`, the updated `RoleResponse`.

### 5d. Logic, in order
1. Validator. 2. Load (404). 3. **Own role** guard (403). 4. Admin role: **422** `The Admin role always has every permission; its permissions cannot be changed.`
5. Validate the requested names (unknown 422; not in table 422) and dependencies (422).
6. Work out what **changed** (added or removed) and require that you hold each of them (403). You may keep permissions you do not hold; you cannot add or remove them.
7. Row version check (409). Remove and add rows, save, clear the cache of every user with the role.

### 5e. Business rules
| Rule | Where |
|---|---|
| Whole-set replacement | service |
| Removing a permission that another kept permission needs is refused | `FindMissing` over the final set |
| Takes effect on the user's next request | cache invalidation (test) |

### 5f. Errors
400, 403, 404, 409, 422.

### 5g. Concurrency and retries
Row version. Two admins editing the same role: the second gets 409 (test).

### 5h. Why is it built this way?
Replacing the full set matches the matrix screen and keeps dependencies checkable in one place.

### Example (invented)
`{ "permissions": ["Students.View", "Students.Edit"], "rowVersion": "AAAAAAAAB9E=" }`

---

## 6. `PATCH /api/roles/{id}/status`

### 6a. Purpose and caller
Activates or deactivates a role.

### 6b. Request
`{ "isActive": true|false, "rowVersion": "..." }`

### 6c. Response
`200 OK`, the updated `RoleResponse`.

### 6d. Logic, in order
1. Validator. 2. Load (404). 3. **Own role** guard (403). 4. Admin cannot be deactivated (**422** `The Admin role cannot be deactivated.`). 5. Switching a role on or off counts as changing **all** its permissions, so you must hold them all (403). 6. Row version (409). 7. Save, clear the cache of its users.

### 6e. Business rules
| Rule | Where |
|---|---|
| A deactivated role grants nothing | `PermissionCacheService` (test: `A_deactivated_role_grants_nothing_until_reactivated`) |
| Users keep the role but lose all access through it | no rule stops it (see *Should fix* 1) |
| A role cannot be given to a new user while inactive | `PrepareRoleAsync` in the users flow (422) |

### 6f. Errors
400, 403, 404, 409, 422.

### 6g. Concurrency and retries
Row version.

### 6h. Why is it built this way?
Deleting roles would orphan users and history; deactivating is reversible.

### Example (invented)
`{ "isActive": false, "rowVersion": "AAAAAAAAB9E=" }`

---

## 7. `GET /api/permissions`

### 7a. Purpose and caller
The catalog for the permission matrix.

### 7b. Request
None.

### 7c. Response
`200 OK`, array of modules: `{ module, permissions: [{ name, description, requires: [[...]] }] }`. Each entry in `requires` is a list of alternatives, any one of which satisfies it (for example `["Attendance.View", "Attendance.ViewScoped"]`).

### 7d. Logic, in order
1. Policy `Roles.View`. 2. Group the code catalog by module and attach each permission's requirements. No database access.

### 7e. Business rules
Permissions are defined **in code**; this endpoint is read-only.

### 7f. Errors
403.

### 7g. Concurrency and retries
Read only.

### 7h. Why is it built this way?
The frontend greys out or auto-selects dependencies from this list.

### Example (invented)
```json
[ { "module": "Students", "permissions": [ { "name": "Students.Edit", "description": "Edit students", "requires": [["Students.View"]] } ] } ]
```

---

## Doubts and possible problems

### Blocker
None found.

### Should fix
1. **Deactivating a role that users hold is allowed silently.** Those users keep their role name but have no permissions, so every screen answers 403 (and `/auth/me` shows no permissions). There is no warning with the user count and no block. The list already knows `userCount`; the screen should confirm, or the API should refuse.
2. **The permission cache is in one process.** With two API instances (a load balancer), a role change on one would not reach the other until the cache expires. The code says it assumes one instance. Fine for the demo; note it for production.
3. **Student and Parent roles can be given permissions** even though no one can hold them yet. Harmless, but the matrix suggests otherwise.

### Question
1. **Can a non-Admin role manager ever edit Admin-level things?** By design no (the Admin role is locked, and you cannot change a role you hold). That means the only way to edit the Supervisor role is by an Admin or by another role that holds all its permissions. Confirm that is what you want.
2. **Should role changes be limited to a short list of people** (for example only `Roles.Edit` holders who are Admins)? Today any holder of `Roles.Edit` can edit any role they do not hold, within the "no escalation" rule.
3. **Role names are limited to 30 characters and English letters** by the validator pattern. Is an Arabic role name needed?
4. **New permissions in a future release** reach Admin automatically but no other role. The pinned role matrix test (`RoleMatrixSnapshotTests`) is the reminder to decide each time. Is that process acceptable?
