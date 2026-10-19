# Auth controller: what each endpoint actually does

Source: `backend/SchoolManagement.API/Controllers/AuthController.cs`, route prefix `api/auth`.
Written for a developer with about two years of experience who is learning this codebase.

## How this file was made

- **Everything here was read from the code** (controller, `AuthService`, `RefreshTokenService`, repositories, middleware, `Program.cs`, the React `services/` and `store/` code) and cross-checked against the test names that exist in `backend/SchoolManagement.Tests`.
- **I did not run the API or the database.** There is no SQL Server in my environment. So **every example request and response below is invented**: the shapes, field names, error codes and messages come from the code, but the values (ids, tokens, dates, trace ids) are made up. Nothing here is a captured real response.
- "Tests exist" means a test with that name is in the repo. I did not run them.
- **Not verified** marks anything I could not confirm from the code alone.
- The doubts and possible bugs I noticed are at the end, labelled **Blocker**, **Should fix** or **Question**.

## The six endpoints at a glance

| # | Method + URL | Credential | Called by |
|---|---|---|---|
| 1 | `POST /api/auth/login` | none (anonymous) | `LoginPage.tsx` |
| 2 | `POST /api/auth/refresh` | refresh cookie + CSRF header | `session.ts` (`refreshSession`) |
| 3 | `POST /api/auth/logout` | refresh cookie + CSRF header | `Sidebar.tsx`, `ChangePasswordRequiredPage.tsx` |
| 4 | `POST /api/auth/logout-all` | Bearer access token | no caller found in the React code |
| 5 | `GET /api/auth/me` | Bearer access token | `AuthContext.tsx` |
| 6 | `POST /api/auth/change-password` | Bearer access token | `ChangePasswordRequiredPage.tsx` |

## Background you need before the endpoints

### Two tokens

| | Access token | Refresh token |
|---|---|---|
| Form | JWT, 15 minutes (`Jwt:ExpiryMinutes`) | 32 random bytes, base64url (43 characters) |
| Where it lives | JSON response body; React keeps it in memory only (`session.ts`) | `httpOnly; Secure; SameSite=Strict` cookie, `Path=/api/auth` |
| Cookie name | not a cookie | `__Secure-eschool_rt` (Development: `eschool_rt`) |
| What the JWT contains | `sub` (user id), `security_stamp`, `jti`. **No roles, no permissions.** | not a JWT |
| What the database stores | nothing | only the SHA-256 hash (`RefreshTokens.TokenHash`, `binary(32)`) |
| Lifetime | 15 min | idle 7 days (slides on every refresh), absolute 30 days from login |

File: `JwtTokenGenerator.cs` (access), `RefreshTokenService.cs` + `RefreshTokenCookie.cs` (refresh).

### A "family" is one login on one device

Every login starts a new `FamilyId`. Every refresh revokes the presented token (reason `Rotated`) and inserts a new token with the same `FamilyId`. At most one token per family is usable. Another device has another family, so reuse on one device does not end the other.

**The values stored in `RefreshTokens.RevokedReason`** (`Domain/Entities/RefreshTokenRevokeReasons.cs`, column width 20, enforced by check constraint `CK_RefreshTokens_RevokedReason`): `Rotated`, `Logout`, `LogoutAll`, `PasswordChanged`, `ReuseDetected`, `SessionInvalidated`. A family revoked for reuse gets exactly `ReuseDetected`. Only rows still active are updated, so the earlier token that was already rotated keeps `Rotated`.

### The request pipeline for every Auth call (in order)

From `Program.cs`:

1. `CorrelationIdMiddleware`: reads or creates `X-Correlation-Id`, puts it in the Serilog context, echoes it in the response header.
2. `UseSerilogRequestLogging`.
3. `GlobalExceptionMiddleware`: turns exceptions into the JSON error envelope (details in `api-quick-guide.md` section 3).
4. `UseHttpsRedirection`, then `UseCors` (the allowed-origins list is empty by default, because the UI and API are served from one origin).
5. `UseAuthentication`: validates the JWT. After the signature and lifetime pass, `OnTokenValidated` (in `Program.cs`) reads the user from the **permission cache** and rejects the token if the user is missing, deleted, not `Active`, or the `security_stamp` claim differs from the cached stamp. This is the "sessions end when an account changes" rule.
6. `MustChangePasswordMiddleware`: if the user's `MustChangePassword` flag is set, every path except the five listed in that file returns **403 `PASSWORD_CHANGE_REQUIRED`**. The five allowed paths are `/api/auth/change-password`, `/me`, `/refresh`, `/logout`, `/logout-all`.
7. `UseAuthorization`: the fallback policy requires an authenticated user. No `Module.Action` permission is needed for any Auth endpoint.
8. The controller action (the CSRF filter, where present, runs here).

### Audit, cache and who the "user" is

- `AuditableEntitySaveChangesInterceptor` runs on every `SaveChanges`. It stamps `UpdatedAt`/`UpdatedBy` and writes `AuditLogs` rows for changed fields. `RefreshToken` is `[AuditIgnore]`, so token rows never produce audit rows. On `User`, `LastLoginAt` and `AccessFailedCount` are `[AuditIgnore]`; `PasswordHash` and `SecurityStamp` are `[AuditMasked]` (logged as `***`).
- On **anonymous** requests (login) the interceptor has no user id, so audit rows are written with user name `"system"` and `UpdatedBy` stays unchanged.
- The permission cache (`PermissionCacheService`) is a **singleton in-memory dictionary**. Services call `InvalidateUser(id)` **after** the save commits.

### Testing it

Swagger: `POST /api/auth/login` returns the token in the body; click Authorize and paste it. For `/refresh` and `/logout`, Swagger shows an `X-Requested-With` header pre-filled with `eschool` (`CsrfHeaderOperationFilter.cs`). The cookie is sent by the browser automatically when you use Swagger UI on the same origin.

---

## 1. `POST /api/auth/login`

### 1a. Purpose and caller

Sign in with a username or e-mail address and a password. Starts a new device session: returns a short-lived access token and sets the refresh cookie.
Called by `LoginPage.tsx` through `AuthContext.login` → `authService.login` → `authHttp.post("/auth/login")`, then `startSession(data)` stores the token in memory and tells other tabs.

### 1b. Request

JSON body, `LoginRequest` (`Application/DTOs/Auth/LoginRequest.cs`). Validator: `Application/Validators/LoginRequestValidator.cs`.

| Field | Type | Required | Rules |
|---|---|---|---|
| `usernameOrEmail` | string | yes | `NotEmpty()` only. No maximum length. Matched against `Users.Username` **or** `Users.Email` (exact match; case follows the database collation, **Not verified**) |
| `password` | string | yes | `NotEmpty()` only. Deliberately no strength check ("that rule applies at account creation") |

No headers are required. No cookie is read.

### 1c. Response

`200 OK`, body `AuthResult` (`Application/DTOs/Auth/AuthResult.cs`) plus a `Set-Cookie` header. The refresh token is **never** in the body (`AuthSession` keeps the two apart on purpose).

| Field | Type | Meaning |
|---|---|---|
| `userId` | int | `Users.Id` |
| `username` | string | |
| `email` | string | |
| `roles` | string[] | Names of **all** the user's roles. This list does not filter out inactive roles (see doubt 8) |
| `token` | string | The access JWT |
| `expiresAtUtc` | datetime (UTC) | When the access token expires |
| `mustChangePassword` | bool | If true, the UI forces the change-password page |

How it differs by user: it does not. There is no data scope and no sensitive data on this endpoint. `mustChangePassword = true` users get the same body, but every other endpoint is then blocked by `MustChangePasswordMiddleware`.

`Set-Cookie`: `<name>=<43 chars>; expires=<idle expiry>; path=/api/auth; secure; samesite=strict; httponly`.

### 1d. Logic, in order

| # | Step | File and method |
|---|---|---|
| 1 | Authentication middleware. No `Authorization` header is needed; the endpoint is `[AllowAnonymous]` | `AuthController.Login` attribute |
| 2 | `MustChangePasswordMiddleware` skips unauthenticated requests | `MustChangePasswordMiddleware.InvokeAsync` |
| 3 | Validate the body. On failure the **controller** builds the 400 envelope itself (it does not throw) | `AuthController.Login`, `LoginRequestValidator` |
| 4 | Load the user with roles: `SELECT ... FROM Users JOIN UserRoles JOIN Roles WHERE Username = @x OR Email = @x`. The global query filter hides soft-deleted users | `UserRepository.GetByUsernameOrEmailAsync` |
| 5 | User not found, or `Status != "Active"`, or `LockoutEnd > UtcNow` → return `null` | `AuthService.LoginAsync` |
| 6 | Verify the password (ASP.NET Core Identity's PBKDF2 `PasswordHasher<User>`) | `Infrastructure/Security/PasswordHasher.cs` |
| 7a | **Wrong password:** `AccessFailedCount++`. At 5 failures set `LockoutEnd = UtcNow + 15 min` and reset the counter to 0. `SaveChanges`, then `InvalidateUser`. Return `null` | `AuthService.LoginAsync` |
| 7b | **Right password:** `AccessFailedCount = 0`, `LockoutEnd = null`, `LastLoginAt = UtcNow`. `SaveChanges`, then `InvalidateUser` | `AuthService.LoginAsync` |
| 8 | Start a new family: generate 32 random bytes, hash with SHA-256, `INSERT INTO RefreshTokens` (`ExpiresAt = now + 7 d`, `FamilyExpiresAt = now + 30 d`, IP and user agent stored, truncated to 45 and 256 chars) | `RefreshTokenService.IssueAsync` → `RefreshTokenRepository.AddAsync` |
| 9 | Build the JWT (`sub`, `security_stamp`, `jti`) and the `AuthResult` | `AuthService.CreateAccessResult`, `JwtTokenGenerator.GenerateToken` |
| 10 | Write the cookie, return 200 | `RefreshTokenCookie.Write` |

Database work: one `SELECT` (step 4), one `UPDATE Users` (step 7), one `INSERT RefreshTokens` (step 8). Three separate commits.
Transaction and locks: none explicit. The interceptor opens its own transaction only when a new row needs an audit entry after insert; here it does not.
Audit rows: the `Users` update stamps `UpdatedAt`. An `AuditLogs` row is written **only** if an audited column changed, which is `LockoutEnd` (the 5th failure sets it; the first successful login after an expired lockout clears it). That row has user name `"system"`. A normal success or failure writes no audit row because the other changed columns are `[AuditIgnore]`.
Cache and session effects: `InvalidateUser(id)` after each save, so the next request reloads the user from the database. A new refresh family is created; **existing families of the same user are not touched**.

### 1e. Business rules and where each is enforced

| Rule | Enforced in |
|---|---|
| Both fields present | Validator (controller calls it) |
| One generic failure for unknown user / wrong password / inactive / locked out, so nothing reveals which | Service (`return null`) and controller (single 401) |
| Only `Active` users can sign in | Service. The database only restricts the allowed values: `CK_Users_Status IN ('Active','Inactive','Suspended')` |
| 5 wrong passwords → 15-minute lockout, counter resets | Service constants `MaxFailedAttempts`, `LockoutDuration`. Not configurable |
| Soft-deleted users cannot sign in | Global query filter on `Users` (EF Core). **Not verified** in the configuration file; inferred from the comment in `AuthService.RefreshAsync` and `PermissionCacheService` |
| Username and e-mail cannot be confused with each other | Username pattern `^[a-zA-Z0-9._-]+$` (no `@`, in `UserRequestValidators.MustBeValidUsername`) plus unique filtered indexes on `Username` and `Email` |
| Refresh token never in the body | `AuthSession` type + controller returns `session.Result` only |

### 1f. Errors

| Status | `errorCode` | Condition |
|---|---|---|
| 400 | `VALIDATION_ERROR` | `usernameOrEmail` or `password` empty. `errors` lists the fields |
| 401 | `INVALID_CREDENTIALS` | Any failure in steps 5 to 6: unknown user, wrong password, not Active, locked out |
| 500 | `INTERNAL_SERVER_ERROR` | Unhandled exception (for example the database is down) |
| 307 | none | Plain `http://` request in an environment where HTTPS redirection is on |
| 400 / 415 | not our envelope | Malformed JSON or a wrong `Content-Type`: ASP.NET's own response. **Not verified** (not tested) |

Locked-out accounts get the same 401 and no `Retry-After`. There is no 429: no rate limiter is configured in `Program.cs`.

### 1g. Concurrency and retries

- No row version is involved: sign-in does not call `User.NewVersion()`, on purpose, so an administrator's open edit form is not made stale by someone signing in.
- **Repeating the call is safe but not idempotent**: every success creates another family. A client that logs in twice leaves the first family alive until its idle expiry (7 days). The browser keeps only the newest cookie.
- Parallel wrong-password requests read and write `AccessFailedCount` without a lock or an atomic `+1` (doubt 1).

### 1h. Why is it built this way?

**Decision:** a 15-minute JWT that holds only identity plus a security stamp, paired with a rotating, hashed, httpOnly refresh cookie.
**Alternative:** one long-lived JWT kept in `localStorage`.
**Trade-off:** the long-lived JWT is simpler and needs no token table, but any script injected into the page (XSS) could steal it and it cannot be revoked. Here the refresh token is invisible to JavaScript and revocable, and stamp comparison makes password, role or status changes take effect on the next request. The price is more moving parts: a `RefreshTokens` table that grows by a row per refresh, a daily purge job, a CSRF header on cookie endpoints, and a stamp check on every request that depends on a **single-instance in-memory cache**.

### Examples (all invented)

```http
POST /api/auth/login
Content-Type: application/json

{ "usernameOrEmail": "admin", "password": "Admin@123" }
```

```http
HTTP/1.1 200 OK
Content-Type: application/json
Set-Cookie: eschool_rt=INVENTED-43-CHARACTER-TOKEN-VALUE-XXXXXXXXXXXX; expires=Thu, 22 Oct 2026 07:00:00 GMT; path=/api/auth; secure; samesite=strict; httponly
X-Correlation-Id: 5c0f1c1e-0000-0000-0000-invented00001

{
  "userId": 1,
  "username": "admin",
  "email": "admin@example.com",
  "roles": ["Admin"],
  "token": "eyJ...invented...",
  "expiresAtUtc": "2026-10-15T07:15:00Z",
  "mustChangePassword": false
}
```

```http
HTTP/1.1 401 Unauthorized

{
  "success": false,
  "message": "Invalid username/email or password.",
  "errorCode": "INVALID_CREDENTIALS",
  "traceId": "5c0f1c1e-0000-0000-0000-invented00002"
}
```

Tests that exist: `AuthControllerTests` (200 with seeded admin, wrong password 401, empty body 400); `AuthServiceTests` (unknown user, inactive, locked, expired lockout, counter, fifth attempt locks, success resets, `MustChangePassword`); `RefreshTokenTests.Login_SetsSecureHttpOnlyStrictCookie_AndKeepsRefreshTokenOutOfBody`, `Login_StoresOnlyTheHash_WithSevenAndThirtyDayExpiries`.

---

## 2. `POST /api/auth/refresh`

### 2a. Purpose and caller

Swap the refresh cookie for a new access token and a **rotated** cookie. It is anonymous because the access token has already expired when it is needed; the cookie is the credential.
Called by `session.ts` `refreshSession()` (via `authHttp.post("/auth/refresh")`), which is used (a) once at app start by `AuthContext` → `restoreSession()` so a page reload keeps the user signed in, (b) by the 401 handling in `apiClient.ts` when an API call is rejected, (c) when another tab broadcasts a login. The calls are serialised inside one tab (`inFlight`) and across tabs (Web Locks API, `withAuthLock`).

### 2b. Request

No body. Needs:

| Input | Where | Required | Rule |
|---|---|---|---|
| refresh token | cookie `__Secure-eschool_rt` (Dev: `eschool_rt`), sent automatically for `Path=/api/auth` | yes | Missing, blank, unknown, expired, revoked or reused → 401 |
| `X-Requested-With: eschool` | header | yes | `RequireCsrfHeaderAttribute.cs`; exact, case-sensitive value |

No validator.

### 2c. Response

`200 OK`, same `AuthResult` as login, new `token`, and a **new** `Set-Cookie` (the old one is now useless). Not scoped; same for every user. On any failure the cookie is cleared.

### 2d. Logic, in order

| # | Step | File and method |
|---|---|---|
| 1 | Authentication middleware: nothing to validate (anonymous). `MustChangePasswordMiddleware` skips unauthenticated requests and also lists `/refresh` as allowed | `Program.cs`, `MustChangePasswordMiddleware` |
| 2 | CSRF header check; missing → 403 before any database work, and the token is **not** consumed | `RequireCsrfHeaderAttribute.OnActionExecuting` |
| 3 | Read the cookie | `RefreshTokenCookie.Read` |
| 4 | Hash it and `SELECT` the row by `TokenHash` (unique index `UX_RefreshTokens_TokenHash`), no tracking | `RefreshTokenService.LookupAsync` → `RefreshTokenRepository.GetByHashAsync` |
| 5 | Classify: not found → `NotFound`; revoked with reason `Rotated` → **`Reused`**. The whole family is revoked at once with `RevokedReason = 'ReuseDetected'` (only rows that are still active are updated) and a Warning is logged; revoked for another reason → `Revoked`; `ExpiresAt <= now` → `Expired`; else `Active` | `LookupAsync` |
| 6 | Anything but `Active` → `null` → controller clears the cookie and returns 401 | `AuthService.RefreshAsync`, `AuthController.Refresh` |
| 7 | Load the owner with roles. User missing (soft-deleted), `Status != Active`, or `user.SecurityStamp != token.SecurityStamp` → `RevokeFamily(SessionInvalidated)` and return `null`. **Lockout is deliberately not checked** | `AuthService.RefreshAsync`, `UserRepository.GetByIdAsync` |
| 8 | Rotate: inside an execution-strategy **transaction**: (i) `INSERT` the replacement token (same `FamilyId`, same `FamilyExpiresAt`, `ExpiresAt = min(now + 7 d, FamilyExpiresAt)`); (ii) `UPDATE RefreshTokens SET RevokedAt, RevokedReason='Rotated', ReplacedByTokenId WHERE Id = @current AND RevokedAt IS NULL`; (iii) if exactly 1 row changed, commit; otherwise roll back | `RefreshTokenService.RotateAsync` → `RefreshTokenRepository.TryRotateAsync` |
| 9 | Lost the race (0 rows) → revoke the whole family with `RevokedReason = 'ReuseDetected'` (same code path as step 5), return `null` | `RotateAsync` |
| 10 | New JWT using the user's **current** stamp, write the new cookie, 200 | `AuthService.CreateAccessResult`, `RefreshTokenCookie.Write` |

Locks: the `UPDATE ... WHERE RevokedAt IS NULL` takes a row lock; a second request with the same token waits, then sees `RevokedAt` set, updates 0 rows and loses. That is the lock that makes "exactly one winner" true.
Audit rows: none (`RefreshToken` is `[AuditIgnore]`, and `User` is not modified).
Cache: not touched. Logging: Information on start of family; Warning `Refresh token reuse detected ...` (token value and hash are never logged).

### 2e. Business rules and where each is enforced

| Rule | Enforced in |
|---|---|
| Cookie requests need the custom header | Controller attribute `RequireCsrfHeader` (second line of defence after `SameSite=Strict`) |
| Only the hash is stored | Service (`HashToken`); column is `binary(32)` |
| A rotated token can never be used again; reuse kills the family | Service (`LookupAsync`, `RotateAsync`) |
| Exactly one concurrent request can rotate a token | Repository (`TryRotateAsync`) using a conditional `UPDATE` inside a transaction |
| Idle expiry ≤ absolute expiry, `ExpiresAt > IssuedAt` | Database check constraint `CK_RefreshTokens_Expiry`; the service relies on it and compares only `ExpiresAt` |
| A revoked row always has a reason, an active one has none | Database check constraint `CK_RefreshTokens_Revoked`; reasons limited by `CK_RefreshTokens_RevokedReason` |
| Password / role / status change ends the session | Service compares stamps (step 7), same rule as `OnTokenValidated` for access tokens |
| Absolute 30-day cap | Service (`CreateToken` caps `ExpiresAt`), database check |
| Old rows deleted after `RetentionDays` | `RefreshTokenCleanupJob` (daily, first run 5 minutes after start) → `PurgeExpiredAsync` |

### 2f. Errors

| Status | `errorCode` | Condition |
|---|---|---|
| 403 | `CSRF_CHECK_FAILED` | `X-Requested-With` missing or not `eschool` |
| 401 | `REFRESH_TOKEN_INVALID` | No cookie, unknown, expired, revoked, **reused**, user deleted/inactive, stamp changed, or lost the rotation race. All the same body on purpose |
| 500 | `INTERNAL_SERVER_ERROR` | Unhandled exception |

### 2g. Concurrency and retries

- **Not safe to repeat with the same cookie.** The second use of a token is treated as theft: the family is revoked and the user has to sign in again. This is covered by `Refresh_ReplayingARotatedToken_Returns401_AndRevokesTheWholeFamily` and `Refresh_TwoConcurrentRequestsWithTheSameToken_ExactlyOneWins_ThenFamilyIsRevoked`.
- The frontend avoids accidental reuse: one in-flight call per tab and a cross-tab Web Lock. `logout` takes the same lock.
- No row version is used; the conditional `UPDATE` is the concurrency control.
- If the response is lost in the network after the server rotated, the browser still holds the old cookie. The next refresh looks like reuse (doubt 5).

### 2h. Why is it built this way?

**Decision:** strict reuse detection with token rotation, revoking the whole family on any replay.
**Alternative:** a non-rotating refresh token that stays valid until expiry, or rotation with a short grace window.
**Trade-off:** rotation + reuse detection means a stolen cookie is useful only until the real user refreshes (and then both are cut off), but a harmless double-submit (two tabs, a retry) also ends the session. The team chose safety and put the double-submit prevention in the browser (locks) rather than loosening the server. The alternatives are simpler for users and weaker against theft.

### Examples (all invented)

```http
POST /api/auth/refresh
X-Requested-With: eschool
Cookie: eschool_rt=INVENTED-43-CHARACTER-TOKEN-VALUE-XXXXXXXXXXXX
```

```http
HTTP/1.1 200 OK
Set-Cookie: eschool_rt=INVENTED-NEW-43-CHARACTER-TOKEN-VALUE-YYYY; expires=Thu, 22 Oct 2026 07:20:00 GMT; path=/api/auth; secure; samesite=strict; httponly

{ "userId": 1, "username": "admin", "email": "admin@example.com", "roles": ["Admin"],
  "token": "eyJ...invented...", "expiresAtUtc": "2026-10-15T07:35:00Z", "mustChangePassword": false }
```

```http
HTTP/1.1 401 Unauthorized
Set-Cookie: eschool_rt=; expires=Thu, 01 Jan 1970 00:00:00 GMT; path=/api/auth; secure; samesite=strict

{ "success": false, "message": "Your session has ended. Please sign in again.",
  "errorCode": "REFRESH_TOKEN_INVALID", "traceId": "5c0f1c1e-0000-0000-0000-invented00003" }
```

Tests that exist: `RefreshTokenTests` (rotation, replay, other sessions unaffected, concurrent, no CSRF header, no cookie, unknown token, idle expiry, deactivated user, must-change-password user); `AuthServiceTests.RefreshAsync_*`; `RefreshTokenServiceTests`.

### Sequence diagram: login, refresh and the cookie

```mermaid
sequenceDiagram
    autonumber
    participant B as Browser (React)
    participant A as API (AuthController)
    participant S as AuthService / RefreshTokenService
    participant D as SQL Server

    Note over B,D: Login
    B->>A: POST /api/auth/login {usernameOrEmail, password}
    A->>A: LoginRequestValidator
    A->>S: LoginAsync
    S->>D: SELECT user + roles
    S->>S: verify PBKDF2 hash
    S->>D: UPDATE Users (reset failures, LastLoginAt)
    S->>S: InvalidateUser (permission cache)
    S->>D: INSERT RefreshTokens (hash only, new FamilyId)
    A-->>B: 200 {token, expiresAtUtc, ...} + Set-Cookie (httpOnly, Path=/api/auth)
    Note over B: access token kept in memory only

    Note over B,D: 15 minutes later, or after a page reload
    B->>A: POST /api/auth/refresh (cookie + X-Requested-With: eschool)
    A->>A: CSRF header check
    A->>S: RefreshAsync(cookie)
    S->>D: SELECT RefreshTokens WHERE TokenHash = SHA256(cookie)
    alt token Active and user stamp matches
        S->>D: BEGIN; INSERT replacement; UPDATE old SET Rotated WHERE RevokedAt IS NULL; COMMIT
        A-->>B: 200 new token + new Set-Cookie
    else already rotated (reuse) or lost the race
        S->>D: UPDATE family SET RevokedReason='ReuseDetected' WHERE RevokedAt IS NULL
        A-->>B: 401 REFRESH_TOKEN_INVALID + cookie cleared
        Note over B,D: tokens already rotated keep 'Rotated'; the family's still-active token becomes 'ReuseDetected'
        Note over B: React publishes "signed out" and shows the login page
    else expired or already revoked
        A-->>B: 401 REFRESH_TOKEN_INVALID + cookie cleared
    else user inactive/deleted or stamp changed
        S->>D: UPDATE family SET RevokedReason='SessionInvalidated' WHERE RevokedAt IS NULL
        A-->>B: 401 REFRESH_TOKEN_INVALID + cookie cleared
    end
```

---

## 3. `POST /api/auth/logout`

### 3a. Purpose and caller

End **this device's** session and clear the cookie. Anonymous so it still works after the access token has expired.
Called by `Sidebar.tsx` (sign-out button) and `ChangePasswordRequiredPage.tsx` through `AuthContext.logout` → `authService.logout`, which runs the call under the same Web Lock as refresh and then calls `endSession()` even if the server cannot be reached.

### 3b. Request

No body. Same two inputs as refresh: the refresh cookie (optional here) and the header `X-Requested-With: eschool` (required). No validator.

### 3c. Response

`204 No Content`, and a `Set-Cookie` that deletes the cookie (same `Path`, `Secure`, `SameSite`, or the browser would keep it). Always 204, even with no cookie or an unknown or already-ended session.

### 3d. Logic, in order

| # | Step | File and method |
|---|---|---|
| 1 | CSRF header check (403 if missing) | `RequireCsrfHeaderAttribute` |
| 2 | Look the token up by hash | `RefreshTokenService.LookupAsync` |
| 3 | If the token is `Active` **or** `Expired`, `UPDATE RefreshTokens SET RevokedAt, RevokedReason='Logout' WHERE FamilyId = @f AND RevokedAt IS NULL` (one statement, no entity loaded) | `AuthService.LogoutAsync` → `RefreshTokenRepository.RevokeFamilyAsync` |
| 4 | Not found, blank or already revoked: nothing. A `Reused` token was already revoked inside the lookup | `LogoutAsync` |
| 5 | Clear the cookie, return 204 | `RefreshTokenCookie.Clear` |

Transaction: none (a single `UPDATE`). Audit rows: none. Cache and stamp: **not touched**. The access token that was already issued keeps working until it expires (at most 15 minutes), because the stamp is not rotated here (doubt 6).

### 3e. Business rules and where each is enforced

| Rule | Enforced in |
|---|---|
| Logout is idempotent and reveals nothing | Service (no exception on unknown token); controller (always 204) |
| Only the current device's family is ended | Repository query filters by `FamilyId` |
| Header required | Controller attribute |

### 3f. Errors

| Status | `errorCode` | Condition |
|---|---|---|
| 403 | `CSRF_CHECK_FAILED` | Header missing or wrong |
| 500 | `INTERNAL_SERVER_ERROR` | Unhandled exception |

There is no 401: a bad cookie is not an error here.

### 3g. Concurrency and retries

Safe to repeat any number of times (test: `Logout_EndsTheSession_ClearsTheCookie_AndIsIdempotent`). No row version. If a logout races with a refresh from another tab using the same cookie, the loser may be treated as reuse; the browser prevents this with the shared lock.

### 3h. Why is it built this way?

**Decision:** logout revokes the refresh *family* only, and is anonymous plus idempotent.
**Alternative:** require a valid access token and rotate the security stamp on every logout.
**Trade-off:** the chosen design never fails for a user who is already half signed out (expired access token), and it leaves their other devices alone. The cost is that a copied access token still works for up to 15 minutes. "Sign out everywhere" (next endpoint) is the strong version.

### Example (invented)

```http
POST /api/auth/logout
X-Requested-With: eschool
Cookie: eschool_rt=INVENTED-43-CHARACTER-TOKEN-VALUE-XXXXXXXXXXXX
```
```http
HTTP/1.1 204 No Content
Set-Cookie: eschool_rt=; expires=Thu, 01 Jan 1970 00:00:00 GMT; path=/api/auth; secure; samesite=strict
```

Tests that exist: `RefreshTokenTests.Logout_*`; `AuthServiceTests.LogoutAsync_*`.

---

## 4. `POST /api/auth/logout-all`

### 4a. Purpose and caller

"Sign out everywhere": end every session of the current user, on every device, immediately.
Caller: `authService.logoutAll()` exists, but **no component calls it** (searched the React source). Treat the endpoint as built but not yet reachable from the UI.

### 4b. Request

No body, no validator. `Authorization: Bearer <access token>`. No `X-Requested-With` header is needed (it is a Bearer endpoint; browsers do not attach that header on their own). No permission is required beyond being signed in.

### 4c. Response

`204 No Content` and a `Set-Cookie` that clears the cookie. Not scoped.

### 4d. Logic, in order

| # | Step | File and method |
|---|---|---|
| 1 | JWT validated; `OnTokenValidated` stamp check | `Program.cs` |
| 2 | `MustChangePasswordMiddleware`: allowed even when a password change is pending | `MustChangePasswordMiddleware` |
| 3 | Read the user id from the `sub` claim | `AuthController.GetCurrentUserId` |
| 4 | Load the user (with roles) | `UserRepository.GetByIdAsync` |
| 5 | `SecurityStamp = Guid.NewGuid()`, `SaveChanges` | `AuthService.LogoutAllAsync` |
| 6 | `InvalidateUser(id)` after the commit, so the next request on any device fails the stamp check | `AuthService.LogoutAllAsync` |
| 7 | `UPDATE RefreshTokens SET RevokedAt, RevokedReason='LogoutAll' WHERE UserId = @u AND RevokedAt IS NULL` (uses filtered index `IX_RefreshTokens_UserId_Active`) | `RefreshTokenService.RevokeAllForUserAsync` → `RefreshTokenRepository.RevokeAllForUserAsync` |
| 8 | Clear the cookie, 204 | `RefreshTokenCookie.Clear` |

Transaction: none across steps 5 and 7. Security does not depend on step 7 succeeding: the new stamp already kills every access token (step 1 of the next request) and every refresh token (stamp compared in `RefreshAsync`); step 7 only records why they ended.
Audit rows: one `AuditLogs` row, action `Update`, table `Users`, field `securityStamp` shown as `***` (it is `[AuditMasked]`), attributed to the current user. `User.NewVersion()` is **not** called, so an administrator's open Users edit form does not become stale.
Logging: Information `Revoked N refresh token(s) for user ...`.

### 4e. Business rules and where each is enforced

| Rule | Enforced in |
|---|---|
| Access tokens die at once, not after 15 minutes | Service rotates the stamp; `OnTokenValidated` (`Program.cs`) compares it on every request |
| The user's other sessions all end | Service (`RevokeAllForUser`) and, independently, the stamp check in `RefreshAsync` |
| The caller must be signed in | Authorization fallback policy + `[Authorize]` on the class |

### 4f. Errors

| Status | `errorCode` | Condition |
|---|---|---|
| 401 | `UNAUTHENTICATED` | Missing, malformed, expired token, or stamp mismatch |
| 404 | `NOT_FOUND` | The user row vanished between token validation and the load (should not happen; **Not verified**) |
| 500 | `INTERNAL_SERVER_ERROR` | Unhandled exception |

### 4g. Concurrency and retries

Safe to repeat: each call rotates the stamp again. The repeat call itself arrives with the old token, so it gets 401 (the first call already invalidated it). The stamp write has no conflict check; two simultaneous calls both succeed and the last stamp wins. Test: `LogoutAll_EndsEverySession_AndAccessTokensStopWorkingImmediately`.

### 4h. Why is it built this way?

**Decision:** rotate `User.SecurityStamp`, and revoke the refresh tokens as a second, "bookkeeping" step.
**Alternative:** only revoke refresh tokens.
**Trade-off:** revoking alone leaves already-issued access tokens working for up to 15 minutes, so "sign out everywhere" would not mean it. Rotating the stamp fixes that, at the price of one extra audit row and a cache invalidation; and the same stamp is also used by password, role and status changes, so there is one mechanism for "end all sessions".

### Example (invented)

```http
POST /api/auth/logout-all
Authorization: Bearer eyJ...invented...
```
```http
HTTP/1.1 204 No Content
Set-Cookie: eschool_rt=; expires=Thu, 01 Jan 1970 00:00:00 GMT; path=/api/auth; secure; samesite=strict
```

---

## 5. `GET /api/auth/me`

### 5a. Purpose and caller

Tell the frontend who is signed in and what they may do, so it can show or hide menu items and buttons.
Called by `AuthContext.tsx` (`authService.me()`) after sign-in and again when an API call returns 403 (`notifyForbidden` → reload), because an administrator may have changed the user's role.

### 5b. Request

No body, no query string, no validator. `Authorization: Bearer <access token>`. Allowed even when `MustChangePassword` is true.

### 5c. Response

`200 OK`, `CurrentUserResponse` (`Application/DTOs/Auth/CurrentUserResponse.cs`):

| Field | Type | Meaning |
|---|---|---|
| `userId` | int | |
| `username` | string | |
| `email` | string | |
| `roles` | string[] | Names of the user's **active** roles only |
| `permissions` | string[] | Every permission name held, e.g. `Students.View`. For a user with the `Admin` role it is the entire catalog (`Permissions.All`), computed in code, not read from the database |
| `mustChangePassword` | bool | |

Differences: a teacher or other scoped user gets `Students.ViewScoped` instead of `Students.View`, which is how the UI knows they only see their own students. There is no sensitive data in this response; whether someone holds `Students.ViewSensitive` shows up only as a permission name.

### 5d. Logic, in order

| # | Step | File and method |
|---|---|---|
| 1 | JWT validated; `OnTokenValidated` reads the **cache**, not the database (stamp, status, deleted) | `Program.cs`, `PermissionCacheService.GetUserInfoAsync` |
| 2 | `MustChangePasswordMiddleware`: path is on the allowed list | `MustChangePasswordMiddleware` |
| 3 | Authorization: fallback policy (authenticated) | `Program.cs` |
| 4 | Read `sub` | `AuthController.GetCurrentUserId` |
| 5 | Read the cached info and map it | `AuthService.GetCurrentUserAsync` |
| 6 | Cache miss → load once: `Users` + `UserRoles` + `Roles` + `RolePermissions` + `Permissions`, no tracking, query filters ignored; drop inactive roles; Admin gets all permissions | `PermissionCacheService.LoadAndCacheAsync` |

Transaction, locks, audit rows, session effects: none. Read-only.

### 5e. Business rules and where each is enforced

| Rule | Enforced in |
|---|---|
| Inactive roles grant nothing | `PermissionCacheService` (filters `Role.IsActive`) |
| Admin holds every permission, including ones added later | `PermissionCacheService` (shortcut on the role name) |
| Permission changes show up quickly | Callers of role/user changes call `InvalidateUser` / `InvalidateAll` after committing (see the Users and Roles files) |

### 5f. Errors

| Status | `errorCode` | Condition |
|---|---|---|
| 401 | `UNAUTHENTICATED` | No/invalid/expired token, user deleted or inactive, stamp mismatch |
| 204 | none | `GetCurrentUserAsync` returns `null` and `Ok(null)` becomes an empty 204. Cannot happen after step 1 passed. **Not verified** |
| 500 | `INTERNAL_SERVER_ERROR` | Unhandled exception |

### 5g. Concurrency and retries

Read-only, safe to repeat. The cache is a plain in-process `ConcurrentDictionary`; with two API instances, an invalidation on one would not reach the other (the code says it assumes a single instance).

### 5h. Why is it built this way?

**Decision:** keep roles and permissions **out of the JWT** and resolve them server-side from a cache on every request.
**Alternative:** put roles and permissions in token claims.
**Trade-off:** claims would avoid any lookup, but a changed role would not apply until the token expires and tokens would be large. The cache makes permission changes effective at once and keeps the token tiny, at the cost of a per-instance cache that must be invalidated correctly.

### Example (invented)

```http
GET /api/auth/me
Authorization: Bearer eyJ...invented...
```
```http
HTTP/1.1 200 OK

{
  "userId": 7,
  "username": "t.rahman",
  "email": "t.rahman@example.com",
  "roles": ["Teacher"],
  "permissions": ["Attendance.Mark", "Students.ViewScoped", "ClassSections.View"],
  "mustChangePassword": false
}
```

(The permission names in this example are invented as an illustration; the real set for the Teacher role is defined by the E1 migration, which I have not listed here.)

---

## 6. `POST /api/auth/change-password`

### 6a. Purpose and caller

Let a signed-in user set a new password. Also clears `MustChangePassword`, ends every other device's session, and gives this device a fresh session.
Called by `ChangePasswordRequiredPage.tsx` (the forced screen at `/change-password`, enforced by `ProtectedRoute`). I did not find any other caller; the My Account page uses the `/api/me` endpoints (covered in the Me controller file, later).

### 6b. Request

JSON body `ChangePasswordRequest`, validator `Application/Validators/ChangePasswordRequestValidator.cs`. Bearer token required. Allowed when `MustChangePassword` is true.

| Field | Type | Required | Rules |
|---|---|---|---|
| `currentPassword` | string | yes | Not empty. Checked against the stored hash by the service |
| `newPassword` | string | yes | Not empty; at least 8 characters; at least one uppercase `[A-Z]`, one lowercase `[a-z]`, one digit `[0-9]`, one special `[^a-zA-Z0-9]` |

There is no maximum length and no rule that the new password must differ from the current one.

### 6c. Response

`200 OK`, `AuthResult` with `mustChangePassword: false` and a new access token bound to the **new** security stamp, plus a new refresh cookie (a new family for this device). Not scoped.

### 6d. Logic, in order

| # | Step | File and method |
|---|---|---|
| 1 | JWT validated, stamp checked | `Program.cs` |
| 2 | `MustChangePasswordMiddleware` lets the path through | `MustChangePasswordMiddleware` |
| 3 | Validate the body; on failure the **controller** returns the 400 envelope | `AuthController.ChangePassword` |
| 4 | Load the user | `UserRepository.GetByIdAsync` |
| 5 | Verify `currentPassword`; wrong → throws `BusinessRuleException` | `AuthService.ChangePasswordAsync` |
| 6 | Hash the new password, `MustChangePassword = false`, `SecurityStamp = Guid.NewGuid()`, `SaveChanges` | `AuthService.ChangePasswordAsync` |
| 7 | `InvalidateUser(id)` after the commit | same |
| 8 | Revoke every active refresh token of the user (reason `PasswordChanged`) | `RefreshTokenService.RevokeAllForUserAsync` |
| 9 | Issue a new family for this device (carries the new stamp) | `RefreshTokenService.IssueAsync` |
| 10 | New JWT, new cookie, 200 | `CreateAccessResult`, `RefreshTokenCookie.Write` |

Transactions: three separate commits (user update, bulk revoke, token insert). A failure after step 6 leaves the password changed and all sessions dead, and the user simply signs in again with the new password.
Audit rows: one `Users` `Update` row by the current user showing `passwordHash: "***"`, `securityStamp: "***"` and `mustChangePassword: true → false`.
Session effects: the access token used for this very request stops working as soon as step 6 commits (stamp mismatch), so the frontend must use the token in the response.

### 6e. Business rules and where each is enforced

| Rule | Enforced in |
|---|---|
| New password strength | Validator (controller calls it). The same five rules also exist as `MustBeStrongPassword` for user creation, with slightly different messages (doubt 12) |
| Must prove the current password | Service (`VerifyPassword`) |
| Password change ends all other sessions | Service (stamp rotation + `RevokeAllForUser`); stamp comparison in `OnTokenValidated` and `RefreshAsync` |
| Forced change clears the flag | Service |
| Passwords stored only as PBKDF2 hashes | `Infrastructure/Security/PasswordHasher.cs` |

### 6f. Errors

| Status | `errorCode` | Condition |
|---|---|---|
| 400 | `VALIDATION_ERROR` | Any rule above; `errors` has the field names and messages (for example "New password must be at least 8 characters.") |
| 401 | `UNAUTHENTICATED` | Bad/expired token or stamp mismatch |
| 422 | `BUSINESS_RULE_VIOLATION` | "Current password is incorrect." |
| 404 | `NOT_FOUND` | User row missing (should not happen) |
| 500 | `INTERNAL_SERVER_ERROR` | Unhandled exception |

### 6g. Concurrency and retries

- No row version; two simultaneous change-password calls can both pass the current-password check, and the later write wins.
- **A retry after a lost response will fail:** the old access token is dead (401), and the old password no longer matches (422). The user must sign in with the new password. That is a safe failure, not a duplicate change.
- Wrong current passwords are not counted toward the lockout (doubt 3).

### 6h. Why is it built this way?

**Decision:** one call that changes the password, rotates the stamp, revokes other sessions and returns a fresh session.
**Alternative:** change the password and force a full re-login.
**Trade-off:** returning a new session keeps the user on the page (no second sign-in, which matters for the forced first-login flow), but it makes the endpoint do three commits and return tokens, so it needs the same care as login.

### Example (invented)

```http
POST /api/auth/change-password
Authorization: Bearer eyJ...invented...
Content-Type: application/json

{ "currentPassword": "Temp@12345", "newPassword": "Better#Pass9" }
```
```http
HTTP/1.1 200 OK
Set-Cookie: eschool_rt=INVENTED-NEW-43-CHARACTER-TOKEN-VALUE-ZZZZ; path=/api/auth; secure; samesite=strict; httponly

{ "userId": 7, "username": "t.rahman", "email": "t.rahman@example.com", "roles": ["Teacher"],
  "token": "eyJ...invented...", "expiresAtUtc": "2026-10-15T07:45:00Z", "mustChangePassword": false }
```
```http
HTTP/1.1 422 Unprocessable Entity

{ "success": false, "message": "Current password is incorrect.",
  "errorCode": "BUSINESS_RULE_VIOLATION", "traceId": "5c0f1c1e-0000-0000-0000-invented00004" }
```

Tests that exist: `RefreshTokenTests.ChangePassword_EndsEveryOtherSession_AndGivesThisDeviceANewOne`, `MustChangePasswordUser_CanStillRefresh_ButOtherEndpointsStayBlocked`; `AuthServiceTests.ChangePasswordAsync_*`.

---

## Doubts and possible bugs

**Blocker**

- None found. I did not find anything in this controller that I would stop a release for.

**Should fix**

1. **Lockout counter can be bypassed with parallel guesses.** `AuthService.LoginAsync` does `user.AccessFailedCount++` on a tracked entity and then `SaveChanges`. Two requests that read the same row both write the same value, so a burst of N parallel wrong passwords can be counted as one. `User.RowVersion` does not help because login deliberately does not change it. An atomic `ExecuteUpdate` (`AccessFailedCount = AccessFailedCount + 1`) would close this. Not tested.
2. **Anyone can lock anyone out (for 15 minutes) and there is no rate limit.** Five wrong passwords for a known username locks that account. No rate limiter exists in `Program.cs`, so this can be repeated forever, including against the only Admin account. Unlocking is `AuthService.UnlockUserAsync`, used by the Users screen, which the locked Admin cannot use.
3. **Wrong `currentPassword` in change-password is never counted.** A stolen 15-minute access token allows unlimited guessing of the current password, with no lockout and no rate limit.
4. **Failed sign-ins are not logged anywhere that would show a guessing attack.** I looked for it in the code and found: `AuthService` and `AuthController` have no logger at all. A wrong password makes the controller return `Unauthorized(...)` (not an exception), so `GlobalExceptionMiddleware` logs nothing. The only trace is the generic Serilog request line for `POST /api/auth/login` with status 401, which has the correlation id, path, status and duration; no username, no user id and no IP address (`Program.cs` enriches the log only with `FromLogContext`, and `UseForwardedHeaders` is not configured). `AccessFailedCount` is `[AuditIgnore]`, so failed attempts write no `AuditLogs` row. The only audit row is the one written when the 5th failure sets `LockoutEnd`, and it is attributed to `"system"`, with no IP and not which attempt caused it. A successful login is also only visible as the Information line `Refresh token family ... started for user ...` (no IP). Consequence: someone trying one password against many usernames (password spraying) never reaches the lockout on any one account, and nothing in the logs identifies it. Suggestion (not done): log every failed attempt at Warning with the user id if found (never the password or the typed username text if it could be a password), the client IP and the reason class (unknown user / wrong password / inactive / locked), and alert on a rate. I labelled this *Should fix* rather than *Question* because the code already answers the question: no.

**Question**

5. **Reuse detection and a lost response.** If the server rotates the cookie but the response never reaches the browser, the next refresh looks like reuse and ends the session. Is a short grace window (for example 10 to 30 seconds, return the same successor) worth the added complexity? Today the Web Locks only prevent self-inflicted double-submits.
6. **Access token survives a plain logout for up to 15 minutes**, because `Logout` revokes only the refresh family and does not rotate the stamp. Is that acceptable? (`logout-all` does rotate it.)
7. **Timing difference reveals whether a username exists.** An unknown user returns before any password hashing happens; a known user pays for a PBKDF2 verification. The response bodies are identical, but the response time is not. Not measured.
8. **`roles` differs between endpoints.** Login returns every role name from `user.UserRoles`; `/me` and the permission cache drop inactive roles. A user with an inactive role would see it in the login result but not in `/me`.
9. **`MustChangePasswordMiddleware` puts `context.TraceIdentifier` in `traceId`**, while every other error uses `GetCorrelationId()`. So the trace id in a `PASSWORD_CHANGE_REQUIRED` response will not match the `X-Correlation-Id` in the logs.
10. **Login page hides every failure.** `LoginPage.tsx` shows "Invalid username/email or password." for any error, including a network failure or a 500.
11. **`PasswordHasher.VerifyPassword` treats `SuccessRehashNeeded` as success but never rehashes**, so password hashes would never be upgraded if the Identity hasher's defaults change.
12. **Password rules are written twice** (`ChangePasswordRequestValidator` and `MustBeStrongPassword`), with different message prefixes. A future change to one can drift from the other.
13. **No maximum length on `usernameOrEmail` or `password`** at login (the columns are 50 and 100). Not harmful by itself; only the body-size limit applies.
14. **Duplicate sessions accumulate.** Each login creates a family and never ends the one in the cookie it replaces; an abandoned family lives up to 7 days idle. Cleanup is by expiry only.
15. **`GetClientInfo` stores the proxy's IP** until `UseForwardedHeaders` is configured (the code comment says it is a deployment step; not present in `Program.cs`).
16. **Data type/clock:** lockout uses `DateTime.UtcNow` directly while tokens use `TimeProvider`. Tests of lockout therefore cannot control the clock.
17. **Validation error key casing.** `ToErrorDictionary()` keys come from FluentValidation property names (`UsernameOrEmail`). Whether the JSON keys are PascalCase or camelCase depends on the serializer options on the path that writes them; I could not confirm which. **Not verified.**
