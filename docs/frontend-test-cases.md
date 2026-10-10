# Frontend Test Cases (Full UI)

Manual test script for the React app (`http://localhost:5173`), screen by screen, followed by
end-to-end flows and a plan for automating them. Total: **~190 manual cases**.

**Before you start**

1. API running (`https://localhost:7100`), frontend running (`npm run dev`).
2. Run `npm run build` once: it must finish with **no TypeScript errors**.
3. Open browser DevTools → Console + Network. Every test below also implies: **no red console errors** and **no 500 responses**.
4. Users needed: **Admin**, `zt.clerk` (Clerk), `zt.supervisor` (Supervisor), `zt.teacher1` (Teacher, class teacher of "ZT Class A"). Create them with the Swagger steps in `swagger-test-cases.md` §2 and §5.
5. Test data prefix: **ZT**.

**Legend**: ✅ pass · ❌ fail · ⏭ skipped. Each ID can be copied into a spreadsheet.

---

## 1. Login, session and layout

| ID | Steps | Expected |
|---|---|---|
| LOG-01 | Open `/` while logged out | Redirected to `/login` |
| LOG-02 | Open `/students` directly while logged out | Redirected to `/login` |
| LOG-03 | Login with username + correct password | Lands on Dashboard, header shows "Welcome, username" |
| LOG-04 | Login with email instead of username | Works |
| LOG-05 | Wrong password | Message "Invalid username/email or password."; stays on login; button re-enabled |
| LOG-06 | Submit empty form | Fields flagged / error shown, no crash |
| LOG-07 | Click Sign in twice quickly | Button disabled while submitting; one request only |
| LOG-08 | Refresh the page after login | Still logged in (session in localStorage) |
| LOG-09 | Delete `localStorage` entry manually and navigate | Redirected to login |
| LOG-10 | Let the token expire (or edit `expiresAtUtc` to the past) and reload | Session cleared, login shown |
| LOG-11 | Make any API call return 401 (e.g. tamper token in storage) | App redirects to `/login` automatically |
| LOG-12 | Click Logout in the sidebar | Back to login; browser Back button does not show protected pages |
| LAY-01 | Sidebar sections: Overview, Records, Daily, … | Visible; the current page is highlighted |
| LAY-02 | Click each built item: Dashboard, Admissions, Students, Classes & Sections, Parents, Subjects, Teachers, Attendance | Correct page opens, URL changes |
| LAY-03 | Click a not-yet-built item (Reports, Timetable, …) | Redirects to dashboard without error (known placeholder) |
| LAY-04 | Dark-mode toggle | Theme switches; all pages readable in both; choice survives refresh |
| LAY-05 | Narrow the window to phone width | Layout usable, tables scroll horizontally, no clipped modals |
| LAY-06 | Open an unknown URL `/xyz` | Redirected to dashboard |

## 2. Reusable components (check them wherever they appear)

| ID | Component | Expected behaviour |
|---|---|---|
| CMP-01 | ValidationModal | Lists every server validation message (from `errors`); closes with the button/✕; no raw JSON shown |
| CMP-02 | ValidationModal on 409/422 | Shows the server `message` (e.g. "is full (1/1)") |
| CMP-03 | ConfirmDialog | Cancel keeps data; Confirm performs the action once |
| CMP-04 | ViewDetailsModal | Shows all sections, empty values as "—", closes cleanly |
| CMP-05 | Table footer | "Showing x–y of N" correct; page-size 10/25/50 changes rows; Prev/Next disabled at ends |
| CMP-06 | Quick search box above the table | Filters rows live, resets to page 1 |
| CMP-07 | Export CSV | File downloads, opens in Excel, header + rows match what is filtered on screen |
| CMP-08 | Modals | Esc/✕ closes where supported; background does not scroll; focus lands in the first field |
| CMP-09 | Network offline / API stopped | Page shows an error state or message, not a blank screen or endless spinner |

## 3. Class Sections (`/classes-sections`)

| ID | Steps | Expected |
|---|---|---|
| CLS-U01 | Open the page | List loads with Academic Year, Section, Stage, Medium filters populated |
| CLS-U02 | Click **Add Class**; leave everything empty; Save | Required-field messages; nothing created |
| CLS-U03 | Fill Name "ZT Class", Section "A", Stage Primary, Medium English, Stream General, Capacity 30 | Created; appears in the list with display name "ZT Class A (2026-2027)"; code auto-generated |
| CLS-U04 | Add the same class again | Error modal about duplicate |
| CLS-U05 | Capacity 0 / 201, Grade 13, Floor 21 | Validation messages |
| CLS-U06 | Leave Grade blank (LKG/UKG case) | Accepted |
| CLS-U07 | Edit: change Room | Saved; reflected in the list |
| CLS-U08 | Edit: capacity lower than current students | Error "Capacity cannot be lower than the current strength" |
| CLS-U09 | Toggle Active off in the edit form | Row shows Inactive; the class no longer appears in admission/student class dropdowns |
| CLS-U10 | View (👁) | Details modal shows enrolled count and teacher info |
| CLS-U11 | Each filter (year, section, stage, medium) and the search box | List narrows correctly; combining filters works; clearing restores all |
| CLS-U12 | Delete an empty class | Confirm dialog, then row disappears |
| CLS-U13 | Delete a class that has students/subjects | Error message "Mark it Inactive instead" |
| CLS-U14 | As Clerk: add/edit/delete | Server returns 403 and the UI shows a clear message (see AZ section) |

## 4. Subjects (`/subjects`)

| ID | Steps | Expected |
|---|---|---|
| SUB-U01 | Open the page | List + filters (Class, Academic Year, Type) |
| SUB-U02 | Add Subject: name "ZT Math", class "ZT Class A", type Theory, max 100, pass 35 | Created |
| SUB-U03 | Switch Type to **Both** | Theory max/pass and Practical max/pass fields appear; single max/pass hidden |
| SUB-U04 | Type Both, leave Theory max empty | Client message; nothing sent |
| SUB-U05 | Pass marks greater than max marks | Validation message |
| SUB-U06 | Leave Code empty | Code auto-generated from name |
| SUB-U07 | Duplicate code in the same class | Error modal "Subject Code + Class must be unique" |
| SUB-U08 | Edit changes marks | Saved |
| SUB-U09 | Filters and search | Work and combine |
| SUB-U10 | Delete subject | Confirm → removed |
| SUB-U11 | Subject assigned to a teacher shows the teacher name | Yes |

## 5. Teachers (`/teachers`)

| ID | Steps | Expected |
|---|---|---|
| TCH-U01 | Open the page | Stat tiles (All/Active/Inactive/Suspended) with correct counts; list loads |
| TCH-U02 | Click a status tile | List filters to that status |
| TCH-U03 | Add Teacher with all fields and a strong password | Created; counts update |
| TCH-U04 | Weak password `abc` | Message lists exactly what is missing (8 chars, uppercase, digit, special) |
| TCH-U05 | Password empty on create | "Login password is required." |
| TCH-U06 | Duplicate username/email | Error modal |
| TCH-U07 | Edit teacher: leave password blank | Saved; old password still works |
| TCH-U08 | Edit with a new password | Saved; can log in with it |
| TCH-U09 | Change status to Suspended | Tile counts update; that teacher can no longer log in |
| TCH-U10 | Open **Teacher Profile & Assignments** | Stat tiles; assigned subjects grouped by class |
| TCH-U11 | Pick a class → subject checklist shows only that class's subjects | Yes; a subject already taught by another teacher is disabled with "Already taught by <name>" |
| TCH-U12 | Assign subjects | Appear in the profile |
| TCH-U13 | Tick "Assign as Class Teacher" while assigning | Class shows ★ Class Teacher |
| TCH-U14 | Make Class Teacher on a class that already has one | Error naming the current class teacher |
| TCH-U15 | Unset class teacher | ★ removed |
| TCH-U16 | Remove a subject (🗑 on the subject) | Unassigned |
| TCH-U17 | Delete a teacher who still has assignments | Error with counts |
| TCH-U18 | Delete a teacher with no assignments | Confirm → removed; login no longer works |
| TCH-U19 | Search by name/username/email | Filters |

## 6. Parents (`/parents`)

| ID | Steps | Expected |
|---|---|---|
| PAR-U01 | Open | List, status tiles, Contact Method filter |
| PAR-U02 | Add Parent with only name + mobile | Created |
| PAR-U03 | Add without mobile / invalid email | Messages |
| PAR-U04 | Fill all sections (contact prefs, notification checkboxes, work info, address) | Saved; edit shows the same values |
| PAR-U05 | Status Inactive | Counts update |
| PAR-U06 | View (👁) | Details correct |
| PAR-U07 | **Linked Children** (👨‍👩‍👧) | Lists linked students with relation and primary star; empty state when none |
| PAR-U08 | Link a student from the parent side (search student, relation, primary) | Appears in the list |
| PAR-U09 | Link the same student again | Error "already linked" |
| PAR-U10 | Make another parent primary | Star moves; only one primary |
| PAR-U11 | Unlink | Confirm → removed |
| PAR-U12 | Fee Records button | Opens the "not yet available" style modal (module not built) |
| PAR-U13 | Delete a parent | Confirm → removed; the student's Linked Parents list no longer shows them |
| PAR-U14 | Email field label says "unique when set" — enter an email that another parent has | **Currently accepted** (documented gap); note the result |
| PAR-U15 | Filters, search | Work |

## 7. Admissions (`/admissions`)

### 7a. List and pipeline

| ID | Steps | Expected |
|---|---|---|
| ADM-U01 | Open | Pipeline tabs: All, Registered, Admitted, Enrolled, Rejected, each with a count |
| ADM-U02 | Click each tab | Rows filter; counts match |
| ADM-U03 | Filters: Academic Year, Admission Type, Applied-for Class, date from/to, search | Work and combine |
| ADM-U04 | Row actions per status | Registered: view, edit, confirm, reject, delete. Admitted: view, edit, enrol, reject, delete. Enrolled: view, edit, 🔗 (student record created), delete — no confirm/enrol/reject. Rejected: view rejection reason, delete |
| ADM-U05 | Export CSV | Matches filtered rows |

### 7b. Register

| ID | Steps | Expected |
|---|---|---|
| ADM-U10 | Click **+ New Registration**; Save empty | Required-field messages |
| ADM-U11 | Fill applicant, class (only Active classes listed), type, phone | Saved; row appears under Registered with REG-… number |
| ADM-U12 | Add a Father and a Mother row (**+ Add guardian**) | Rows appear with relation/name/mobile/email/primary radio |
| ADM-U13 | Try to add a 6th guardian | **+ Add guardian** is disabled at 5 |
| ADM-U14 | Two fathers | Server message "only one father and one mother" |
| ADM-U15 | Primary radio: choose Mother | Only one primary at a time |
| ADM-U16 | Remove a guardian row | Row disappears |
| ADM-U17 | Invalid DOB (future) | Error |
| ADM-U18 | Register with **no** guardians | Allowed |

### 7c. Edit, confirm, reject

| ID | Steps | Expected |
|---|---|---|
| ADM-U20 | Edit an application; change phone, replace guardians | Saved; reopening shows the new values |
| ADM-U21 | **Confirm Admission** on Registered: fee 500, reference, blood group, religion, category, notes | Moves to Admitted; fee visible in details |
| ADM-U22 | Fee negative | Error |
| ADM-U23 | Religion/Category are fixed lists | No free text |
| ADM-U24 | **Reject** → modal asks for a reason (no browser prompt) | Empty reason blocked; saved reason shown with 👁 "View Rejection Reason" |
| ADM-U25 | Reject an Enrolled application | Action not offered |
| ADM-U26 | Delete an application | Confirm → disappears |

### 7d. Enrol

| ID | Steps | Expected |
|---|---|---|
| ADM-U30 | On an Admitted application with Father+Mother whose mobiles match existing parents, click **Enroll to Class** | Modal loads guardian matches; each guardian pre-selected "Use existing: <parent>" |
| ADM-U31 | Guardian mobile matches nobody | Pre-selected "Create new parent" |
| ADM-U32 | Two parents share the mobile | **No** pre-selection; staff must choose (or "Search another parent…") |
| ADM-U33 | Guardian without mobile | Enrol button blocked with message |
| ADM-U34 | Choose the same existing parent for two guardians | Blocked with message |
| ADM-U35 | "Search another parent…" | Search list of all parents; selecting one sets the decision |
| ADM-U36 | Fill roll number, admission number, date, class (Active classes only), optional fields | |
| ADM-U37 | Roll number already used in that class | Error modal "already taken" |
| ADM-U38 | Class is full | Error modal "is full (n/n)" |
| ADM-U39 | Submit valid enrolment | Application becomes **Enrolled** with 🔗; student appears in Students; Parents list shows new parents if CreateNew was chosen |
| ADM-U40 | Network failure mid-submit | Error shown; **no** partial student/parent created (check Students and Parents lists) |
| ADM-U41 | Double-click the Enrol button | Only one student created |

## 8. Students (`/students`)

### 8a. List

| ID | Steps | Expected |
|---|---|---|
| STU-U01 | Open | Pipeline tabs: All, Active, Inactive, Left with counts |
| STU-U02 | Filters: Class, Gender, Curriculum, English Level, House, "has allergies" | Work and combine; allergies filter uses the health data |
| STU-U03 | Search (header + quick search) | Filters by name/admission no. |
| STU-U04 | Row action tooltips | View, Fee Summary, Attendance Report, Report Card, Linked Parents, Academic History, Edit, Delete |
| STU-U05 | Fee Summary / Attendance Report / Report Card | "Not yet available" modal naming the missing module |
| STU-U06 | Export CSV | Correct columns |
| STU-U07 | Left students | Shown under All and Left only, with a purple badge |

### 8b. View / Edit / Create

| ID | Steps | Expected |
|---|---|---|
| STU-U10 | 👁 View a student enrolled via admission | Sections Basics, Academic, health, parents, etc. all filled; blood group from health |
| STU-U11 | Open **Edit** | All sections load from the **detail** endpoint (health, pickup persons, identity) |
| STU-U12 | Edit personal data and Save | Reflected in list and view |
| STU-U13 | Parents/Guardians section in the modal | **Read-only**, with a hint to manage links from Parents |
| STU-U14 | Add two pickup persons (name, relation, phone, ID note) | Saved; shown on reopen |
| STU-U15 | Invalid phone on pickup person | Validation message |
| STU-U16 | Remove all pickup persons and save | Gone on reopen |
| STU-U17 | Fill health section (blood group, allergies, notes) | Saved; allergies chip/filter works |
| STU-U18 | Identity Documents section as **Admin/Supervisor** | Editable; saving uses the identity endpoint; reopened values full |
| STU-U19 | Identity Documents as **Clerk** | Values masked (`********9012`), fields **not editable** |
| STU-U20 | Aadhaar not 12 digits | Message |
| STU-U21 | Aadhaar already used by another student | Error "already recorded for another student" |
| STU-U22 | Add Student (standalone) with required fields | Created with first history row |
| STU-U23 | Duplicate adm. number / roll number | Error modals |
| STU-U24 | Status options are only Active / Inactive / Left | Yes |
| STU-U25 | Change class in Edit to another **same-year** class and Save | List shows the new class; 📚 shows two rows (Transferred + Active) |
| STU-U26 | Change class to inactive/full class | Error modal |
| STU-U27 | Class dropdown in Edit | Lists every Active class (display name includes the year) plus the student's current class; picking a class of another academic year and saving → error about year-end promotion |
| STU-U28 | Set status Left and change class together | Error "cannot be moved… and marked as Left in the same edit" |
| STU-U29 | Set status Left | Row moves to Left; 📚 shows the closing row `Left` |
| STU-U30 | Set back to Active | New Active row in 📚 |
| STU-U31 | Delete student | Confirm → removed from the list; its parent links disappear |

### 8c. History and parents modals

| ID | Steps | Expected |
|---|---|---|
| STU-U40 | 📚 on a normal student | One row: academic year, class, roll, from, "—", `Active` badge |
| STU-U41 | After a class change | Two rows, newest first; old row has end date, `Transferred` and remark "Moved to …" |
| STU-U42 | Modal is read-only | No edit controls |
| STU-U43 | Loading and failure states | "Loading…" then data; with API stopped → "Could not load the history." |
| STU-U44 | 👪 Linked Parents | Lists parents with relation, ★ primary, mobile/email; empty state when none |

## 9. Attendance (`/attendance`)

| ID | Steps | Expected |
|---|---|---|
| ATT-U01 | Open as Admin | Tabs Daily / Subject; Class, Date (today), Search |
| ATT-U02 | Pick a class | Roster of Active students with roll no., name, adm no., 5 status buttons, remarks, last-7 dots |
| ATT-U03 | Subject tab, class with no subjects | Message "No active subjects" |
| ATT-U04 | Subject tab without choosing a subject | Roster not loaded / prompt to choose |
| ATT-U05 | **Mark All Present** | Everyone Present; counters update |
| ATT-U06 | Mark All Absent | Everyone Absent |
| ATT-U07 | Change individual statuses; add remarks | Counters (Present/Absent/Late/Half Day/Leave) update live |
| ATT-U08 | Save with a student unmarked | Error "N student(s) have no status yet" (or Save disabled until complete — record which) |
| ATT-U09 | Save | Banner "✓ Saved by <user> · <time>"; Save disabled until something changes |
| ATT-U10 | Reload the page / re-select the class and date | Saved statuses come back |
| ATT-U11 | Change a status and save again | Same session updated; banner time changes |
| ATT-U12 | **Copy from previous day** | Statuses copied from the previous session date; button disabled when none exists |
| ATT-U13 | Search students by name/admission/roll | Filters visible rows; hidden rows keep their status and are still saved |
| ATT-U14 | Unsaved changes then change class/date/tab | Confirm "Discard unsaved changes?" |
| ATT-U15 | Unsaved changes then close/refresh the browser tab | Browser "leave site?" warning |
| ATT-U16 | Future date | Banner 🔒 "cannot be marked for a future date"; buttons disabled |
| ATT-U17 | 8+ days ago as **Clerk** | 🔒 "Only an Admin can change attendance older than 7 days"; Admin can edit |
| ATT-U18 | Login as `zt.teacher1`, class A, Daily | Editable |
| ATT-U19 | Teacher on a class they are not class teacher of | 🔒 banner, read-only |
| ATT-U20 | Teacher on Subject tab for a subject they do not teach | 🔒 banner |
| ATT-U21 | Student moved out of the class after the date | Still shown for the old date; absent for new dates |
| ATT-U22 | Left student recorded earlier | Marked as not active in class (greyed) on old dates |
| ATT-U23 | Daily and Subject attendance for the same day | Independent sessions |
| ATT-U24 | Large class (40+ students) | Scrolls smoothly; save < 2 s |

## 10. Dashboard

| ID | Steps | Expected |
|---|---|---|
| DSH-01 | Open | "Welcome, <username>"; currently only a greeting (no widgets built) |

## 11. Role-based behaviour (UI)

Today the UI **does not hide** buttons by permission; the API refuses (403). Verify the message is friendly.

| ID | User | Action | Expected |
|---|---|---|---|
| RBAC-01 | Clerk | Add a class / student / parent / teacher | 403 → clear "You do not have permission…" in the error modal, no crash |
| RBAC-02 | Clerk | Open Students → View student with Aadhaar | Masked |
| RBAC-03 | Supervisor | Same | Full values; editing identity works only if the API allows (needs `Students.Manage` too → 403 expected) |
| RBAC-04 | Teacher | Attendance own class | Works |
| RBAC-05 | Teacher | Open Admissions → click Enrol | 403 handled gracefully; the guardian-matches call fails with a message |
| RBAC-06 | Any | After an admin changes permissions | User must log out and in; the old token keeps old rights until then |

## 12. End-to-end flows (run in order, one browser session)

### E2E-1: Full school day

1. Login as Admin.
2. Classes: create "ZT Class A" (cap 30) and "ZT Class B".
3. Subjects: create "ZT Math" in class A.
4. Teachers: create `zt.teacher1`; assign Math in class A and make class teacher.
5. Parents: create "ZT Father" (mobile 9876501111).
6. Admissions: register "ZTKid One" with Father (same mobile) and Mother (new mobile); confirm with fee 500; enrol into class A.
   - Expect: father **matched** to the existing parent, mother created new.
7. Students: the new student is listed Active; 👪 shows both parents; 📚 shows one Active row.
8. Edit the student: add allergy, one pickup person, blood group. Reopen and confirm everything persisted.
9. Move the student to class B → 📚 shows `Transferred` + `Active`.
10. Move back to class A (roll number free) → 📚 shows three rows.
11. Attendance: as Admin mark class A for today (Mark All Present, one Late, one Absent). Save. Reload and verify.
12. Logout; login as `zt.teacher1`; open Attendance, class A: editable. Open class B: read-only.
13. Login as Clerk: student identity masked; adding a class returns a friendly 403.
14. Reject a new application with a reason; verify "View Rejection Reason".
15. Delete the throwaway data in reverse order (students → parents → subjects → teachers → classes).

Pass criteria: no console errors, no 500, every list updates without a manual refresh.

### E2E-2: Sibling admission (parent reuse)

1. Register two applicants with the **same father mobile**.
2. Enrol the first: Father → Create new. Enrol the second: Father → **Use existing** pre-selected (child shown as already linked).
3. Parents list: one father with two linked children.
4. Create a second parent with the same mobile, then enrol a third applicant: enrol modal shows "must choose" with both candidates.

### E2E-3: Class full / inactive

1. Class with capacity 1; enrol one student.
2. Try to enrol a second → error "is full".
3. Deactivate a class → it disappears from the application/enrol class lists; an existing applicant of that class can still be edited.

### E2E-4: Left and return

1. Mark a student Left → class strength drops by one; attendance roster for new dates no longer lists them.
2. Reactivate → new Active row in history; strength back.

## 13. Cross-browser, accessibility, performance

| ID | Check | Expected |
|---|---|---|
| NFR-01 | Chrome, Edge (and Firefox/Safari if used) | Same behaviour |
| NFR-02 | Keyboard only: Tab through a form, Enter to submit, Esc to close modals | Logical order, visible focus |
| NFR-03 | Zoom 150% | Layout intact |
| NFR-04 | Lists with ~500 students (seed with SQL) | Page renders < 2 s; filters stay responsive (client-side filtering; no server paging yet) |
| NFR-05 | Slow network (DevTools "Slow 3G") | Loading states visible; no duplicate submissions |
| NFR-06 | Double-click Save buttons everywhere | One request each |

---

## 14. Automating this (recommended plan)

No frontend tests exist yet. Add them in this order; each tier catches a different class of bug.

| Tier | Tool | What to test first | Effort |
|---|---|---|---|
| 1. Unit | **Vitest** | `utils/apiError.ts` (`getApiErrors` handles validation, business, network shapes); CSV export builders; masking/format helpers; password rule helper in `TeacherModal` | small |
| 2. Component | **React Testing Library** + **MSW** (mock API) | `RegistrationModal` (add/remove guardians, max 5, primary radio), `EnrollModal` (pre-selection rules, blocking rules), `StudentModal` (identity fields disabled when `isMasked`), `StudentHistoryModal` (loading/empty/error/rows), `SubjectModal` (Both vs Theory fields), `TeacherModal` (password rules) | medium |
| 3. Page | RTL + MSW | Each list page: filters, pagination, empty state, error state; `AttendancePage`: Mark All Present, dirty-state prompt, 🔒 banner when `canEdit=false` | medium |
| 4. End-to-end | **Playwright** against a seeded test database | E2E-1 and E2E-2 above (login → admission → enrol → attendance); run in CI before merge | larger |

Suggested first 10 automated tests (highest value):

1. `getApiErrors` returns field messages for a 400 body, the `message` for 409/422, and a generic message for network errors.
2. `EnrollModal` pre-selects only `UseExisting` / `CreateNew` suggestions and leaves `MustChoose` empty.
3. `EnrollModal` disables Enrol when any guardian is `MissingMobile` or two guardians use the same parent.
4. `RegistrationModal` caps guardians at 5 and keeps exactly one primary.
5. `StudentModal` disables identity inputs when `identity.isMasked` is true.
6. `StudentHistoryModal` renders rows newest-first with badges and shows the error text on failure.
7. `SubjectModal` shows the correct fields per type and sends nulls for the hidden ones.
8. `AttendancePage` disables Save when `canEdit=false` and shows the reason.
9. `ProtectedRoute` redirects when not authenticated.
10. Playwright: login → register admission → enrol → student visible with history row.

Add `npm run test` (Vitest) and `npm run e2e` (Playwright) scripts and run `npm run build` + tests in CI.
