# Parents — Database Design

Fifth module in the build sequence. This is where the Admission-to-Parent transition, left open in both the Admissions and Students design docs, gets resolved.

## Purpose & Scope

Verified directly against the prototype's `parents.js`: parents are one-per-guardian records, linked to students via a `children: [{ studentId, primary }]` array — confirming the `Parents` + `StudentGuardians` design already committed to in Decision #9.

Two deliberate departures from the prototype's exact shape:

1. **No `password` field on `Parent`.** The prototype gives each parent an independent plaintext password, which is exactly the credential-duplication pattern Decision #3 already ruled out (same reasoning that removed `Username`/`Password` from `Teachers` in favor of a `UserId` link). Login credentials live on `Users` only.
2. **`relation` (Father/Mother/Guardian) moves to `StudentGuardians`, not `Parent`.** The prototype stores it directly on the parent record, but Decision #9 already specified it belongs on the junction — a person could legitimately be "Guardian" to one child and "Father" to another in a blended family; the junction table is the more correct shape.

**Admission-to-Parent transition — now resolved.** Snapshot (Option A): `Admissions`' father/mother/guardian fields remain permanently as historical record, never migrated or deleted. Creating a Student from an admission does **not** automatically create Parent records — that stays a deliberate, separate action (a "Link/Create Parent" button on the Student, pre-filled from the admission's data as a convenience), for the same reason student portal login stays manual: both are consequential, portal-adjacent actions that shouldn't happen silently as a side effect of something else.

## Entity List

| Entity | Purpose | PK | Key FKs |
| --- | --- | --- | --- |
| `Parents` | Guardian profile — contact, work, notification preferences | Id | UserId→Users (nullable) |
| `StudentGuardians` | Many-to-many Student↔Parent, with relation type and primary-contact flag | (StudentId, ParentId) | StudentId→Students, ParentId→Parents |

## Schema

### Parents

| Column | Type | Null | Key | Notes |
| --- | --- | --- | --- | --- |
| Id | int | N | PK |  |
| UserId | int | Y | FK→Users | Nullable — portal access still opt-in per Decision #9 |
| Name | nvarchar(100) | N |  |  |
| Email | nvarchar(100) | Y |  |  |
| Mobile | nvarchar(20) | N |  |  |
| Status | nvarchar(20) | N |  | Active / Inactive |
| Occupation | nvarchar(100) | Y |  |  |
| Nationality | nvarchar(50) | Y |  |  |
| CountryOfResidence | nvarchar(50) | Y |  |  |
| Timezone | nvarchar(50) | Y |  |  |
| PreferredLanguage | nvarchar(30) | Y |  |  |
| PreferredContactMethod | nvarchar(30) | Y |  |  |
| Whatsapp | nvarchar(20) | Y |  |  |
| EmergencyOnly | bit | N |  | Default false |
| NotifyAttendance / NotifyExams / NotifyFees / NotifyNotices / NotifyDiscipline | bit | N |  | Individual typed columns, not a JSON blob — small fixed set |
| Employer / JobTitle / WorkEmail / WorkPhone | nvarchar | Y |  |  |
| BillingContact | bit | N |  | Default false |
| AddressLine / City / State / Pincode | nvarchar | Y |  |  |
| CreatedAt / UpdatedAt / CreatedBy / UpdatedBy / IsDeleted / DeletedAt / DeletedBy | — |  |  | Full audit + soft delete |

### StudentGuardians

| Column | Type | Null | Key | Notes |
| --- | --- | --- | --- | --- |
| StudentId | int | N | PK (composite), FK→Students |  |
| ParentId | int | N | PK (composite), FK→Parents |  |
| RelationType | nvarchar(30) | N |  | Father / Mother / Guardian |
| IsPrimaryContact | bit | N |  | Default false |

No uniqueness constraint on `Email`/`Mobile` — the prototype's own sample data has multiple parents with blank `email`; a unique index would break on the first duplicate.

## Index Strategy

| Index | Table.Columns | Why |
| --- | --- | --- |
| FK index | `Parents.UserId` | Join performance |
| PK (composite, effectively unique) | `StudentGuardians(StudentId, ParentId)` | Prevents duplicate links; also the lookup path for "who are this student's guardians" |
| Non-clustered | `StudentGuardians.ParentId` | Reverse lookup: "which students does this parent have" (the prototype's own primary use case — a parent portal showing their linked children) |

## Deferred Items & Open Decisions

- **Parent portal login provisioning** — still BUSINESS DECISION REQUIRED (carried from Decision #9): whether every linked parent gets a `Users` row from day one, or it's opt-in per guardian.
- **"Link/Create Parent" action** — not yet built; lives on the Students module once its frontend exists, pre-filled from the originating admission's father/mother/guardian data where one exists.
- **Notification preferences (`Notify*` columns)** — schema captured now, but nothing yet consumes them (no notice/exam/fee notification system built). Inert columns until those modules exist.
