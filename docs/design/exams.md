# Exams: design (not built)

Status: design only. Written 19 Oct 2026. First part of the chain **Exams -> Results/Marks -> Hall Tickets**. This document designs **Exams** fully and only sketches the other two so the tables line up.

## 1. What the prototype does

**Exams screen**

- Table of exams: id, name + type, code, term, assessment (Formative/Summative), class, start -> end, weight %, scheme (P/G), duration, max marks per subject, status (Draft/Published) "by Admin User", lock date, actions.
- Filters: search (name, code), type, class, term, assessment, scheme, and a status pipeline (All / Published / Draft).
- Fields on the form: name*, code, type (Unit Test, Quarterly, Mid Term, Half Yearly, Annual), class*, year, start*, end*, max marks per subject, term (Term 1-3), assessment (Formative/Summative), curriculum stage (Primary/Secondary), grading scheme (Percentage/GPA), weight %, duration (min), lock date. The form also has report-card, next-exam id and sections inputs that the save code never reads.
- Actions: Add, Edit, Delete (hard, confirm), Publish / Unpublish (toggle Draft <-> Published), Enter Marks (jumps to Results), and three read-only views built from marks: **Top Performers**, **Grade Distribution**, **Class Marksheet** (printable).
- Result rules hard-coded in the script: grade bands (A+ >= 90, A >= 80, B+ >= 70, B >= 60, C >= 50, D >= 33, F below), pass at 33 %, an absent subject counts 0 toward the total and the student becomes **INCOMPLETE** (grade "AB", no rank), ranking only complete students.

**Results / Marks Entry screen:** list of exam cards with a marks status (Draft -> Submitted -> Moderated -> Locked -> Published); an Enter Marks dialog: choose one subject, then per student a score (out of the exam's max), attempt (1-3), absent tick, remarks, comments (max 500); Save all.

**Hall Tickets screen:** choose an exam (fixes the class), search the class's students, Download one or Generate All. A ticket shows school, student, admission number, roll, class, exam, code, dates, duration, room, subject list.

## 2. Where this conflicts with what we built

1. **Class and year are text** ("Class 1 A (2024-2025)", a separate year dropdown). We have `ClassSectionId`, which already carries the academic year. The year field is removed (derived).
2. **One exam row per class section** with a repeated code (`QT1-2024` on both 1 A and 1 B). Keep that shape (decision 1) but the code uniqueness is per class section, not global.
3. **Marks per subject:** the prototype has one `maxMarksPerSubject` for all subjects. Our `Subjects` already store `MaxMarks`, `PassMarks`, theory/practical splits. Two sources of truth (decision 3).
4. **Two "publish" ideas.** The Exams screen publishes an exam (Draft/Published); Results has a five-step marks status ending in Published. In the sample data they always match, so they may be one lifecycle seen from two pages, or two different things (schedule visible vs results visible). Decision 2.
5. **Grading is code, not data:** bands, pass mark 33 %, "GPA" option with no GPA logic anywhere. Needs a home (decision 4).
6. **Pass mark** in the prototype is 33 % of the total; our subjects carry their own `PassMarks`.
7. **Terms:** the prototype has Term 1-3 on exams, while the Timetable decision was "no terms". They do not conflict: term is a label on the exam only, never an entity (decision 6).
8. **Curriculum stage** (Primary/Secondary) is a property of the class, not of an exam (decision 7).
9. **Weight %** is stored and shown but nothing consumes it (it would feed a report card, which has no screen). Keep as data only.
10. **Created/Published "by Admin User"** is hard-coded; ours comes from audit columns.
11. **Hall ticket subjects have no dates:** only an exam date range and one room. There is no exam timetable (per-subject date and time) in the prototype. We do not invent one (decision 8).
12. **Top performers / grade distribution / marksheet** need marks. They are built with Results, not with Exams (decision 9).
13. **Alert-based validation and hard delete** are replaced by field errors, confirm dialogs and soft delete.

## 3. Business rules (Exams module)

1. **Create:** class section must be live and in an academic year that is not Closed. Name required (<= 100). Code optional (<= 30), unique per class section among live exams when present (or generated, decision 5). `StartDate <= EndDate`; both inside the class's academic-year dates. `LockDate`, if set, is on or after `EndDate`. Weight 0-100 (2 decimals). Duration 1-600 minutes. Default max marks > 0.
2. **Subjects:** creating an exam adds one `ExamSubjects` row for every **Active** subject of the class, copying max/pass from the exam default or the subject (decision 3). Rows can be edited (max, pass with pass <= max) and subjects can be removed/added while the exam is Draft. A class with no subjects -> 422.
3. **Edit:** all fields while Draft. While Published: only dates, duration, lock date and description-type fields; class, subject list and max marks are frozen. Once marks exist (Results module), max marks and subject list are frozen even in Draft.
4. **Publish / Unpublish:** Draft -> Published needs at least one subject and dates set. Unpublish is refused once marks have moved beyond Draft (checked when Results exists).
5. **Delete:** soft delete. Refused when the exam is Published or any marks exist (409 with the count). Deleting a Draft with no marks is allowed.
6. **Locking:** after `LockDate` (school time zone) marks entry is refused except by a user holding `Results.Unlock`. Enforced by the Results module; Exams only stores the date.
7. **Exam clash:** the prototype does not forbid two exams of one class overlapping in dates (its sample data never does). No rule until you decide (decision 10).
8. **Year rule:** an exam belongs to its class's year. When the year is Closed the exam is read-only.
9. **Teachers:** do not edit exams. A teacher sees exams of classes they teach or are class teacher of (scoped).

## 4. Database

`Exams`

| Column | Type | Notes |
|---|---|---|
| `Id` | int PK | |
| `ClassSectionId` | int FK | Restrict; implies the academic year |
| `Name` | nvarchar(100) | |
| `Code` | nvarchar(30) null | |
| `ExamType` | nvarchar(20) | CHECK in (Unit Test, Quarterly, Mid Term, Half Yearly, Annual), or a reference table (decision 5) |
| `Term` | tinyint null | CHECK 1-3 |
| `AssessmentType` | nvarchar(20) | CHECK in (Formative, Summative) |
| `GradingScheme` | nvarchar(20) | CHECK = 'Percentage' until GPA is defined |
| `WeightPercent` | decimal(5,2) | CHECK 0-100, default 0 |
| `DurationMinutes` | smallint | CHECK 1-600 |
| `DefaultMaxMarks` | decimal(6,2) | CHECK > 0 |
| `StartDate`, `EndDate` | date | CHECK start <= end |
| `LockDate` | date null | CHECK >= EndDate |
| `Status` | nvarchar(20) | CHECK in (Draft, Published); default Draft |
| `PublishedAt`, `PublishedBy` | datetime2 null, int null | |
| audit columns, `RowVersion`, `IsDeleted/DeletedAt/DeletedBy` | | |

Unique `(ClassSectionId, Code)` where `Code IS NOT NULL AND IsDeleted = 0`. Index `(ClassSectionId, StartDate)` for "exams of a class" and the list. `CurriculumStage` is **not** a column (decision 7).

`ExamSubjects`

| Column | Type | Notes |
|---|---|---|
| `Id` | int PK | |
| `ExamId` | int FK | Restrict |
| `SubjectId` | int FK | must belong to the exam's class section (service rule) |
| `MaxMarks` | decimal(6,2) | CHECK > 0 |
| `PassMarks` | decimal(6,2) | CHECK 0 <= pass <= MaxMarks |
| audit, `RowVersion`, `IsDeleted` | | |

Unique `(ExamId, SubjectId)` live rows. Marks use `decimal(6,2)`, not int: half marks happen.

**Sketch of the next two modules (not designed here):**
`ExamMarks (Id, ExamSubjectId, StudentEnrollmentId, Score decimal(6,2) null, IsAbsent, Attempt, Remarks, Comments, ...)` tied to the **enrollment** (so results stay with the class the student was in), unique on (ExamSubjectId, StudentEnrollmentId, Attempt); plus `Exams.MarksStatus` (Draft, Submitted, Moderated, Locked, Published) added by the Results migration, never earlier. Hall tickets store nothing: they are rendered from the exam, its subjects, and the class's Active enrollments.

No migration touches the existing tables. Pre-check: none needed (new tables). The `Down` drops both tables. Then `dotnet ef migrations add Check`.

## 5. Endpoints

| Method and path | Purpose |
|---|---|
| `GET /api/exams` | paged; `search`, `classSectionId`, `academicYearId`, `type`, `term`, `assessment`, `status`, sort; pipeline counts returned with the page |
| `GET /api/exams/{id}` | exam + its subjects |
| `POST /api/exams` | create (+ subjects) |
| `POST /api/exams/bulk` | same exam for several class sections in one transaction (decision 1) |
| `PUT /api/exams/{id}` | edit; `rowVersion` |
| `PUT /api/exams/{id}/subjects` | replace max/pass and the subject list while Draft |
| `POST /api/exams/{id}/publish`, `/unpublish` | status changes |
| `DELETE /api/exams/{id}` | soft delete |

Later (other modules): `GET /api/results/...` (marks, top performers, grade distribution, marksheet), `GET /api/hall-tickets/exams/{id}` (list) and `.../{studentEnrollmentId}` (ticket data; printing stays in the browser).

## 6. Permissions

Module `Exams`: `View`, `ViewScoped`, `Create`, `Edit`, `Delete`, `Publish`.

- Admin: all. Supervisor: View, Create, Edit, Publish. Clerk: View. Teacher: ViewScoped. Student, Parent: none now (published exams for own class/child is decision 11).
- Results and Hall Tickets get their own modules later (`Results.View/Enter/Moderate/Lock/Publish/Unlock`, `HallTickets.View`).
- Needs catalog, role seed, `ExpectedRoleMatrix`, `AccessMatrixTests` rows, catalog-sync migration.

## 7. Frontend

Same table, filters, status pipeline and add/edit modal as the prototype, using searchable selects for class and subject lists, confirm dialogs and toasts. The year input disappears (shown read-only from the class). The four result-based buttons (Enter Marks, Top Performers, Grade Distribution, Marksheet) are **not shown** until Results exists (decision 9). Currency does not appear on this screen.

## 8. Tests to write

Date range outside the year; start after end; lock date before end; weight and duration limits; code unique per class (but the same code on another class is fine); subjects auto-created from Active subjects only; pass above max refused; edit frozen fields while Published; publish without subjects refused; delete of Published refused; soft-deleted code can be reused; stale rowVersion 409; teacher scope shows only own classes' exams; role matrix rows; Closed-year exam read-only.

## Decisions for you

1. **Shape:** one exam row per class section (as the prototype) with a "create for several classes" action (recommended), or a single exam event linked to many classes?
2. **Publish meaning:** are the Exams-screen Publish and the Results "Published" the same thing? Recommended: two separate states. Exam Published = schedule visible; marks status Published = results visible to students/parents.
3. **Max/pass marks:** take them from the exam default, from each subject's `MaxMarks/PassMarks`, or copy subject values and allow override per exam (recommended)? Is the 33 % rule a school rule or just demo?
4. **Grading:** keep the A+..F bands and 33 % pass as a configurable grade scale (a small settings/masters table) instead of code? Is GPA needed now? Recommended: Percentage only; hide "GPA".
5. **Reference lists:** exam types, terms and assessment types as fixed lists (recommended) or Masters tables? Is the exam code typed by staff or generated (like `UT1-2026`)?
6. **Terms:** keep Term 1-3 as a label on the exam. Does the school really use three terms?
7. **Curriculum stage (Primary/Secondary):** drop from Exams and derive from the grade, or keep a field?
8. **Hall ticket content:** the prototype shows the date range, one room and a subject list. Is that enough, or do you need a date, time and room per subject (an exam timetable, new table)? Room: the class's room (consistent with Timetable decision 2)?
9. **Result-based views** (Top Performers, Grade Distribution, Marksheet): built with Results, and hidden until then. OK?
10. **Overlap:** may a class have two exams with overlapping dates (for example a practical during a written)?
11. **Student/Parent view:** may they see Published exams of their own class/child (and later hall tickets)? Not built now; the permission design depends on it.
12. **Hall-ticket eligibility:** should a student with unpaid fees or poor attendance be refused a hall ticket? Not in the prototype; I assume no.
13. **Ignored form fields** (report card, next exam id, sections): drop them? Recommended: drop.
14. **Absent rule:** an absent subject makes the student INCOMPLETE with no rank (prototype). Keep? This matters for Results.
