# Settings and Masters (admission fees): what each endpoint actually does

Sources: `backend/SchoolManagement.API/Controllers/SettingsController.cs` (prefix `api/settings`) and `AdmissionFeesController.cs` (prefix `api/admission-fees`).
Written for a developer with about two years of experience who is learning this codebase.

## How this file was made

- **Everything here was read from the code**: both controllers, `SchoolSettingsService`, `AdmissionFeeService`, `SchoolTimeZones`, `AdmissionFeeRepository`, the two validators, the DTOs, `SchoolSettingsConfiguration` and `AdmissionFeeConfiguration`, and the names of the tests.
- **I did not run the API or the database.** There is no SQL Server in my environment. So **every example below is invented**: shapes, field names, error codes and messages come from the code, but names and numbers are made up.
- Tests exist: `AdmissionFeeServiceTests` (list, set rounding, update not duplicate, unknown class 404, clear, suggested fee) and, in `UsersApiTests`, the school settings save and the time-zone list. I did not run them in this session.
- **Not verified**: which screens use `workingDays` and `currencyCode` after they are saved. I only found the time zone in use (admission dates and the REG year).
- The doubts I noticed are at the end, labelled **Blocker**, **Should fix** or **Question**.

## The seven endpoints at a glance

| # | Method + URL | Permission needed | Called by (React) |
|---|---|---|---|
| 1 | `GET /api/settings/school` | `Settings.View` | `SchoolSettingsPage` |
| 2 | `PUT /api/settings/school` | `Settings.Edit` | `SchoolSettingsPage` |
| 3 | `GET /api/settings/time-zones` | `Settings.View` | `SchoolSettingsPage` (picker) |
| 4 | `GET /api/settings/basics` | any signed-in user | top bar, address forms |
| 5 | `GET /api/admission-fees` | `Settings.View` | `MastersPage`, admission confirm |
| 6 | `PUT /api/admission-fees/{className}` | `Settings.Edit` | `MastersPage` |
| 7 | `DELETE /api/admission-fees/{className}` | `Settings.Edit` | `MastersPage` |

"Masters" (school configuration lists) share the `Settings` permissions; there is no separate master permission. The suggested fee for one application (`GetSuggestedForAdmissionAsync`) is served by the Admissions controller, see `admissions.md`.

## Background you need before the endpoints

### The same request pipeline as Auth

Correlation id, Serilog, `GlobalExceptionMiddleware`, JWT, `MustChangePasswordMiddleware`, then the permission policy. See `auth.md`.

### School settings: one row

`SchoolSettings` holds the **single** school record. A database check (`CK_SchoolSettings_SingleRow`) makes a second row impossible; the row is created by a migration. It has a `rowversion`. Nothing here is deleted or created through the API, only edited.

### The settings provider (cache)

`ISchoolSettingsProvider` caches the settings for the rest of the code (admission date and year, the top bar). After `PUT /api/settings/school` is saved, `Invalidate()` is called. The cache is in the API process (one instance assumed).

### Admission fee master

`AdmissionFees` holds **one fee per class name** (for example "Class 5", not per section and not per year). The class names come from the existing class sections. Unique index `UX_AdmissionFees_ClassName`, amount `decimal(10,2)`, check `Amount >= 0`.

---

## 1. `GET /api/settings/school`

### 1a. Purpose and caller
The Settings page.

### 1b. Request
None.

### 1c. Response
`200 OK`, `SchoolSettingsResponse`: `schoolName, shortName, email, phone, website, addressText, countryId, countryName, timeZoneId, currencyCode, workingDays[], aboutTagline, rowVersion`. `workingDays` are English day names, Monday first.

### 1d. Logic, in order
1. Policy `Settings.View`. 2. Read the single row with its country. 3. Convert the stored day flags to names.

### 1e. Business rules
Working days are stored as a bit mask in one column (`CK_SchoolSettings_WorkingDays` keeps it between 1 and "all days").

### 1f. Errors
403.

### 1g. Concurrency and retries
Read only. The `rowVersion` is needed by the edit.

### 1h. Why is it built this way?
One small record, no list, no paging.

### Example (invented)
```json
{ "schoolName": "Demo International School", "shortName": "DIS", "email": "info@school.example", "phone": "+97121234567",
  "website": "https://school.example", "addressText": "Khalifa City, Abu Dhabi", "countryId": 1, "countryName": "United Arab Emirates",
  "timeZoneId": "Asia/Dubai", "currencyCode": "AED", "workingDays": ["Monday","Tuesday","Wednesday","Thursday","Friday"],
  "aboutTagline": "Learning together", "rowVersion": "AAAAAAAAB9E=" }
```

---

## 2. `PUT /api/settings/school`

### 2a. Purpose and caller
Saves the school profile and the school-wide options.

### 2b. Request
`UpdateSchoolSettingsRequest`:

| Field | Rule |
|---|---|
| `schoolName` | required, max 150 |
| `shortName` | optional, max 30 |
| `email` | optional, max 100, valid e-mail |
| `phone` | optional, valid phone |
| `website` | optional, max 200, a full `http://` or `https://` address |
| `addressText` | optional, max 300 |
| `countryId` | optional, > 0, an existing active country |
| `timeZoneId` | required, max 64, an IANA id the server knows (`Asia/Dubai`); a Windows id is refused |
| `currencyCode` | required, exactly three capital letters (`AED`) |
| `workingDays` | at least one, English day names, no repeats |
| `aboutTagline` | optional, max 300 |
| `rowVersion` | required |

### 2c. Response
`200 OK`, the saved `SchoolSettingsResponse` (reloaded, so the country name is current).

### 2d. Logic, in order
1. Validator (400 with a field map).
2. Load the row and `Expect(rowVersion)`: another admin saved first gives **409** `CONCURRENCY_CONFLICT`.
3. If a country is given it must exist and be active, else **400** `VALIDATION_ERROR` (`CountryId`).
4. Copy the fields (blanks become `null`), convert the day names to flags, save.
5. Clear the settings cache (after the commit).
6. Reload and return.

### 2e. Business rules and where each is enforced
| Rule | Where |
|---|---|
| A real IANA time zone | validator (`SchoolTimeZones.IsValidIanaId`); the database cannot tell a typo, it only checks not blank |
| Currency is 3 capitals | validator and `CK_SchoolSettings_CurrencyCode` (binary collation, so `aed` is refused) |
| At least one working day | validator and `CK_SchoolSettings_WorkingDays` |
| Concurrent edits refused | row version |

### 2f. Errors
400, 403, 409.

### 2g. Concurrency and retries
Row version. Safe to retry with the same version only if the first call failed; after success the version is new.

### 2h. Why is it built this way?
The time zone is stored as an IANA id so the same text works on Windows and Linux servers and in browsers.

### Example (invented)
```json
{ "schoolName": "Demo International School", "countryId": 1, "timeZoneId": "Asia/Dubai", "currencyCode": "AED",
  "workingDays": ["Monday","Tuesday","Wednesday","Thursday","Friday"], "rowVersion": "AAAAAAAAB9E=" }
```
A Windows id such as `Arabian Standard Time` gives `400` with `TimeZoneId: Time zone must be an IANA time-zone id the server knows, for example "Asia/Dubai" or "Asia/Kolkata".`

---

## 3. `GET /api/settings/time-zones`

### 3a. Purpose and caller
Fills the time-zone drop-down.

### 3b. Request
None.

### 3c. Response
`200 OK`, array of `{ id, displayName, utcOffsetMinutes }`, for example `{ "id": "Asia/Dubai", "displayName": "(UTC+04:00) Asia/Dubai", "utcOffsetMinutes": 240 }`. Sorted by today's offset, then id.

### 3d. Logic, in order
1. Policy. 2. List every zone the server knows (Windows ids are converted to IANA ids; old names such as `Asia/Calcutta` are shown by their current name `Asia/Kolkata`). 3. Compute today's offset for each.

### 3e. Business rules
The list is built from the **server's** time-zone data, so a server without tzdata would return fewer zones. (Not verified for your Windows server; the converted ids come from .NET's table.)

### 3f. Errors
403.

### 3g. Concurrency and retries
Read only.

### 3h. Why is it built this way?
Showing the offset helps pick the right zone; storing the IANA id keeps it portable.

### Example (invented)
`[ { "id": "Asia/Dubai", "displayName": "(UTC+04:00) Asia/Dubai", "utcOffsetMinutes": 240 } ]`

---

## 4. `GET /api/settings/basics`

### 4a. Purpose and caller
The school name and country for every signed-in user: the top bar, and address forms that pre-select the school's country.

### 4b. Request
None.

### 4c. Response
`200 OK`, `{ schoolName, shortName, countryId }`.

### 4d. Logic, in order
1. Signed-in check only (no permission). 2. Read from the cached settings provider.

### 4e. Business rules
Deliberately limited to three harmless fields; the rest of the settings need `Settings.View`.

### 4f. Errors
401.

### 4g. Concurrency and retries
Read only; can be up to the cache's age out of date (cleared on every save).

### 4h. Why is it built this way?
Every page needs the school name, but not every role should read the full settings.

### Example (invented)
`{ "schoolName": "Demo International School", "shortName": "DIS", "countryId": 1 }`

---

## 5. `GET /api/admission-fees`

### 5a. Purpose and caller
Lists every class with its admission fee, for the master screen.

### 5b. Request
None.

### 5c. Response
`200 OK`, array of `{ className, amount }`; `amount` is `null` when no fee is set.

### 5d. Logic, in order
1. Policy `Settings.View`. 2. Take the distinct **class names** from the existing (non-deleted) class sections, in teaching order (LKG/UKG first, then Class 1, 2, ...). 3. Join the stored fees by class name.

### 5e. Business rules
A class name appears once, however many sections and years it has. A fee for a class name that no longer exists in any section is not listed.

### 5f. Errors
403.

### 5g. Concurrency and retries
Read only.

### 5h. Why is it built this way?
The list is driven by the classes, so every class is shown even if no fee is set yet.

### Example (invented)
`[ { "className": "LKG", "amount": 500.00 }, { "className": "Class 1", "amount": null } ]`

---

## 6. `PUT /api/admission-fees/{className}`

### 6a. Purpose and caller
Sets (creates or replaces) the fee of a class.

### 6b. Request
`{ "amount": 750.00 }`. The class name is in the URL (URL-encode spaces: `Class%205`). Amount must be between 0 and 1,000,000.

### 6c. Response
`200 OK`, `{ className, amount }` (rounded).

### 6d. Logic, in order
1. Validator (400). 2. Trim the name; it must be one of the class names in the list (**404** `There is no class named 'Class 99'.`). The comparison is exact on letters (`class 5` is not `Class 5`). 3. Round to 2 decimals (away from zero). 4. Insert or update the single row. 5. Return.

### 6e. Business rules and where each is enforced
| Rule | Where |
|---|---|
| One fee per class | unique index `UX_AdmissionFees_ClassName`; service updates the existing row |
| Amount never negative | validator and `CK_AdmissionFees_AmountNotNegative` |
| Fee is a **suggestion** for Confirm Admission, not a rule; the amount charged is typed at confirmation | see `admissions.md` |

### 6f. Errors
400, 403, 404.

### 6g. Concurrency and retries
No row version. Two admins setting the same new class at the same moment: the second hits the unique index and `AddAsync` does not translate it, so it returns a **500** (see *Should fix* 1). Repeating the same call is harmless.

### 6h. Why is it built this way?
Keyed by class name so the fee does not need to be re-entered for each new year or section.

### Example (invented)
`PUT /api/admission-fees/Class%205` with `{ "amount": 750 }` → `{ "className": "Class 5", "amount": 750.00 }`.

---

## 7. `DELETE /api/admission-fees/{className}`

### 7a. Purpose and caller
Removes a class's fee ("no suggested fee").

### 7b. Request
Class name in the URL.

### 7c. Response
`204 No Content`.

### 7d. Logic, in order
1. Policy `Settings.Edit`. 2. The class name must exist (404). 3. If a fee row exists, **hard-delete** it; if not, nothing happens (still 204).

### 7e. Business rules
It is a hard delete (no soft delete, no history). Existing admissions are unaffected because they stored their own fee.

### 7f. Errors
403, 404.

### 7g. Concurrency and retries
Idempotent: repeating returns 204.

### 7h. Why is it built this way?
The fee is only a default, so there is nothing to protect.

### Example (invented)
`DELETE /api/admission-fees/Class%205` → `204`.

---

## Doubts and possible problems

### Blocker
None found.

### Should fix
1. **Duplicate fee create race returns 500.** `AdmissionFeeRepository.AddAsync` does not translate the unique-index error into 409. Very low risk (one person edits masters).
2. **Class names with `/` or other special characters** cannot be used in the URL path (`/api/admission-fees/{className}`). Names like "Class 5/6" would break. A body or query parameter, or the class's grade, would be safer.
3. **Fee changes are not audited with a reason and the table has no `RowVersion`.** There is no history of who changed a fee and when beyond the generic audit trail (not verified for this table).
4. **The settings cache is per process** (as for roles). Fine for a single server.

### Question
1. **Is a fee per class name enough, or is it per academic year?** Fees often change each year; today the old fee is overwritten.
2. **Which screens read `workingDays` and `currencyCode`?** I only found the time zone used. If attendance or fee screens should use working days (for example to skip weekends), that is not wired yet. Please confirm.
3. **Should the demo school's country and time zone be set before the demo** (UAE and `Asia/Dubai`)? The admission number year and `RegistrationDate` use this time zone.
4. **Should "Masters" get their own permission** instead of sharing `Settings.View/Edit`? Today anyone who can edit the school profile can edit fees.
5. **More masters planned?** (document types, stages, mediums, streams are fixed lists in code today.)
