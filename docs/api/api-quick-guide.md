# API quick guide

A short guide for a developer who is learning this API: how to run it, every endpoint, what errors look like, five worked requests, and the rules that are easy to miss.

**How this guide was made.** Everything below was read from the code (controllers, services, DTOs, middleware, the frontend `services/` folder). I did **not** run the API while writing it, so the example *values* are invented and only the *shapes* and *messages* come from the code. Anything I could not confirm is marked **⚠ Not verified**; the full list is in section 6.

Contents: [1. Run, Swagger, sign in](#1-run-the-api-open-swagger-sign-in-and-authorize) · [2. Endpoints](#2-every-endpoint) · [3. Error responses](#3-standard-error-responses) · [4. Worked examples](#4-five-worked-examples-http) · [5. Easy-to-miss rules](#5-rules-that-are-easy-to-miss) · [6. Not verified](#6-what-i-could-not-verify)

---

## 1. Run the API, open Swagger, sign in and authorize

Swagger is only switched on when the API runs in the **Development** environment (`Program.cs`: `if (app.Environment.IsDevelopment()) { UseSwagger(); UseSwaggerUI(); }`). Both launch profiles set `ASPNETCORE_ENVIRONMENT=Development`.

**Once, before the first run**

1. Install the .NET 8 SDK and have SQL Server running (the project uses SQL Server Express). Install the EF tool if you do not have it: `dotnet tool install --global dotnet-ef`.
2. Put the two required secrets in *user secrets* (they are deliberately empty in `appsettings.json`; the project's secrets id is `schoolmanagement-api-secrets`). Run from the repository root:

   ```
   dotnet user-secrets set "ConnectionStrings:DefaultConnection" "Server=.\SQLEXPRESS;Database=<your database>;Trusted_Connection=True;TrustServerCertificate=True" --project backend/SchoolManagement.API
   dotnet user-secrets set "Jwt:SigningKey" "<a long random string, 32+ characters>" --project backend/SchoolManagement.API
   ```

   The connection string above is an example: use your own server and database name. The 32-character advice for the signing key is my recommendation, not a rule I found in the code (**⚠ Not verified**).
3. Create or update the database. The API does **not** apply migrations when it starts (I found no `Migrate` call in `Program.cs`), so run:

   ```
   dotnet ef database update --project backend/SchoolManagement.Infrastructure --startup-project backend/SchoolManagement.API
   ```
4. Trust the HTTPS development certificate once: `dotnet dev-certs https --trust`.

**Every time**

1. Start the API from the repository root:

   ```
   dotnet run --project backend/SchoolManagement.API --launch-profile https
   ```

   It listens on `https://localhost:7100` and `http://localhost:5100`. (Visual Studio: pick the **https** profile and press F5. The React dev server proxies `/api` to `https://localhost:7100`.)
2. Open Swagger at **https://localhost:7100/swagger**. The launch profiles open it for you.
3. Find `POST /api/auth/login`, click **Try it out**, and send:

   ```json
   { "usernameOrEmail": "admin", "password": "Admin@123" }
   ```

   `admin` / `Admin@123` is what the sample file `backend/SchoolManagement.API/E2-Users-Settings.http` and the automated tests use. Use the administrator account of **your** database if it differs (**⚠ Not verified**: I could not find where the first administrator is created in the code, so I do not know how your database got its account).
4. In the response, copy the value of `token` (the long `eyJ...` text). Do not copy the quotes.
5. Click the green **Authorize** button at the top of the page. Paste the token into the box and click **Authorize**, then **Close**. Paste only the token: Swagger adds the word `Bearer` itself.
6. Now every request you try from Swagger carries the token. Try `GET /api/auth/me`: it returns your roles and permissions.
7. The token lasts **15 minutes** (`Jwt:ExpiryMinutes`). When requests start returning `401`, sign in again and re-authorize. If the response shows `"mustChangePassword": true`, change the password first (`POST /api/auth/change-password`): until you do, every other endpoint answers `403 PASSWORD_CHANGE_REQUIRED`.

**Two endpoints behave differently.** `POST /api/auth/refresh` and `POST /api/auth/logout` do not use the bearer token. They use the refresh **cookie** that login set, and they need the header `X-Requested-With: eschool`. Swagger shows that header box for you on those two endpoints.

---

## 2. Every endpoint

92 endpoints in 15 controllers. How to read the **Permission needed** column:

- A name such as `Students.Edit` is a permission; the policy is built from it. Admin holds all permissions.
- `A or B` means any one of them is enough. `A **and** B` means both are needed.
- **Signed in only** means any signed-in user.
- Permissions are only part of the story: some rules are checked inside the service (for example "a teacher may mark attendance only for their own class"). See section 5.

The **Frontend page** column was found by searching the React code for the call (`frontend/school-management-ui/src`). "— (not used by the UI yet)" means I found no caller. Page names are the ones in the sidebar; "form" means the add/edit page.


#### Admissions

| Method | URL | What it does | Permission needed | Frontend page |
|---|---|---|---|---|
| `GET` | `/api/admissions/parent-search` | Searches existing parents by name or mobile for the "pick an existing parent" box (2+ letters or 3+ digits, max 10 results, with their linked children). | Parents.View **and** (Admissions.Create or Admissions.Edit); the second check is in the controller | Admission form (ExistingParentPicker) |
| `GET` | `/api/admissions` | Lists every application. | Admissions.View | Admissions list |
| `GET` | `/api/admissions/{id}` | Returns one application with its guardians. | Admissions.View | Admission detail, Admission form (edit) |
| `POST` | `/api/admissions` | Registers a new application (status Registered). | Admissions.Create | Admission form (new) |
| `PUT` | `/api/admissions/{id}` | Edits an application that is still Registered or Admitted. | Admissions.Edit | Admission form (edit) |
| `POST` | `/api/admissions/{id}/confirm-admission` | Moves a Registered application to Admitted and records the fee and extra details. | Admissions.Edit | Admission detail |
| `GET` | `/api/admissions/{id}/guardian-matches` | Suggests, for each guardian, which existing parent to reuse at enrolment. | Admissions.Edit | Admission detail (Enroll dialog) |
| `POST` | `/api/admissions/{id}/enroll` | Enrols an Admitted application: creates the student and parents in one transaction and marks it Enrolled. | Admissions.Enroll | Admission detail (Enroll dialog) |
| `POST` | `/api/admissions/{id}/reject` | Rejects a Registered or Admitted application with a reason. | Admissions.Edit | Admission detail |
| `DELETE` | `/api/admissions/{id}` | Soft-deletes an application (not allowed once Enrolled). | Admissions.Delete | Admissions list, Admission detail |

#### Attendance

| Method | URL | What it does | Permission needed | Frontend page |
|---|---|---|---|---|
| `GET` | `/api/attendance/roster` | Returns the roster of a class for a date (optionally one subject) with saved statuses. | Attendance.View or Attendance.ViewScoped | Attendance |
| `PUT` | `/api/attendance` | Saves the whole roster for a class, date and optional subject in one transaction. | Attendance.Manage or Attendance.Mark | Attendance |
| `GET` | `/api/attendance/students/{studentId}` | Returns one student's attendance history and totals. | Attendance.View or Attendance.ViewScoped | — (not used by the UI yet) |
| `GET` | `/api/attendance/summary` | Returns per-student totals and percentages for a class over a date range. | Attendance.View or Attendance.ViewScoped | — (not used by the UI yet) |

#### Auth (sign-in and session)

| Method | URL | What it does | Permission needed | Frontend page |
|---|---|---|---|---|
| `POST` | `/api/auth/login` | Checks username/e-mail and password; returns the access token and sets the refresh cookie. | None (anonymous) | Login |
| `POST` | `/api/auth/refresh` | Swaps the refresh cookie for a new access token (rotates the cookie). | None (anonymous) + header `X-Requested-With: eschool` | Automatic (session.ts, on page load and expiry) |
| `POST` | `/api/auth/logout` | Ends this device's session and clears the cookie (always 204). | None (anonymous) + header `X-Requested-With: eschool` | Sidebar (Logout) |
| `POST` | `/api/auth/logout-all` | Ends every session of the current user on every device. | Signed in only | — (no button yet) |
| `GET` | `/api/auth/me` | Returns the signed-in user with roles and the full permission list. | Signed in only | Automatic after sign-in (menus and buttons) |
| `POST` | `/api/auth/change-password` | Changes your own password and ends your other sessions. | Signed in only | Change password (forced) page |

#### Class sections

| Method | URL | What it does | Permission needed | Frontend page |
|---|---|---|---|---|
| `GET` | `/api/class-sections` | Lists all class sections. | ClassSections.View | Classes & Sections, Subjects, Attendance, Admission form, Student edit, Assign-subjects dialog |
| `GET` | `/api/class-sections/{id}` | Returns one class section. | ClassSections.View | Class section detail, Class section form (edit) |
| `POST` | `/api/class-sections` | Creates a class section. | ClassSections.Create | Class section form (new) |
| `PUT` | `/api/class-sections/{id}` | Edits a class section. | ClassSections.Edit | Class section form (edit) |
| `PATCH` | `/api/class-sections/{id}/status` | Marks a class section Active or Inactive. | ClassSections.Edit | Classes & Sections list, Class section detail |
| `DELETE` | `/api/class-sections/{id}` | Deletes a class section that has no students or subjects. | ClassSections.Delete | Classes & Sections list, Class section detail |

#### Countries

| Method | URL | What it does | Permission needed | Frontend page |
|---|---|---|---|---|
| `GET` | `/api/countries` | Lists countries for address forms. | Signed in only | User form, My Account, Parent form, Student edit, Admission form, Settings (country pickers) |

#### Documents

| Method | URL | What it does | Permission needed | Frontend page |
|---|---|---|---|---|
| `GET` | `/api/documents/options` | Returns the document types and the upload size limit. | Documents.View | Documents tab (student/application), Admission form |
| `GET` | `/api/documents` | Lists documents of all students and applicants (filters: status, type, expiry, owner). | Documents.View | Documents |
| `GET` | `/api/students/{studentId}/documents` | Lists one student's documents. | Documents.View | Student detail (Documents tab) |
| `GET` | `/api/admissions/{admissionId}/documents` | Lists one application's documents. | Documents.View | Admission detail (Documents tab) |
| `GET` | `/api/documents/{id}/file` | Downloads the file (sensitive types also need Students.ViewSensitive). | Documents.View | Documents (download link) |
| `POST` | `/api/students/{studentId}/documents` | Uploads a PDF/JPG/PNG to a student (multipart form). | Documents.Upload | Student detail (Documents tab) |
| `POST` | `/api/admissions/{admissionId}/documents` | Uploads a PDF/JPG/PNG to an application (multipart form). | Documents.Upload | Admission detail, Admission form |
| `POST` | `/api/documents/{id}/verify` | Marks a Pending document Verified (not your own upload). | Documents.Verify | Documents |
| `POST` | `/api/documents/{id}/reject` | Rejects a Pending document with a reason (not your own upload). | Documents.Verify | Documents |
| `POST` | `/api/documents/{id}/unverify` | Puts a Verified document back to Pending. | Documents.Verify | Documents |
| `DELETE` | `/api/documents/{id}` | Deletes a document (a Verified one must be unverified first). | Documents.Delete | Documents |

#### My account

| Method | URL | What it does | Permission needed | Frontend page |
|---|---|---|---|---|
| `GET` | `/api/me/profile` | Returns your own staff profile and e-mail. | Signed in only | My Account |
| `PUT` | `/api/me/profile` | Updates your own profile, e-mail and addresses. | Signed in only | My Account |

#### Parents

| Method | URL | What it does | Permission needed | Frontend page |
|---|---|---|---|---|
| `GET` | `/api/parents` | Lists parents (full details, or name and mobile only when scoped). | Parents.View or Parents.ViewScoped | Parents |
| `GET` | `/api/parents/{id}` | Returns one parent (full or scoped version). | Parents.View or Parents.ViewScoped | Parent detail, Parent form (edit) |
| `POST` | `/api/parents` | Creates a parent. | Parents.Create | Parent form (new) |
| `PUT` | `/api/parents/{id}` | Edits a parent. | Parents.Edit | Parent form (edit) |
| `DELETE` | `/api/parents/{id}` | Soft-deletes a parent. | Parents.Delete | Parents list, Parent detail |
| `GET` | `/api/parents/{id}/students` | Lists a parent's linked children. | Parents.View or Parents.ViewScoped | Parent detail |
| `POST` | `/api/parents/{id}/students` | Links a student to this parent. | Parents.Edit | Parent detail (Link child dialog) |
| `DELETE` | `/api/parents/{id}/students/{studentId}` | Removes the link between a parent and a student. | Parents.Edit | Parent detail |
| `POST` | `/api/parents/{id}/students/{studentId}/primary` | Makes this parent the primary contact of the student. | Parents.Edit | Parent detail |

#### Permissions catalog

| Method | URL | What it does | Permission needed | Frontend page |
|---|---|---|---|---|
| `GET` | `/api/permissions` | Returns the permission catalog grouped by module, with what each permission requires. | Roles.View | Role form, Role detail |

#### Roles

| Method | URL | What it does | Permission needed | Frontend page |
|---|---|---|---|---|
| `GET` | `/api/roles` | Lists roles with user and permission counts. | Roles.View | Roles & Permissions list; User form (role dropdown) |
| `GET` | `/api/roles/{id}` | Returns one role with its permissions and row version. | Roles.View | Role detail, Role form (edit) |
| `POST` | `/api/roles` | Creates a custom role with a set of permissions. | Roles.Create | Role form (new) |
| `PUT` | `/api/roles/{id}` | Renames a role / edits its description (needs the row version). | Roles.Edit | Role form (edit) |
| `PUT` | `/api/roles/{id}/permissions` | Replaces the role's whole permission set (needs the row version). | Roles.Edit | Role form (edit) |
| `PATCH` | `/api/roles/{id}/status` | Activates or deactivates a role (needs the row version). | Roles.Edit | Role status dialog |

#### Settings

| Method | URL | What it does | Permission needed | Frontend page |
|---|---|---|---|---|
| `GET` | `/api/settings/school` | Returns the school settings with the row version. | Settings.View | Settings |
| `PUT` | `/api/settings/school` | Saves the school settings (needs the row version). | Settings.Edit | Settings |
| `GET` | `/api/settings/time-zones` | Lists time zones for the Settings picker. | Settings.View | Settings |
| `GET` | `/api/settings/basics` | Returns school name, country and today's date in the school's time zone. | Signed in only | Top bar (school name), address forms, Attendance (today) |

#### Students

| Method | URL | What it does | Permission needed | Frontend page |
|---|---|---|---|---|
| `GET` | `/api/students` | Lists students (full rows, or reduced rows when scoped). | Students.View or Students.ViewScoped | Students, Class section detail, Link-child dialog |
| `GET` | `/api/students/{id}` | Returns one student (full, or reduced when scoped); the read is audited. | Students.View or Students.ViewScoped | Student detail, Student edit |
| `POST` | `/api/students` | Creates a student directly (students normally arrive through enrolment). | Students.Create | — (no screen; students are created by Enroll) |
| `PUT` | `/api/students/{id}` | Edits a student: personal data, class move, status, health, pickup persons. | Students.Edit | Student edit |
| `PUT` | `/api/students/{id}/identity` | Sets Aadhaar / passport / visa numbers. | Students.Edit **and** Students.ViewSensitive | Student edit (Identity section) |
| `GET` | `/api/students/{id}/enrollments` | Returns the student's academic history, newest first. | Students.View or Students.ViewScoped | Student detail (History) |
| `GET` | `/api/students/{id}/guardians` | Lists the student's parents/guardians (full or contact-only). | Students.View or Students.ViewScoped | — (not used by the UI yet) |
| `POST` | `/api/students/{id}/guardians` | Links a parent to this student. | Parents.Edit | — (not used by the UI yet; the UI links from the Parent page) |
| `DELETE` | `/api/students/{id}/guardians/{parentId}` | Unlinks a parent from this student. | Parents.Edit | — (not used by the UI yet; the UI unlinks from the Parent page) |
| `DELETE` | `/api/students/{id}` | Soft-deletes a student. | Students.Delete | Students list, Student detail |

#### Subjects

| Method | URL | What it does | Permission needed | Frontend page |
|---|---|---|---|---|
| `GET` | `/api/subjects` | Lists subjects. | Subjects.View | Subjects, Attendance, Class section detail, Assign-subjects dialog |
| `GET` | `/api/subjects/{id}` | Returns one subject. | Subjects.View | Subject form (edit) |
| `POST` | `/api/subjects` | Creates a subject. | Subjects.Create | Subject form (new) |
| `PUT` | `/api/subjects/{id}` | Edits a subject. | Subjects.Edit | Subject form (edit) |
| `DELETE` | `/api/subjects/{id}` | Deletes a subject. | Subjects.Delete | Subjects list |

#### Teachers

| Method | URL | What it does | Permission needed | Frontend page |
|---|---|---|---|---|
| `GET` | `/api/teachers` | Lists teachers. | Teachers.View | Teachers |
| `GET` | `/api/teachers/{id}` | Returns one teacher. | Teachers.View | Teacher detail, Teacher form (edit) |
| `PUT` | `/api/teachers/{id}` | Edits the teacher's profile and specialization. | Teachers.Edit | Teacher form (edit) |
| `DELETE` | `/api/teachers/{id}` | Deletes the teacher and their login (refused while they teach); ends their sessions. | Teachers.Delete | Teachers list, Teacher detail |
| `GET` | `/api/teachers/{id}/assignments` | Lists the teacher's subjects and class-teacher roles. | Teachers.View | Teacher detail |
| `POST` | `/api/teachers/{id}/subjects` | Assigns subjects of a class to the teacher. | Teachers.Assign | Assign-subjects dialog |
| `DELETE` | `/api/teachers/{id}/subjects/{subjectId}` | Removes one subject assignment. | Teachers.Assign | Teacher detail |
| `PUT` | `/api/teachers/{id}/class-teacher` | Makes the teacher class teacher of a class section. | Teachers.Assign | Teacher detail |
| `DELETE` | `/api/teachers/{id}/class-teacher/{classSectionId}` | Removes the class-teacher role for a section. | Teachers.Assign | Teacher detail |

#### Users

| Method | URL | What it does | Permission needed | Frontend page |
|---|---|---|---|---|
| `GET` | `/api/users` | Lists staff accounts. | Users.View | Users & Staff |
| `GET` | `/api/users/{id}` | Returns one account with profile, addresses and row version. | Users.View | User detail, User form (edit) |
| `POST` | `/api/users` | Creates a staff account with its profile (temporary password, role). | Users.Create | User form (new); Teachers "New teacher" redirects here |
| `PUT` | `/api/users/{id}` | Edits account, profile, addresses and role in one save (needs the row version). | Users.Edit | User form (edit) |
| `PATCH` | `/api/users/{id}/status` | Sets Active / Inactive / Suspended; anything but Active ends the user's sessions. | Users.ChangeStatus | User detail (status dialog) |
| `POST` | `/api/users/{id}/reset-password` | Sets a temporary password the user must change at next sign-in; ends their sessions. | Users.ResetPassword | User detail (reset dialog) |
| `POST` | `/api/users/{id}/unlock` | Clears a lockout after too many failed sign-ins. | Users.ChangeStatus | User detail |
| `DELETE` | `/api/users/{id}` | Soft-deletes the account, its profile and teacher record; ends sessions. | Users.Delete | User detail (delete dialog) |

---

## 3. Standard error responses

Every error uses the same JSON envelope (`ApiErrorResponse`). The property names are camelCase on the wire:

| Field | Meaning |
|---|---|
| `success` | Always `false` for an error. |
| `message` | A sentence a person can read. |
| `errorCode` | A stable code for programs (see below). |
| `errors` | Only for validation errors: field name → list of messages. |
| `traceId` | Copy of the `X-Correlation-Id` response header. Send your own `X-Correlation-Id` request header to choose it; otherwise a GUID is generated. Give this id to whoever reads the logs. |

Who produces what: validation errors are returned by the controllers; `NotFound`, `Conflict`, `Forbidden`, business-rule and concurrency errors are thrown by services and turned into JSON by `GlobalExceptionMiddleware`; 401 and 403 from the authentication layer are written by handlers in `Program.cs`. All of them use the same envelope.

### 400 Bad Request: validation failed

Example: `POST /api/auth/login` with an empty body `{}`.

```json
{
  "success": false,
  "message": "Validation failed.",
  "errorCode": "VALIDATION_ERROR",
  "errors": {
    "UsernameOrEmail": [ "'Username Or Email' must not be empty." ],
    "Password": [ "'Password' must not be empty." ]
  },
  "traceId": "0d3b6a52-5c1e-4a3f-9f55-6e0e3d1c9a10"
}
```

- The keys in `errors` are the C# property names (`UsernameOrEmail`), not camelCase, and nested ones use a path such as `Profile.Gender` or `Addresses[0].PostalCode`. This follows from `ValidationResultExtensions.ToErrorDictionary()`.
- The wording of the messages is FluentValidation's default text for `NotEmpty()`. I did not run it, so check the exact text (**⚠ Not verified**).
- A request that cannot even be read (broken JSON, a text where a number is expected) is rejected by ASP.NET Core before our code runs. I found no custom handler for that, so it probably returns ASP.NET's default `application/problem+json` body instead of our envelope (**⚠ Not verified**).

### 401 Unauthorized

No token, an expired or bad token, or a token whose account has since changed (see "sessions end", section 5):

```json
{
  "success": false,
  "message": "Authentication required.",
  "errorCode": "UNAUTHENTICATED",
  "traceId": "6a0c2f8e-0a8b-4f55-8c2a-0f1b6f1d7a44"
}
```

A wrong password at login gives a different code:

```json
{
  "success": false,
  "message": "Invalid username/email or password.",
  "errorCode": "INVALID_CREDENTIALS",
  "traceId": "b7d2c1a4-2f6e-4c55-9d4c-5e8a1a0c3b21"
}
```

And a dead refresh cookie (`POST /api/auth/refresh`): `"message": "Your session has ended. Please sign in again."`, `"errorCode": "REFRESH_TOKEN_INVALID"`.

### 403 Forbidden

You are signed in but do not hold the permission:

```json
{
  "success": false,
  "message": "You do not have permission to perform this action.",
  "errorCode": "FORBIDDEN",
  "traceId": "1c9e7a30-8d44-4d0a-b1f4-2a7f0b6e9c52"
}
```

Services can also throw `403` with their own message and the same `FORBIDDEN` code, for example `"You cannot change the 'ZT Reception' role because you hold it yourself."` or `"Choosing an existing parent needs permission to view parents."`.

A different `403` appears while a password change is pending:

```json
{
  "success": false,
  "message": "You must change your password before continuing.",
  "errorCode": "PASSWORD_CHANGE_REQUIRED",
  "traceId": "0HN7K2F4Q1V3A:00000001"
}
```

(This one is written by `MustChangePasswordMiddleware`, which fills `traceId` from ASP.NET's own request id instead of the correlation id, so it looks different.)

### 404 Not Found

```json
{
  "success": false,
  "message": "Student 9999 does not exist.",
  "errorCode": "NOT_FOUND",
  "traceId": "e4f1b2c3-6d7a-4b8c-9e0f-1a2b3c4d5e6f"
}
```

The wording differs per record (`"Role 7 could not be found."`, `"Admission 5 does not exist."`). A student outside a teacher's scope gets exactly this body too, on purpose (section 5).

### 409 Conflict

Two different cases share the status. A duplicate or a state that does not allow the action:

```json
{
  "success": false,
  "message": "Username 'clerk.demo1' is already taken.",
  "errorCode": "CONFLICT",
  "traceId": "f0a1b2c3-d4e5-4f60-8a9b-0c1d2e3f4a5b"
}
```

and a stale row version (somebody saved first):

```json
{
  "success": false,
  "message": "This record was changed by someone else after you opened it. Reload it and make your changes again.",
  "errorCode": "CONCURRENCY_CONFLICT",
  "traceId": "a9b8c7d6-e5f4-4a3b-8c2d-1e0f9a8b7c6d"
}
```

### Also used by the API

| Status | `errorCode` | When |
|---|---|---|
| 422 | `BUSINESS_RULE_VIOLATION` | The request is well formed but a business rule refuses it, e.g. `"At least one guardian is required to enrol. Add a guardian to the application first."` or a malformed row version. |
| 413 | `PAYLOAD_TOO_LARGE` | An upload larger than the server accepts. |
| 500 | `INTERNAL_SERVER_ERROR` | Anything unexpected. The message is always the generic `"An unexpected error occurred."`; the details are only in the log, found by `traceId`. |

All the bodies above are built from the code (`GlobalExceptionMiddleware`, `Program.cs`, the controllers and service messages). The GUIDs in `traceId` are made up.

---

## 4. Five worked examples (.http)

These are in the `.http` format used by Visual Studio 2022 (17.12+) and the VS Code "REST Client" extension, the same as `backend/SchoolManagement.API/E2-Users-Settings.http`. Put them in one file and send them from top to bottom: later requests use values from earlier responses (`# @name x` then `{{x.response.body.$.field}}`). Change the usernames and numbers on a second run, because they must be unique.

All the response bodies are what the code **should** return, built from the DTO definitions. The ids, dates, tokens and row versions are made up. I did not run them (**⚠ Not verified** against a live API).

```http
@baseUrl = https://localhost:7100
```

### Example 1: sign in

```http
### 1. Sign in (no token needed)
# @name login
POST {{baseUrl}}/api/auth/login
Content-Type: application/json

{ "usernameOrEmail": "admin", "password": "Admin@123" }

@token = {{login.response.body.$.token}}
```

Expected: `200 OK`

```json
{
  "userId": 1,
  "username": "admin",
  "email": "admin@school.local",
  "roles": [ "Admin" ],
  "token": "eyJhbGciOiJIUzI1NiIs...",
  "expiresAtUtc": "2026-10-15T08:15:00Z",
  "mustChangePassword": false
}
```

The refresh token is **not** in the body. It arrives as an httpOnly cookie in the `Set-Cookie` header (`eschool_rt` in Development, `__Secure-eschool_rt` otherwise, path `/api/auth`). Wrong password gives `401 INVALID_CREDENTIALS`; an empty field gives `400 VALIDATION_ERROR` (section 3).

### Example 2: list students

```http
### 2. List students (needs Students.View or Students.ViewScoped)
GET {{baseUrl}}/api/students
Authorization: Bearer {{token}}
```

Expected: `200 OK`, a JSON array. There are no query parameters: the API returns every student you may see and the React page filters and pages in the browser.

```json
[
  {
    "id": 41,
    "admNo": "ADM-2026-0041",
    "rollNumber": "7",
    "classSectionId": 3,
    "classSectionName": "LKG C (2026-2027)",
    "admissionDate": "2026-04-01",
    "status": "Active",
    "photoUrl": null,
    "firstName": "Aisha",
    "middleName": null,
    "lastName": "Khan",
    "gender": "Female",
    "dateOfBirth": "2021-03-14",
    "mobile": "+971 50 123 4567",
    "category": "General",
    "transportRequired": false,
    "nationality": "Indian",
    "curriculumTrack": null,
    "house": "Red",
    "ealCode": null,
    "allergies": null,
    "admissionRegNo": "REG-2026-0012",
    "admissionId": 12,
    "hasAllergies": true
  }
]
```

What to notice: `allergies` is `null` and `hasAllergies` is `true`: you hold `Students.View` but not `Students.ViewSensitive`, so you get the yes/no flag but not the text. A caller who has only `Students.ViewScoped` (a teacher) gets a shorter row without `mobile`, `category`, `nationality` and the admission fields. See section 5.

### Example 3: create a user

```http
### 3. Create a clerk (needs Users.Create; the Clerk role must exist and you must hold every permission it grants)
# @name createClerk
POST {{baseUrl}}/api/users
Authorization: Bearer {{token}}
Content-Type: application/json

{
  "username": "clerk.demo1",
  "email": "clerk.demo1@school.local",
  "password": "Temp@12345",
  "roleName": "Clerk",
  "profile": {
    "fullName": "Demo Clerk",
    "gender": "Female",
    "dateOfBirth": "1992-03-14",
    "mobile": "+971 50 123 4567",
    "joiningDate": "2026-09-01",
    "qualification": "B.Com",
    "employeeNumber": "EMP-DEMO-1",
    "bloodGroup": "O+",
    "emergencyContactName": "Demo Contact",
    "emergencyContactPhone": "+971 50 765 4321"
  },
  "addresses": null
}

@clerkId = {{createClerk.response.body.$.id}}
@clerkVersion = {{createClerk.response.body.$.rowVersion}}
```

Expected: `201 Created`, header `Location: api/users/12`, body:

```json
{
  "id": 12,
  "username": "clerk.demo1",
  "email": "clerk.demo1@school.local",
  "status": "Active",
  "roles": [ "Clerk" ],
  "mustChangePassword": true,
  "isLocked": false,
  "lastLoginAt": null,
  "createdAt": "2026-10-15T06:20:11Z",
  "teacherId": null,
  "profile": {
    "fullName": "Demo Clerk",
    "needsName": false,
    "gender": "Female",
    "dateOfBirth": "1992-03-14",
    "mobile": "+971 50 123 4567",
    "joiningDate": "2026-09-01",
    "qualification": "B.Com",
    "employeeNumber": "EMP-DEMO-1",
    "bloodGroup": "O+",
    "emergencyContactName": "Demo Contact",
    "emergencyContactPhone": "+971 50 765 4321"
  },
  "addresses": [],
  "rowVersion": "AAAAAAAAB9E="
}
```

Things that go wrong: the same username again → `409 CONFLICT` (`"Username 'clerk.demo1' is already taken."`); a weak password or missing profile name → `400 VALIDATION_ERROR` with keys such as `Password` and `Profile.FullName`; `"roleName": "Student"` or `"Parent"` → `422` (those logins are not built yet). The password is temporary: the new user must change it at the first sign-in (`mustChangePassword: true`). Role `Teacher` also creates the teacher record.

### Example 4: save a role's permissions with a row version

A role's permission set is replaced as a whole, and the request must carry the `rowVersion` you last read. First create a practice role, then change it.

```http
### 4a. Create a custom role (needs Roles.Create)
# @name createRole
POST {{baseUrl}}/api/roles
Authorization: Bearer {{token}}
Content-Type: application/json

{
  "name": "ZT Reception",
  "description": "Practice role",
  "permissions": [ "ClassSections.View" ]
}

@roleId = {{createRole.response.body.$.id}}
```

Expected: `201 Created`, header `Location: api/roles/7`, body is the role (same shape as 4b).

```http
### 4b. Read the role to get the current rowVersion
# @name role
GET {{baseUrl}}/api/roles/{{roleId}}
Authorization: Bearer {{token}}

@roleVersion = {{role.response.body.$.rowVersion}}
```

```json
{
  "id": 7,
  "name": "ZT Reception",
  "description": "Practice role",
  "isSystem": false,
  "isActive": true,
  "hasAllPermissions": false,
  "permissions": [ "ClassSections.View" ],
  "rowVersion": "AAAAAAAAB9I="
}
```

```http
### 4c. Replace the permission set (needs Roles.Edit; you must hold every permission you add or remove)
# @name setPerms
PUT {{baseUrl}}/api/roles/{{roleId}}/permissions
Authorization: Bearer {{token}}
Content-Type: application/json

{
  "permissions": [ "ClassSections.View", "Subjects.View", "Subjects.Edit" ],
  "rowVersion": "{{roleVersion}}"
}
```

Expected: `200 OK`, the updated role. Note the **new** `rowVersion`: use it for the next change.

```json
{
  "id": 7,
  "name": "ZT Reception",
  "description": "Practice role",
  "isSystem": false,
  "isActive": true,
  "hasAllPermissions": false,
  "permissions": [ "ClassSections.View", "Subjects.Edit", "Subjects.View" ],
  "rowVersion": "AAAAAAAAB9M="
}
```

(`permissions` is sorted alphabetically by the service, so do not rely on the order you sent.)

```http
### 4d. Send the SAME request again: the version is now stale
PUT {{baseUrl}}/api/roles/{{roleId}}/permissions
Authorization: Bearer {{token}}
Content-Type: application/json

{
  "permissions": [ "ClassSections.View", "Subjects.View", "Subjects.Edit" ],
  "rowVersion": "{{roleVersion}}"
}
```

Expected: `409 Conflict` with `"errorCode": "CONCURRENCY_CONFLICT"` (section 3). Fix: read the role again (4b) and resend with the fresh `rowVersion`.

Other things this endpoint refuses: `Subjects.Edit` without `Subjects.View` and `ClassSections.View` → `422` (`"Some permissions need others granted with them: ..."`); an unknown name → `422` (`"Unknown permission(s): ..."`); a role you hold yourself → `403`; the Admin role → `422` (it always has every permission); a rowVersion that is not valid base64 → `422` (`"The record version is not valid. Reload the record and try again."`).

### Example 5: enrol an admission

Enrolling turns an **Admitted** application into a student. It needs `Admissions.Enroll`, and you need a class section with room. First ask which existing parents match each guardian, then send one decision per guardian.

```http
### 5a. Who matches each guardian? (needs Admissions.Edit)
# @name matches
GET {{baseUrl}}/api/admissions/12/guardian-matches
Authorization: Bearer {{token}}
```

Expected: `200 OK`

```json
{
  "admissionId": 12,
  "guardians": [
    {
      "admissionGuardianId": 31,
      "relationType": "Father",
      "name": "Imran Khan",
      "mobile": "9876501111",
      "suggestedAction": "UseExisting",
      "candidates": [
        { "parentId": 8, "name": "Imran Khan", "mobile": "9876501111", "email": null, "linkedChildren": [ "Zara Khan" ] }
      ],
      "linkedParentId": null
    },
    {
      "admissionGuardianId": 32,
      "relationType": "Mother",
      "name": "Sana Khan",
      "mobile": "9876502222",
      "suggestedAction": "CreateNew",
      "candidates": [],
      "linkedParentId": null
    }
  ]
}
```

`suggestedAction` is only a suggestion: `UseExisting`, `CreateNew`, `MustChoose` (several parents share the number), `MissingMobile` (add a mobile first) or `ParentRemoved` (the parent picked at registration was deleted). The server checks every decision again.

```http
### 5b. Enrol (needs Admissions.Enroll). One decision per guardian is required.
# @name enrol
POST {{baseUrl}}/api/admissions/12/enroll
Authorization: Bearer {{token}}
Content-Type: application/json

{
  "rollNumber": "7",
  "admissionNumber": "ADM-2026-0041",
  "admissionDate": "2026-10-15",
  "entryPoint": null,
  "transportRequired": false,
  "allottedClassSectionId": 3,
  "nationality": null,
  "curriculumTrack": null,
  "englishProficiency": null,
  "ealCode": null,
  "house": null,
  "allergies": null,
  "guardians": [
    { "admissionGuardianId": 31, "action": "UseExisting", "parentId": 8 },
    { "admissionGuardianId": 32, "action": "CreateNew",   "parentId": null }
  ]
}
```

Expected: `200 OK`, the application, now with `"status": "Enrolled"` and a `studentId` (shortened here):

```json
{
  "id": 12,
  "regNo": "REG-2026-0012",
  "firstName": "Aisha",
  "lastName": "Khan",
  "status": "Enrolled",
  "rollNumber": "7",
  "admissionNumber": "ADM-2026-0041",
  "admissionDate": "2026-10-15",
  "allottedClassSectionId": 3,
  "allottedClassSectionName": "LKG C (2026-2027)",
  "studentId": 41,
  "guardians": [ "..." ],
  "rowVersion": "AAAAAAAAB9Q="
}
```

All of it happens in **one transaction**: the student, the new parents, the links, the move of the documents and the status change. If anything fails, nothing is saved. Typical failures:

| Status | Why |
|---|---|
| `409` | The application is not Admitted (`"Cannot enroll application 'REG-2026-0012' — current status is 'Registered', expected 'Admitted'."`), the class section is inactive or full (`"'LKG C (2026-2027)' is full (30/30)."`), the admission number is used, or the roll number is taken in that class. |
| `422` | There is no guardian, a guardian has no decision, or a decision breaks a rule (the same parent twice, `UseExisting` without a `parentId`). |
| `404` | The application, class section or chosen parent does not exist. |

The enrol request has **no** `rowVersion`: two people enrolling the same application at once are queued by a database lock, and the second one gets the `409` above.

---

## 5. Rules that are easy to miss

### Row-version conflicts (optimistic concurrency)

- Every record that two people could edit at once has a `rowVersion`: a base64 text such as `AAAAAAAAB9E=` that changes on every save.
- Read the record, keep its `rowVersion`, and **send it back unchanged** with your edit. If someone saved in between, your edit is refused with `409 CONCURRENCY_CONFLICT` and nothing is overwritten. Reload and re-apply your change; never retry blindly with the old version.
- Requests that carry a `rowVersion`: users (edit, status), roles (edit, permissions, status), school settings, class sections (edit), parents (edit), students (edit), admissions (edit), and document review (verify, reject, unverify).
- A `rowVersion` that is not valid base64 is a `422`, not a `409`.
- After a successful save, the response has the **new** `rowVersion`: use that one next time.
- Class-section status changes, subjects and teachers do not carry one (I found none in their request types).

### Closed admissions

| Status | Can edit | Can confirm | Can enrol | Can reject | Can delete |
|---|---|---|---|---|---|
| Registered | yes | yes (moves to Admitted) | no | yes | yes |
| Admitted | yes | no | yes | yes | yes |
| Enrolled | **no** | no | no | no | **no** (it is the student's admission record) |
| Rejected | **no** | no | no | no | yes |

Refusals are `409 CONFLICT` with a sentence such as `"Application 'REG-2026-0012' is enrolled and can no longer be edited. Edit the student record instead."` or `"Cannot reject an application with status 'Enrolled'."`. Once enrolled, change the **student**, not the application. Enrolment needs at least one guardian, and a decision for every guardian.

### Data scope for teachers

- Some permissions come in two forms: `Students.View` (everyone) and `Students.ViewScoped` (only related people). The same applies to Parents and Attendance. If you hold both, the full one wins.
- A **teacher** with `Students.ViewScoped` sees only students whose **current** class they teach this academic year (class teacher or subject teacher). A parent sees their own children; a student sees themself.
- The list is filtered silently. A student outside the scope answers `404 NOT_FOUND`, **with the same body as a student that does not exist**, so nobody can probe which ids exist.
- A scoped reader gets a **smaller** response, with the same property names but fewer of them: no contact details or address, category, religion, nationality, admission data, identity documents, pickup persons or health fields, and only a yes/no `hasAllergies`. Parents of a student are shown only as name and mobile; a parent sees only their own link, never the co-guardian.
- The scope is worked out on every request and is not cached: a new assignment applies on the next request.
- `Attendance.Mark` (not `Attendance.Manage`) is checked in the service against the teacher's own class or subject. Which status and message come back when it fails I did not check (**⚠ Not verified**). I only confirmed the student endpoints return 404 for out-of-scope records; I did not check parents and attendance.

### Sensitive-data hiding

`Students.ViewSensitive` is a separate permission. Without it, the same endpoint still works but returns less:

- Allergy **text** is `null` (the `hasAllergies` yes/no stays). On a save, whatever you send for allergies is ignored and the stored text is kept.
- `GET /api/students/{id}`: `aadhaarNumber` and `passportNumber` come back masked to their last characters (for example `********9012`) and `isMasked` is `true`.
- `PUT /api/students/{id}/identity` needs **both** `Students.Edit` and `Students.ViewSensitive`.
- Downloading a document of a sensitive type (identity, medical) needs `Students.ViewSensitive` on top of `Documents.View`.
- Reading a student record leaves an audit row (a "sensitive read"), so who looked at what can be found later.
- Passwords and password hashes are never returned, and hashes and security stamps are masked in the audit log.

### Sessions end when an account changes

The access token carries only the user id, a **security stamp** and a token id. On **every** request the API compares the stamp in the token with the one stored for the user. When an account changes, the stamp is changed, so the old token stops working immediately (`401 UNAUTHENTICATED`), and the refresh tokens are revoked, so a refresh fails too (`401 REFRESH_TOKEN_INVALID`). The user must sign in again. This happens when:

- the status becomes Inactive or Suspended (back to Active does not undo it),
- an administrator resets the password,
- the user's **role** is changed,
- the account is deleted,
- the user changes their own password (other devices are signed out) or uses "sign out everywhere" (`POST /api/auth/logout-all`).

Two things that look similar but are different:

- **Editing a role's permissions** (`PUT /api/roles/{id}/permissions`) does **not** end sessions. It clears the cached permissions of the users holding the role, so the new rights apply from their next request without a new sign-in (permissions are not stored in the token). The cache lives in the API process, so this assumes a single API instance.
- A user who **must change their password** is let through only to `change-password`, `me`, `refresh`, `logout` and `logout-all`; everything else is `403 PASSWORD_CHANGE_REQUIRED`.

Guards that protect these changes: you cannot change your own role, status or delete yourself (use My Account); the last active Admin cannot be demoted, deactivated or deleted; and you cannot give a role that holds permissions you do not hold yourself.

---

## 6. What I could not verify

I did not run the API, the database or the frontend for this guide. In particular:

1. **Example values.** All ids, dates, tokens, GUIDs and row versions are invented. The *shapes* come from the DTO records and the *messages* from the code, but I did not capture a real response.
2. **The first administrator.** I did not find where the first Admin account is created. `admin` / `Admin@123` comes from the sample `.http` file and the tests; your database may differ.
3. **Setup details.** Migrations are not applied at start-up (I found no `Migrate` call), and Swagger is Development-only: both read from `Program.cs`. The signing-key length advice and your database name are not from the code.
4. **Validation message text** (FluentValidation's default wording) and the exact error keys for each endpoint.
5. **Malformed JSON** probably returns ASP.NET's default `application/problem+json` rather than our envelope: I found no custom handler, but did not test.
6. **Role examples 4a to 4d**: the status codes and shapes come from the controller and service code; the `409` in 4d is what the concurrency check should produce.
7. **Teacher scope for parents and attendance**, and what `Attendance.Mark` returns for another teacher's class. Only the students endpoints were checked.
8. **Frontend page column**: found by searching for the call in the React source, not by clicking through the app. "— (not used by the UI yet)" means no caller was found. Students are created only by enrolment (no add-student screen exists), even though `POST /api/students` exists.
9. **Permissions of the seeded roles** (what Clerk, Supervisor and Teacher hold by default) are not listed here; look at the Roles page.
