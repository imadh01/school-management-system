# Substitutes: design (not built)

Status: design only. Written 19 Oct 2026. **Depends on Timetable** (periods, cells, the shared teacher-clash check). Do not build before Timetable is built and tested.

## 1. What the prototype does

Screen: Substitute Allocations.

- List with a status pipeline (All / Pending / In Progress / Completed) and columns date, absent teacher, reason badge, "allocated / total" periods, status, actions (Edit, Mark Completed, Delete).
- **New allocation, step 1:** pick the absent teacher, the date, a reason (Sick, Personal, Training, Emergency, Other), an optional description. "Load periods" shows that teacher's periods.
- **Step 2:** one card per period (label, class, subject) with a "Pick substitute" dropdown listing every teacher except the absent one. Save.
- **Edit** reopens step 2 with the saved periods and lets teacher, date, reason and substitutes change.
- **Status is derived:** Pending (no substitute chosen), In Progress (at least one), Completed (only when the user clicks the tick). Completed can be set with zero substitutes chosen.
- **Delete** is a hard delete after a confirm.
- Periods come from a static `TEACHER_SCHEDULE` that ignores the weekday ("independent of actual weekday").

## 2. Where this conflicts with what we built

1. **Schedule source.** The prototype's schedule is a demo constant. We will derive periods from the real Timetable for the weekday of the chosen date. This is the whole reason Substitutes comes after Timetable.
2. **The demo schedule is impossible in our model.** "Teacher 1" teaches Class 1 A, 1 B and 2 A in Period 1 at once. Timetable's teacher-clash rule forbids that. Treat it as demo data, not a requirement (decision 1).
3. **Teachers are names.** We use `TeacherId`. The "teacher" of a period is derived from the subject (`Subjects.TeacherId`), as agreed for Timetable.
4. **No availability check.** The prototype offers any teacher as substitute. A teacher who teaches another class in that period, or is already covering someone else, would be double-booked. We will reject that and show who is free.
5. **Edit can change teacher and date,** which would leave the saved periods belonging to the wrong day. We do not allow that (rule 4 below).
6. **Completed with nothing allocated** is allowed in the prototype. We do not allow it (decision 4).
7. **Hard delete** conflicts with our soft-delete and audit approach.
8. **Date and class labels** in the prototype are hard-coded (`2026-05-18`, "(2024-2025)"). The date defaults to today in the school time zone and the class is the real section.

## 3. Business rules

1. An allocation is for **one absent teacher on one date**. At most one live allocation per (teacher, date).
2. The absent teacher must be an Active teacher. The date must fall inside an academic year and be a **working day** (`SchoolSettings.WorkingDays`). Past dates: allowed back N days (decision 5).
3. On create, the server loads that teacher's **Teaching** periods for the weekday of the date, from the timetable of the academic year containing the date: every live cell whose subject's teacher is the absent teacher. No periods -> 422 "has no periods on that day". Break periods never appear.
4. After create, **teacher and date cannot change** (delete and recreate instead). Reason, description and the substitute on each line can change.
5. Each line is a snapshot: class section, period, subject, taken from the timetable at creation. If the timetable changes later, the line does not move. The screen shows a "timetable changed since" hint when the cell no longer matches (decision 6).
6. **Substitute eligibility** for a line (date D, period P, teacher T):
   - T is Active and is not the absent teacher;
   - T has no timetable cell in period P on D's weekday (T is not teaching);
   - T is not already substituting in period P on D (any other allocation);
   - T is not themselves absent on D in period P (their own allocation).
   Violations -> 409 naming the clash (class and period). Unassigning a line is always allowed.
7. The eligibility check and the save run in one transaction under `sp_getapplock('Substitute:{date}')`, using the same shared clash method as Timetable and Teachers. A filtered unique index is the safety net (section 4).
8. **Status:** `Pending` (no line covered), `In Progress` (some), `Completed` (explicit). Stored only as `CompletedAt/CompletedBy`; Pending / In Progress are computed in the query. Completing requires every line covered, otherwise 422 listing the uncovered periods. A completed allocation is read-only; Admin can reopen (decision 4).
9. **Delete:** soft delete; refused for Completed allocations. Deleting frees the substitute teachers' slots.
10. Attendance interplay is **not** changed by this module (decision 7).

## 4. Database

`SubstituteAllocations` (header)

| Column | Type | Notes |
|---|---|---|
| `Id` | int PK | |
| `AbsentTeacherId` | int FK Teachers | Restrict |
| `AllocationDate` | date | |
| `AcademicYearId` | int FK | year containing the date, for filtering |
| `Reason` | nvarchar(20) | CHECK in (Sick, Personal, Training, Emergency, Other) |
| `Description` | nvarchar(500) null | |
| `CompletedAt`, `CompletedBy` | datetime2 null, int null | null = not completed |
| audit columns, `RowVersion`, `IsDeleted/DeletedAt/DeletedBy` | | same as other masters |

Unique `(AbsentTeacherId, AllocationDate)` filtered to `IsDeleted = 0`. Index `(AllocationDate)` for the list by date; index `(AcademicYearId, AllocationDate)`.

`SubstituteAssignments` (lines)

| Column | Type | Notes |
|---|---|---|
| `Id` | int PK | |
| `AllocationId` | int FK | Cascade is **not** used; Restrict, lines are soft deleted with the header |
| `AllocationDate` | date | copy of the header date so a unique index can use it. Never edited (rule 4) |
| `ClassSectionId`, `PeriodId`, `SubjectId` | int FK | snapshot of the cell |
| `SubstituteTeacherId` | int null FK Teachers | null = not covered yet |
| audit columns, `RowVersion`, `IsDeleted` | | |

Unique `(AllocationId, ClassSectionId, PeriodId)` live rows. **Unique `(SubstituteTeacherId, AllocationDate, PeriodId)` where `SubstituteTeacherId IS NOT NULL AND IsDeleted = 0`**: the database-level guarantee that one teacher covers one slot once. No `TeacherId` on the line for the absent teacher: it is the header's.

No stored status column, no stored count.

## 5. Endpoints

| Method and path | Purpose |
|---|---|
| `GET /api/substitutes` | paged list; `status`, `teacherId` (absent or substitute), `from`, `to`, `search`; returns covered/total counts |
| `GET /api/substitutes/{id}` | header + lines |
| `GET /api/substitutes/preview?teacherId=&date=` | the periods that would be created, and for each period the eligible and busy teachers (with the reason a teacher is busy). Nothing is saved |
| `POST /api/substitutes` | create header + lines (+ optional substitutes) in one transaction |
| `PUT /api/substitutes/{id}` | reason, description, substitutes per line; `rowVersion` required |
| `POST /api/substitutes/{id}/complete` | 422 if lines are uncovered |
| `POST /api/substitutes/{id}/reopen` | Admin only |
| `DELETE /api/substitutes/{id}` | soft delete; 409 if Completed |

Errors: 404 unknown id, 409 clash / duplicate (teacher, date) / stale version, 422 rule violations (non-working day, no periods, uncovered lines), 400 validation.

## 6. Permissions

New module `Substitutes`: `View`, `ViewScoped`, `Create`, `Edit`, `Complete`, `Delete`.

- Admin: all. Supervisor: View, Create, Edit, Complete (not Delete). Clerk: View.
- Teacher: `Substitutes.ViewScoped` = allocations where they are the **substitute** (their own duties) or the absent teacher. No create/edit.
- Student, Parent: none.
- The preview endpoint needs `Substitutes.Create`.
- Needs catalog entries, role seed, `ExpectedRoleMatrix` and `AccessMatrixTests` rows (the guard test requires one per action) and the catalog-sync migration.

## 7. Frontend

Same layout as the prototype: status pipeline, table, two-step modal. Step 1 date defaults to today (school time zone). Step 2 replaces the free dropdown with a searchable select showing only eligible teachers, busy ones greyed with the reason ("Teaching Class 3 B"). Confirm dialogs for Complete and Delete; toasts for results. No new shared components.

## 8. Tests to write

Create loads exactly the absent teacher's teaching periods for that weekday; no periods -> 422; non-working day -> 422; duplicate (teacher, date) -> 409; substitute who is teaching that period -> 409; substitute already covering that period -> 409; two simultaneous saves picking the same substitute for the same slot -> exactly one succeeds; complete with an uncovered line -> 422; completed allocation cannot be edited or deleted; teacher/date cannot be changed on PUT; teacher role sees only own duties; each role's access matrix; break periods never appear.

## Decisions for you

1. **Demo schedule:** confirm "one teacher in three classes in the same period" was only demo data and the clash rule stands.
2. **Who is the "teacher of a period"?** Derived from the subject (as agreed). A cell whose subject has no teacher produces no line. OK?
3. **Substitute from any teacher** or only teachers who teach the same subject or class? Recommended: any free Active teacher (the prototype allows any).
4. **Complete rule:** require all lines covered (recommended), or allow completing with uncovered periods (then they are recorded as "not covered")? Who may reopen: Admin only?
5. **How far back may an allocation be created** (0 days, 7, unlimited)? Recommended: 7 days; older needs Admin.
6. **Timetable changes after creation:** keep the snapshot and show a hint (recommended), or let the user press "Refresh periods"?
7. **Attendance:** should the substitute be allowed to mark attendance for the class in that period on that date? Today only class teacher / subject teacher can (data scope). Recommended: yes, as a later change to `DataScopeQueries`, not in this module. Confirm or say no.
8. **Teacher absence/leave:** there is no leave module in the prototype. Is "reason" enough, or will a Leave module be planned (an allocation would then link to a leave record)?
9. **Notifications** to the substitute teacher: not in the prototype; out of scope unless you say otherwise.
10. **Who creates allocations:** Admin and Supervisor only (recommended), or Clerk too?
