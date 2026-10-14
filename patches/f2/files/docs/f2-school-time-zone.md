# F2: the school's time zone decides "today"

**Status:** built on its own branch (`feature/f2-school-timezone`), not merged. No migration, no data change.

## The problem

Until now "today" was the server's **UTC** date. A school in India (UTC+05:30) starts its day at 18:30 UTC of the day before, so between
00:00 and 05:30 local time the server still believed it was yesterday: "attendance cannot be marked for a future date" appeared for the
school's own today, and the 7-day edit window was off by one for the first hours of every day. The browser made it worse: the Attendance
page used the **browser's** date, a third opinion.

## The rule

* **Business dates** (attendance "today", "moved to this class today") use the school's calendar date in the time zone set in Settings.
* **Timestamps** (CreatedAt, UpdatedAt, audit rows, tokens, lockouts) stay UTC. They are instants, not calendar dates.

## What changed

| Where | Change |
|---|---|
| `ISchoolClock` / `SchoolClock` (new, Application) | `TodayAsync()`: the date at `TimeProvider.GetUtcNow()` in the zone from the school settings (read from the in-memory settings cache, no query). An empty or unknown zone falls back to UTC, the behaviour before F2. Registered as a singleton. |
| `AttendanceService` | "no future dates", the 7-day edit window and the default range of a student's history use `ISchoolClock`. |
| `StudentService` | The date of a class move / leaving (`StudentEnrollmentRules.Today()` removed, with its TODO) uses `ISchoolClock`, so a move and that day's attendance agree. |
| `GET /api/settings/basics` | Also returns `today` (yyyy-MM-dd, school time zone). |
| Attendance page | Default and maximum date come from `schoolService.getToday()` (fresh, never cached); only if that cannot be loaded does it fall back to the browser's date. The API still checks every save. |

## Existing attendance dates do not change

* `AttendanceSessions.Date` is only ever written from the date in the save request; "today" is never used to compute a stored date.
* There is no migration and no data script.
* `database/scripts/f2-attendance-dates-check.sql`: run it before deploying and after (and after changing the time zone). The two result sets must be identical.
* `AttendanceDatesAndTimeZoneTests` saves sessions, switches the school to UTC+14, UTC-11 and India, and checks the stored dates and the saved rosters are unchanged.

What *does* change: at the edges of the day the answer to "is this date in the future?" now follows the school. A date that was refused at 02:00 local time is accepted (it is the school's today); at the other end of the world (a school west of UTC) the evening hours no longer allow tomorrow's UTC date.

## Not changed (still UTC), on purpose

| Use | Why left |
|---|---|
| `Admission.RegistrationDate` and the `REG-yyyy-nnnn` year | A registration date is not part of attendance; changing it changes the numbers staff see. Decide with the business first. |
| Document expiry "today" (`DocumentService`) | Same. |
| Date-of-birth "must be in the past" validators | A one-day tolerance, no business effect. |
| Enrol pop-up default admission date (`toISOString`) | Frontend default only; staff can change it. Easy to move to `getToday()` later. |

## Tests

* `SchoolClockTests`: India midnight edges, Los Angeles (behind UTC), London daylight saving, unknown zone falls back to UTC, the clock follows a changed setting.
* `AttendanceTodayTests`: with UTC = 14th and school = 15th, the school's 15th is editable, the 16th is "future", the 7-day window counts from the school's today, history ends on the school's today, and "before F2" the same request was refused.
* `AttendanceDatesAndTimeZoneTests` (SQL Server): stored dates unchanged under zone changes; the clock follows the setting immediately.
* `DataScopeTests` / `InActionPermissionTests`: their "today" helper now uses the school's zone (the test database is seeded with Asia/Dubai).

## How to use this branch

1. Commit G2 (and anything else pending) on `develop` first. This patch is made on top of that.
2. `git checkout -b feature/f2-school-timezone`
3. Copy the files from `patches/f2/files/` over the repository (same paths). `patches/f2/f2.patch` is the same change as a diff for reading.
4. Delete the `patches` folder, run the tests, commit.
