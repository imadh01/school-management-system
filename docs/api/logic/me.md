# Me controller: what each endpoint actually does

Source: `backend/SchoolManagement.API/Controllers/MeController.cs`, route prefix `api/me`.
Written for a developer with about two years of experience who is learning this codebase.

## How this file was made

- **Everything here was read from the code**: the controller, `UserService.GetMyProfileAsync` / `UpdateMyProfileAsync`, `UpdateMyProfileRequestValidator`, the DTOs and the access matrix rows.
- **I did not run the API or the database.** There is no SQL Server in my environment. So **every example below is invented**.
- Tests exist: `UsersApiTests.My_account_edits_only_my_own_details`. I did not run it in this session.
- **Not verified** marks anything I could not confirm from the code alone.
- The doubts I noticed are at the end, labelled **Blocker**, **Should fix** or **Question**.

## The two endpoints at a glance

| # | Method + URL | Permission needed | Called by (React) |
|---|---|---|---|
| 1 | `GET /api/me/profile` | any signed-in user | `MyAccountPage` |
| 2 | `PUT /api/me/profile` | any signed-in user | `MyAccountPage` |

There is no permission: the user is always the caller, and the id comes from the **token**, never from the URL, so nobody can read or edit another person's profile here. Changing your password is `POST /api/auth/change-password` (see `auth.md`).

## Background you need before the endpoints

### The same request pipeline as Auth

Correlation id, Serilog, `GlobalExceptionMiddleware`, JWT, `MustChangePasswordMiddleware`, then the signed-in check. A user who must change the password cannot use these until they have. See `auth.md`.

### What you may change about yourself

Your e-mail and your profile (name, gender, date of birth, mobile, qualification, blood group, emergency contact, addresses). You may **not** change your username, role, status, joining date or employee number: an administrator sets those on the Users page (`users.md`).

---

## 1. `GET /api/me/profile`

### 1a. Purpose and caller
Shows My Account.

### 1b. Request
None.

### 1c. Response
`200 OK`, the same `UserResponse` as `GET /api/users/{id}` (account, roles, profile, addresses, `rowVersion`).

### 1d. Logic, in order
1. Take the user id from the token (no id: **403** `My Account needs a signed-in user.`).
2. Load the user with profile and addresses (404 if gone).
3. If the account has **no staff profile**: **404** `Your account has no staff profile.`
4. Map and return.

### 1e. Business rules
Only the caller's own record is ever loaded.

### 1f. Errors
| Status | Code | When |
|---|---|---|
| 401 | | no or invalid token |
| 403 | `FORBIDDEN` | token without a user id |
| 404 | `NOT_FOUND` | user or profile missing |

### 1g. Concurrency and retries
Read only. Returns the `rowVersion` the save needs.

### 1h. Why is it built this way?
Using the token's id makes "edit someone else's profile" impossible by construction.

### Example (invented)
`GET /api/me/profile` returns the same JSON shape as `GET /api/users/31`.

---

## 2. `PUT /api/me/profile`

### 2a. Purpose and caller
Saves My Account.

### 2b. Request
`UpdateMyProfileRequest`:

| Field | Rule |
|---|---|
| `email` | required, max 100, valid e-mail |
| `fullName` | required, max 150 |
| `gender` | required, `Male` or `Female` |
| `dateOfBirth` | optional, must be in the past |
| `mobile` | required, valid phone |
| `qualification` | optional, max 150 |
| `bloodGroup` | optional, max 10 |
| `emergencyContactName` / `emergencyContactPhone` | optional (max 150 / valid phone) |
| `addresses` | optional; `null` keeps the stored ones |
| `rowVersion` | required |

### 2c. Response
`200 OK`, the updated `UserResponse`.

### 2d. Logic, in order
1. Validator (400).
2. Load your own user (as endpoint 1), `Expect(rowVersion)`: stale gives **409** `CONCURRENCY_CONFLICT`.
3. The e-mail must not belong to anyone else: **409** `Email 'x' is already registered.`
4. Build the addresses (country/region checks, 400 per field).
5. Copy e-mail and profile fields; **username, role, status, joining date and employee number are not touched**. `NewVersion()`.
6. In a transaction, replace the addresses (if sent) and save.
7. Clear your permission cache entry (the cached e-mail is shown by `/auth/me`) and return.

### 2e. Business rules and where each is enforced
| Rule | Where |
|---|---|
| E-mail unique among live users | pre-check; filtered unique index (a race gives 409 `This username, email or employee number was just taken...`) |
| You cannot change role, status, username | the request has no such fields |
| Changing your e-mail does not end your session | no session change in this method |

### 2f. Errors
400, 401, 403, 404, 409.

### 2g. Concurrency and retries
Row version. The version also changes when an administrator edits you or resets your password, so an open My Account page may get a 409 and must reload.

### 2h. Why is it built this way?
A person can fix their own contact details without needing the Users permission.

### Example (invented)
```json
{ "email": "meera@school.example", "fullName": "Meera Nair", "gender": "Female", "mobile": "+971501234567",
  "dateOfBirth": "1990-04-02", "addresses": null, "rowVersion": "AAAAAAAAB9E=" }
```

---

## Doubts and possible problems

### Blocker
None found.

### Should fix
1. **An account without a staff profile cannot open My Account at all** (404). Staff created through the Users page always have one; an account created another way (for example a seeded admin) might not. Check that the demo Admin has a profile.
2. **Changing your own e-mail does not require your password** and does not end other sessions. If the e-mail is used to reset passwords later, that is an account-takeover path. Fine today (no e-mail reset exists); worth deciding before one is added.

### Question
1. **When Student and Parent logins exist**, will they use `/api/me` too? It requires a staff profile today.
2. **Should a user be able to upload or change their photo** here? Not in the prototype analysis as far as I know (not verified).
