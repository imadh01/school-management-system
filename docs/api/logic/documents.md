# Documents controller: what each endpoint actually does

Source: `backend/SchoolManagement.API/Controllers/DocumentsController.cs`, route prefix `api` (the URLs mix `documents/...`, `students/{id}/documents` and `admissions/{id}/documents`).
Written for a developer with about two years of experience who is learning this codebase.

## How this file was made

- **Everything here was read from the code**: the controller, `DocumentService`, `DocumentRepository`, the three validators, `DocumentSettings`, the `DocumentUploadLimit` filter, `DocumentConfiguration`, the DTOs, the purge job, and the names of the tests.
- **I did not run the API or the database.** There is no SQL Server in my environment. So **every example below is invented**: shapes, field names, error codes and messages come from the code, but ids, names and numbers are made up.
- Tests exist: `DocumentServiceTests` (unit, includes the purge), `DocumentRulesTests` (database rules) and `DocumentApiTests` (oversized file, renamed file, duplicate, Admin self-review, verified delete, download + audit row, sensitive download, enrolment move). I did not run them in this session.
- **Not verified** marks anything I could not confirm from the code alone.
- The doubts I noticed are at the end, labelled **Blocker**, **Should fix** or **Question**.

## The eleven endpoints at a glance

| # | Method + URL | Permission needed |
|---|---|---|
| 1 | `GET /api/documents/options` | `Documents.View` |
| 2 | `GET /api/documents` | `Documents.View` |
| 3 | `GET /api/students/{studentId}/documents` | `Documents.View` |
| 4 | `GET /api/admissions/{admissionId}/documents` | `Documents.View` |
| 5 | `GET /api/documents/{id}/file` | `Documents.View` (+ `Students.ViewSensitive` for identity/medical types) |
| 6 | `POST /api/students/{studentId}/documents` | `Documents.Upload` |
| 7 | `POST /api/admissions/{admissionId}/documents` | `Documents.Upload` |
| 8 | `POST /api/documents/{id}/verify` | `Documents.Verify` |
| 9 | `POST /api/documents/{id}/reject` | `Documents.Verify` |
| 10 | `POST /api/documents/{id}/unverify` | `Documents.Verify` |
| 11 | `DELETE /api/documents/{id}` | `Documents.Delete` |

Callers in React: the Documents page (list, filters, review), and the Documents tabs on the student and admission screens (list, upload, download, delete).

**There is no data scope here.** Anyone holding `Documents.View` can list and (subject to the sensitive rule) download the documents of any student or application.

## Background you need before the endpoints

### The same request pipeline as Auth

Correlation id, Serilog, `GlobalExceptionMiddleware`, JWT, `MustChangePasswordMiddleware`, then the permission policy. See `auth.md`.

### What a document is

One row in `Documents` per uploaded file. It belongs to **exactly one owner**: a student **or** an admission application (`CK_Documents_ExactlyOneOwner`). It points to a **document type** (passport, Emirates ID, birth certificate, ...). Each type says whether it is **sensitive** (identity/medical) and whether it **has an expiry date**.

### Where the file lives

Not in the database and not in a web folder. `LocalFileStorage` writes the file under `Documents:StorageRoot` (default `App_Data/documents` next to the API) with a generated key. The row stores the key, the original file name (only as a label), the content type, the size and a **SHA-256 hash**. Files are only ever served through endpoint 5.

### Status lifecycle

```
Pending ──verify──▶ Verified ──unverify──▶ Pending
   │
   └──reject (with a reason)──▶ Rejected   (final: replaced by a new upload)
```

Database checks keep this honest: a reviewed document names its reviewer and time; a rejected one has a reason; a pending one has neither.

### Settings (appsettings `Documents` section)

`MaxFileSizeBytes` (default 5 MB), `DeletedFileRetentionDays` (90), `ExpiringSoonDays` (60), `PurgeEnabled` (true), `StorageRoot`. They are checked at startup; a bad value stops the API with a message.

### Soft delete, audit, concurrency

- Delete is a soft delete; the file stays on disk until the purge job removes it (see endpoint 11).
- `Documents.RowVersion` is a `rowversion`. Verify, reject and unverify must send it back.
- A deleted student's or application's documents disappear from every list with the owner (`VisibleDocuments`).
- `AuditableEntitySaveChangesInterceptor` audits changes. Every **download** also writes a `Read` audit row (who opened which document, never the content).

---

## 1. `GET /api/documents/options`

### 1a. Purpose and caller
Fills the upload form.

### 1b. Request
None.

### 1c. Response
`200 OK`: `{ types: [{ id, code, name, isSensitive, hasExpiry }], maxFileSizeBytes, expiringSoonDays }`.

### 1d. Logic, in order
1. Policy `Documents.View`. 2. Load the types (a dozen rows). 3. Keep those that are active and `ForStudents`, sorted by sort order then name. 4. Add the two settings so the browser can refuse a too-big file with the exact numbers.

### 1e. Business rules
Only active, student-facing types are offered.

### 1f. Errors
403.

### 1g. Concurrency and retries
Read only.

### 1h. Why is it built this way?
The browser and the API read the same size limit, so the message always matches.

### Example (invented)
```json
{ "types": [ { "id": 3, "code": "PASSPORT", "name": "Passport", "isSensitive": true, "hasExpiry": true } ],
  "maxFileSizeBytes": 5242880, "expiringSoonDays": 60 }
```

---

## 2. `GET /api/documents`

### 2a. Purpose and caller
The Documents page: every document, with filters.

### 2b. Request
Optional query: `status` (`Pending`, `Verified`, `Rejected`), `documentTypeId`, `expiry` (`Expired` or `ExpiringSoon`).

### 2c. Response
`200 OK`, array of `DocumentResponse` (fields in the example below), newest first.

### 2d. Logic, in order
1. Policy. 2. A status or expiry value outside the list gives **400** `VALIDATION_ERROR` with the allowed values. 3. `Expired` = expiry before today; `ExpiringSoon` = from today up to today + `ExpiringSoonDays`. 4. One query joins the owner name and number, type, uploader and reviewer names (deleted users are still named). 5. For each row the response adds `expiryState`, `isOwnUpload` (true when you uploaded it and are not an Admin, so the UI can disable Verify/Reject) and `canDownload` (false for a sensitive file when you lack `Students.ViewSensitive`).

### 2e. Business rules
| Rule | Where |
|---|---|
| Documents of deleted students/applications are hidden | `VisibleDocuments` |
| "Today" is the UTC date | service (see *Should fix* 4) |

### 2f. Errors
400, 403.

### 2g. Concurrency and retries
Read only. Each row carries its `rowVersion` for review.

### 2h. Why is it built this way?
The flags are computed on the server so the screen needs no permission logic of its own.

### Example (invented)
```json
[ { "id": 88, "ownerType": "Student", "ownerId": 301, "ownerName": "Aisha Khan", "ownerNumber": "ADM-2026-0001",
    "documentTypeId": 3, "documentTypeName": "Passport", "isSensitive": true, "hasExpiry": true,
    "title": null, "originalFileName": "passport.pdf", "contentType": "application/pdf", "sizeBytes": 412345,
    "expiryDate": "2027-03-01", "expiryState": null, "status": "Pending", "uploadedBy": "clerk1",
    "uploadedAt": "2026-10-19T05:10:00Z", "reviewedBy": null, "reviewedAt": null, "rejectionReason": null,
    "isOwnUpload": false, "canDownload": true, "rowVersion": "AAAAAAAAB9E=" } ]
```

---

## 3. `GET /api/students/{studentId}/documents`  and  4. `GET /api/admissions/{admissionId}/documents`

### 3a/4a. Purpose and caller
The Documents tab of a student or application.

### 3b/4b. Request
The id in the URL.

### 3c/4c. Response
`200 OK`, array of `DocumentResponse` (as endpoint 2) for that owner.

### 3d/4d. Logic, in order
1. Policy. 2. The owner must exist: **404** `Student {id} was not found.` / `Application {id} was not found.` 3. List that owner's documents, newest first.

### 3e/4e. Business rules
After enrolment the application's documents belong to the student (they are moved at enrolment, see `admissions.md`), so the application's tab is empty then.

### 3f/4f. Errors
403, 404.

### 3g/4g. Concurrency and retries
Read only.

### 3h/4h. Why is it built this way?
One list for each place a user looks at a person.

### Example (invented)
`GET /api/students/999/documents` returns `404` `Student 999 was not found.`

---

## 5. `GET /api/documents/{id}/file`

### 5a. Purpose and caller
Downloads the file itself.

### 5b. Request
`id` in the URL.

### 5c. Response
`200 OK` with the file bytes, the real content type, a safe download name, and the headers `X-Content-Type-Options: nosniff` and `Cache-Control: no-store`.

### 5d. Logic, in order
1. Policy `Documents.View`.
2. Load the row; missing gives **404** `Document {id} was not found.`
3. If the type is **sensitive** and the caller lacks `Students.ViewSensitive`: **403** `This is an identity or medical document. Downloading it also needs the Students.ViewSensitive permission.`
4. Open the stored file. If the file is **missing**: **422** `The file of this document is missing from storage. Ask an administrator to restore it from the backup.` (and an Error log).
5. Read it into memory and compare its **SHA-256** with the stored hash. A mismatch gives **422** `The stored file of this document is damaged...` and nothing is sent.
6. Write a `Read` audit row (`File` or `File (sensitive)`).
7. Return the bytes.

### 5e. Business rules
| Rule | Where |
|---|---|
| Sensitive types need an extra permission | service |
| Integrity is checked before sending | service |
| Every download is audited | `ISensitiveReadLog` |

### 5f. Errors
403, 404, 422.

### 5g. Concurrency and retries
Read only. The file is read whole into memory; the 5 MB limit keeps that small.

### 5h. Why is it built this way?
Serving only through the API means no URL can be guessed and every opening is logged.

### Example (invented)
Response headers: `Content-Type: application/pdf`, `Content-Disposition: attachment; filename="passport.pdf"`.

---

## 6. `POST /api/students/{studentId}/documents`  and  7. `POST /api/admissions/{admissionId}/documents`

### 6a/7a. Purpose and caller
Attaches one file to a student or an application. Called by the upload form.

### 6b/7b. Request
`multipart/form-data`, **one file per request**:

| Field | Rule |
|---|---|
| `file` | required |
| `documentTypeId` | > 0 and an active student-facing type |
| `title` | optional, max 120 |
| `expiryDate` | required if the type has an expiry; ignored (not stored) if it does not |

### 6c/7c. Response
`201 Created`, `Location: api/documents/{id}/file`, body `DocumentResponse` (status `Pending`).

### 6d/7d. Logic, in order
1. **Before the body is read** the `DocumentUploadLimit` filter checks `Content-Length`: over (`MaxFileSizeBytes` + 1 MB) gives **413** with the exact sizes. Otherwise it sets the request limit to that value.
2. Controller validates the form fields (400 `VALIDATION_ERROR`), and that a file was sent (`File: Choose a file to upload.`).
3. The owner must exist (404). For an application: if it is already **Enrolled**, **409** `This application is enrolled: its documents have moved to the student. Upload on the student's page.`
4. Service checks, collecting errors: the type is offered; an expiry date is present when needed; the file is not empty; the size is within the limit (message with the exact size). Failure gives **400** `VALIDATION_ERROR` per field.
5. **The real type is read from the first bytes** (`FileSignatures`): only **PDF, JPG, PNG** are accepted. The file name and the browser's Content-Type are never trusted, so a renamed `.exe` is refused with 400.
6. Compute the SHA-256. If the same hash is already attached to the same owner (and not deleted): **409** `This file is already attached to this student as "Passport" (pending, uploaded 19 Oct 2026). The same file is not stored twice.`
7. Save the file to storage, then insert the row. If the insert fails, the stored file is removed.
8. Log an Information line (id, content type, size, owner; no content) and return the row.

### 6e/7e. Business rules and where each is enforced
| Rule | Where |
|---|---|
| Only PDF, JPG, PNG, judged by content | `FileSignatures` |
| Max size from a setting | filter + service |
| The same file once per owner | service pre-check; final guard unique indexes `UX_Documents_Student_Sha256` / `_Admission_Sha256` (filtered to not deleted) |
| New documents start `Pending` | service |
| Uploading is allowed to a student of any status | no check |

### 6f/7f. Errors
| Status | Code | When |
|---|---|---|
| 400 | `VALIDATION_ERROR` | form field, no file, empty file, too big, wrong content, bad type, missing expiry |
| 403 | `FORBIDDEN` | no `Documents.Upload` |
| 404 | `NOT_FOUND` | owner missing |
| 409 | `CONFLICT` | enrolled application; same file already attached |
| 413 | `VALIDATION_ERROR` | body clearly over the limit |

### 6g/7g. Concurrency and retries
Two uploads of the same file at the same instant: both pass the pre-check; the unique index stops the second and `AddAsync` turns it into **409**. A double-click is therefore safe, and the stored file of the loser is removed. If the server stops between saving the file and inserting the row, an unreferenced file is left behind (see *Should fix* 3).

### 6h/7h. Why is it built this way?
Reading the content, hashing and checking before storing keeps the folder free of unwanted files.

### Example (invented)
`curl -F "file=@passport.pdf" -F "documentTypeId=3" -F "expiryDate=2027-03-01" .../api/students/301/documents`

A renamed `virus.exe.pdf`:
```json
{ "success": false, "message": "Validation failed.", "errorCode": "VALIDATION_ERROR",
  "errors": { "File": ["Only PDF, JPG and PNG files can be uploaded, and this file's content is none of them. (The content is checked, not the name: renaming a file does not change what it is.)"] },
  "traceId": "0HN7K2..." }
```

---

## 8. `POST /api/documents/{id}/verify`,  9. `POST /api/documents/{id}/reject`,  10. `POST /api/documents/{id}/unverify`

### 8a. Purpose and caller
Review a document: accept it, refuse it with a reason, or take an acceptance back. Called from the Documents page and the tabs.

### 8b. Request
- verify / unverify: `{ "rowVersion": "..." }` (must be a valid base64 version).
- reject: `{ "reason": "...", "rowVersion": "..." }`; the reason is required (not blank) and at most 300.

### 8c. Response
`200 OK`, the updated `DocumentResponse`.

### 8d. Logic, in order
1. Validator (400).
2. Load the document (404 `Document {id} was not found.`).
3. `IConcurrencyGuard.Expect` with the row version: someone else changed it first gives **409** `CONCURRENCY_CONFLICT`.
4. Status check: verify and reject need **Pending**; unverify needs **Verified**. Otherwise **409** `Only a pending document can be verified; this one is rejected.`
5. **Self-review rule** (verify and reject only): if you uploaded the document you get **422** `You uploaded this document, so someone else must verify or reject it.` An **Admin** is exempt (a small school may have one administrator).
6. Set the new status, reviewer (`VerifiedBy`) and time; reject also stores the reason. Unverify clears reviewer and time. Save, return.

### 8e. Business rules and where each is enforced
| Rule | Where |
|---|---|
| Pending → Verified or Rejected; Verified → Pending | service + database checks |
| A rejected document is **not** reopened: upload a new one | service (decision 5) |
| Uploader cannot review their own, except Admin | service only (the database cannot tell an Admin) |
| Unverify has no self-review rule | by design in code |

### 8f. Errors
400, 403, 404, 409 (`CONFLICT` or `CONCURRENCY_CONFLICT`), 422 (self-review).

### 8g. Concurrency and retries
Protected by the row version. A repeated call after success returns 409 (wrong status), which a screen should treat as already done.

### 8h. Why is it built this way?
Two-person review is the point of verification; the row version stops two reviewers overwriting each other.

### Example (invented)
```json
{ "reason": "Photo is too blurry to read the number.", "rowVersion": "AAAAAAAAB9E=" }
```

---

## 11. `DELETE /api/documents/{id}`

### 11a. Purpose and caller
Removes a document (wrong file, replaced file).

### 11b. Request
`id` in the URL. **No row version.**

### 11c. Response
`204 No Content`.

### 11d. Logic, in order
1. Policy `Documents.Delete`.
2. Load (404).
3. A **Verified** document cannot be deleted: **409** `This document is verified. Unverify it first, then delete it.`
4. Soft delete (`IsDeleted`, `DeletedAt`).
5. Later, the **purge job** (`DocumentPurgeJob`, first run 10 minutes after the API starts, then every 24 hours, only if `PurgeEnabled`) deletes the stored file of documents deleted more than 90 days ago, in batches of 100, and sets `FilePurgedAt`. The row stays.

### 11e. Business rules
| Rule | Where |
|---|---|
| Verified must be unverified first | service |
| The same file can be uploaded again after a delete | unique indexes ignore deleted rows |
| The file stays on disk for the retention period | purge job |

### 11f. Errors
403, 404, 409.

### 11g. Concurrency and retries
No version check, so a delete races with a verify: if someone verifies between your load and save the delete may still go through. A repeat returns 404.

### 11h. Why is it built this way?
Keeping the file for 90 days allows a mistaken delete to be undone by an administrator from storage.

### Example (invented)
`DELETE /api/documents/88` → `204`.

---

## Doubts and possible problems

### Blocker
None found.

### Should fix
1. **No data scope on documents.** Any role with `Documents.View` can list every student's and application's documents (names, numbers, file names, status). Only the *download* of sensitive types is protected. Confirm which roles hold `Documents.View`, or add scoping like Students.
2. **The list endpoints are unpaged.** `GET /api/documents` returns every document in the school with several joins. Fine for the demo; slow once there are thousands.
3. **Orphan files after a crash.** If the process stops between saving the file and inserting the row, the file is never referenced and never purged (the purge only looks at rows). Also, a purged file whose row stays means the download answers 422 forever; an administrator must restore from backup.
4. **"Today" for expiry is UTC**, not the school's time zone (the same issue as attendance). An expiry date can flip one day early/late around midnight Dubai time.
5. **Delete has no row version** (the other changes do), so it can race with a verify.
6. **Re-uploading a corrected file that happens to be byte-identical is refused** while the rejected one still exists, because the duplicate check counts rejected documents. The user must delete the rejected one first. The 409 message does not say that.

### Question
1. **Should an expiry date in the past be accepted at upload?** Nothing checks it; the document is "Expired" the moment it arrives.
2. **Can a document be attached to a Left/Inactive student?** Allowed today.
3. **Should unverify follow the self-review rule?** Today any reviewer can unverify any document, including their own upload.
4. **`Title`** is free text (max 120). Is the type name enough?
5. **Staff documents (teachers, users)** are mentioned in a code comment as a later step (`StaffProfileId`). Is that planned?
6. **Virus scanning** is not done; only the type and size. Is that acceptable for production?
