# Admissions: picking an existing parent at registration (G2)

**Status:** built; migration `20261014150000_G2_AdmissionGuardianParentLink`.
Before G2 a guardian was always typed by hand and matched to an existing parent only at enrolment, by mobile number. G2 lets staff
pick an existing parent while registering, so siblings' families are linked deliberately instead of by a number that may differ.

## Decisions (approved)

| # | Decision | Why |
|---|---|---|
| 1 | New nullable `AdmissionGuardians.ParentId` -> `Parents` (Restrict), index, and a filtered unique index `(AdmissionId, ParentId) WHERE ParentId IS NOT NULL`. | One column is enough; NULL keeps every existing row exactly as it was ("typed by hand"). The unique index is the safety net for "one parent cannot be two guardians". |
| 2 | The server copies name, mobile and email from the parent and **ignores** the form values of a linked guardian (they are not even validated). | The parent record is the single source of truth; a tampered or stale form cannot put different details on the application. |
| 3 | Linking needs `Parents.View`. The search (`GET /api/admissions/parent-search`) needs `Parents.View` **and** `Admissions.Create` or `Admissions.Edit`. The mobile-number hint and the children in the results exist only behind that endpoint. | The results show other families' mobile numbers and children. `Parents.ViewScoped` (a teacher's own pupils' parents) never reveals other families. |
| 4 | Keeping a link the application already has needs no permission; adding a *new* link does. | A clerk without `Parents.View` must still be able to edit remarks on an application an admin linked. |
| 5 | At enrolment the linked parent is pre-selected as "Use existing", but staff may choose someone else. The change is **audited**. | The application can be wrong; the audit trail shows who replaced which parent. |
| 6 | If the linked parent has been deleted, enrolment is refused (422) with a message telling staff to open the application and choose "Use a different person". Nothing is created. | Quietly creating a new parent would duplicate a family. |

## API

| Endpoint | Change |
|---|---|
| `POST /api/admissions`, `PUT /api/admissions/{id}` | Each guardian may carry `parentId`. Unknown or deleted parent: **404**. Same parent twice: **422**. New link without `Parents.View`: **403**. |
| `GET /api/admissions/parent-search?q=&take=` | New. At least 2 characters (or 3 digits), at most 10 results, live parents only, each with `linkedChildren`. 403 without `Parents.View` or without `Admissions.Create`/`Edit`. |
| `GET /api/admissions/{id}/guardian-matches` | Each guardian has `linkedParentId`. A linked parent is the first candidate with suggestion `UseExisting`, even if the number on the form differs; if it was deleted the suggestion is `ParentRemoved`. A guardian that shares its number with a parent linked to *another* guardian must choose. |
| `POST /api/admissions/{id}/enroll` | A linked parent that was deleted refuses the whole enrolment (422). A changed choice is written to the guardian row (below). |
| `AdmissionResponse.guardians[].parentId` | New. The application never returns children. |

## Audit of an override

At enrolment, if a guardian's registered parent differs from the parent the guardian became (another existing parent, or a new one), the server
sets `AdmissionGuardians.ParentId` to the final parent in the same transaction. The existing audit interceptor then writes an `Update` row on
`AdmissionGuardians` with `{"parentId":{"old":11,"new":12}}`, the user and the time. No separate audit code is involved.
Two guardians swapping parents would briefly break the unique index, so in that one case the old links are cleared and saved first (the audit shows `old -> null`, then `null -> new`).
Guardians that were not linked at registration are not touched.

## Migration and back-up

1. **Back up first** (SSMS): open `database/scripts/g1a-backup-before-migration.sql`, set `@label = N'G2'`, run it, and check it prints the verified file name.
2. Apply: `dotnet ef database update` (or start the API). `20261014150000_G2_AdmissionGuardianParentLink` only adds a nullable column, two indexes and a foreign key.
   Every existing guardian gets `ParentId = NULL`, so nothing changes for existing applications.
3. Roll back if needed: `dotnet ef database update 20261014130000_E2c_DropTeacherNameAndPhone` (drops the column and both indexes; the links chosen since are lost, which is why step 1 exists).

## Frontend

* Registration form, each guardian row: **Find an existing parent** (only with `Parents.View`), a linked row shows the details read-only with the children and **Use a different person** (clears the row).
* A typed mobile number that equals a parent's number shows a non-blocking hint with **Use this parent** (only with `Parents.View`; the same endpoint).
* Enrolment pop-up: the linked parent is pre-selected and labelled "chosen at registration"; changing it shows a note that the change is recorded; `ParentRemoved` shows the message and disables "Review enrolment".

## Tests

* `IntegrationTests/AdmissionExistingParentTests.cs`: permissions of the search, children only for `Parents.View`, copy overrides form values, 404 / 422 / 403, keeping an existing link without permission, matches (`UseExisting` first, `ParentRemoved`), enrolment reuses the parent, override audit (old, new, user), swap, deleted parent refuses and creates nothing.
* `DatabaseRules/AdmissionGuardianParentRulesTests.cs`: the unique index, NULLs allowed, the same parent on two applications, Restrict.
* `AccessMatrixTests`: the new endpoint.

## Not changed

Editing a parent's details still happens on the parent. An application linked to a parent shows the details as they were when it was last saved; the enrolment pop-up shows the parent's current details.
