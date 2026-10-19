# Feedback of 15 Oct 2026: items not built yet

These came from the demo review. Each needs a decision and most need a database migration, so none
was started on the eve of the demo. Nothing below is built. "Proposal" is a suggestion, not a rule.

## 8. Admission fee by class (configuration), payment fields required
- **Today:** `Admissions.AdmissionFee` and `AdmissionFeeReference` are typed in at Confirm, both optional.
- **Proposal:** a table `AdmissionFeeRules (AcademicYearId, ClassName or Grade, Amount)`; Confirm pre-fills the
  amount from the rule for the applied class; the reference becomes required once an amount is set.
- **Needs from you:** one fee per class, or per class and academic year? Can the clerk change the amount?
  Are the payment fields required to move to Admitted? Is this the start of the Fees module?

## 9. Admission number, generated per year
- **Today:** typed in at Enrol; unique across the school.
- **Proposal:** `ADM-{year}-{4-digit serial}` (for example `ADM-2026-0001`), serial restarting each year, generated
  by the server inside the enrol transaction from a small counter table (`NumberSequences`: Kind, Year, Last).
- **Needs from you:** the exact format; the year = admission date year or academic year?; may the clerk overwrite it?

## 10. Roll number, generated
- **Your note:** `[Class][Section][3-digit serial]`. Example `5A001`.
- **Proposal:** the same counter table, one counter per section and academic year.
- **Needs from you:** for "Class 10 B", is it `10B001`? Does the serial restart each academic year? What happens to the
  number when a student moves section (keep, or re-issue)?

## 12. Student photo upload instead of a photo URL
- **Today:** `Students.PhotoUrl` (text).
- **Proposal:** upload through the existing document storage (file type JPG/PNG, 2 MB), store the file id, serve it
  through an authenticated endpoint. Needs a migration and an `Image` rule in the upload validator.
- **Needs from you:** is the photo a document (reviewed) or just a picture? Shown in lists and on the student page?

## 13. Transport module (routes, fee structure, student link)
New module: `TransportRoutes`, `TransportStops`, `TransportFeeStructures`, `StudentTransport (StudentId, RouteId,
StopId, From, To)`. Replaces the two free-text fields on the student (`TransportRoute`, `TransportRequired`).
Waits for your go-ahead as a module; needs its own requirements and design review.

## 18. Hostel and mess tracking
New module only if the school runs a hostel: `Hostels`, `Rooms`, `HostelAllocations`, `MessPlans`. Until then the
Health section stays optional. Needs from you: does the school have a hostel and a mess at all?

## 20. Identity documents by nationality (Aadhaar for India, Emirates ID for UAE)
- **Today:** Aadhaar, passport, visa only. There is **no Emirates ID column**.
- **Proposal:** add `EmiratesIdNumber` (and expiry) to `StudentIdentityDocuments`; the form shows Aadhaar when the
  nationality is Indian and Emirates ID when it is Emirati, both only for users with Students.ViewSensitive.
- **Needs from you:** is the number *required* (blocks saving) or just shown? Users without sensitive access cannot
  fill it, so a hard rule would block them from editing a student.

## 22 and 23. Deleted records are shown, soft delete everywhere
- **Today:** 9 entities are already soft-deleted (Admission, ClassSection, Document, Parent, StaffProfile, Student,
  Subject, Teacher, User). Roles, Academic years and lookup tables are not. The audit log records every delete.
- **Gap:** a deleted record just disappears from the list.
- **Proposal (no migration):** a "Deleted" filter on the Admissions list (and later others) for users with the
  Delete permission, showing who deleted it and when, with a Restore action if you want it.

## 27. Employee number generated for teachers/staff
- **Today:** `StaffProfiles.EmployeeNumber` is typed in (optional).
- **Proposal:** `EMP-{4-digit serial}` from the same counter table, filled automatically when a user is created.
- **Needs from you:** the format, and whether existing blank numbers should be back-filled.

## Also awaiting a decision
- **28. Attendance after 24 hours.** Built as "today and yesterday" for non-admins (by date). A true 24 hours
  counted from when the register was first saved needs a timestamp check. Should Admin also be locked?
- **29. Admin uploads and verifies documents.** An Admin can do both but cannot verify their own upload (rule: nobody
  reviews their own document), so a second Admin is needed. Should an Admin be allowed to verify their own?
- **19. Insurance fields** are hidden from the screens but the columns remain. Remove them from the database?
