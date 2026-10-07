import { useCallback, useEffect, useMemo, useState } from "react";
import { ConfirmDialog } from "@/components/ConfirmDialog";
import { ValidationModal } from "@/components/ValidationModal";
import { classSectionService } from "@/features/class-sections/services/classSectionService";
import type { ClassSectionResponse } from "@/features/class-sections/types/classSection.types";
import { subjectService } from "@/features/subjects/services/subjectService";
import type { SubjectResponse } from "@/features/subjects/types/subject.types";
import { getApiErrors } from "@/utils/apiError";
import { attendanceService } from "../services/attendanceService.ts";
import {
  ATTENDANCE_STATUSES,
  STATUS_COLOR,
  attendancePercentage,
  type AttendanceRosterResponse,
  type AttendanceStatus,
} from "../types/attendance.types";
import "../attendance.css";

type Tab = "daily" | "subject";
type StatusFilter = "" | "Unmarked" | AttendanceStatus;
interface DraftEntry {
  status: AttendanceStatus | null;
  remarks: string;
}

// Local calendar date as yyyy-MM-dd (toISOString would shift it to UTC).
function toDateInput(d: Date) {
  const mm = String(d.getMonth() + 1).padStart(2, "0");
  const dd = String(d.getDate()).padStart(2, "0");
  return `${d.getFullYear()}-${mm}-${dd}`;
}

// The API sends UTC timestamps without a "Z"; add it so the browser converts to local time.
function formatSavedAt(iso: string) {
  const hasZone = /Z$|[+-]\d{2}:\d{2}$/.test(iso);
  return new Date(hasZone ? iso : `${iso}Z`).toLocaleString([], {
    day: "numeric",
    month: "short",
    hour: "2-digit",
    minute: "2-digit",
  });
}

function formatDay(date: string) {
  return new Date(`${date}T00:00:00`).toLocaleDateString([], {
    day: "numeric",
    month: "short",
  });
}

function Last7({ values }: { values: (string | null)[] }) {
  const padded = [
    ...Array<null>(Math.max(0, 7 - values.length)).fill(null),
    ...values,
  ];
  return (
    <span className="att-last7">
      {padded.map((v, i) => {
        const cls =
          v === "Present"
            ? "att-last7__dot--present"
            : v === "Absent"
              ? "att-last7__dot--absent"
              : v
                ? "att-last7__dot--other"
                : "";
        return (
          <span
            key={i}
            className={`att-last7__dot ${cls}`}
            title={v ?? "Not recorded"}
          />
        );
      })}
    </span>
  );
}

export function AttendancePage() {
  const today = toDateInput(new Date());

  const [classSections, setClassSections] = useState<ClassSectionResponse[]>(
    [],
  );
  const [subjects, setSubjects] = useState<SubjectResponse[]>([]);
  const [isLoading, setIsLoading] = useState(true);

  const [tab, setTab] = useState<Tab>("daily");
  const [classSectionId, setClassSectionId] = useState<number | null>(null);
  const [subjectId, setSubjectId] = useState<number | null>(null);
  const [date, setDate] = useState(today);

  const [roster, setRoster] = useState<AttendanceRosterResponse | null>(null);
  const [draft, setDraft] = useState<Record<number, DraftEntry>>({});
  const [isDirty, setIsDirty] = useState(false);
  const [isRosterLoading, setIsRosterLoading] = useState(false);
  const [isSaving, setIsSaving] = useState(false);

  const [statusFilter, setStatusFilter] = useState<StatusFilter>("");
  const [search, setSearch] = useState("");
  const [errors, setErrors] = useState<string[] | null>(null);
  const [pendingChange, setPendingChange] = useState<(() => void) | null>(null);

  const activeClasses = useMemo(
    () => classSections.filter((c) => c.status === "Active"),
    [classSections],
  );
  const classSubjects = useMemo(
    () =>
      subjects.filter(
        (s) => s.classSectionId === classSectionId && s.status === "Active",
      ),
    [subjects, classSectionId],
  );

  // ----- initial lists -----
  useEffect(() => {
    (async () => {
      try {
        const [sections, subjectList] = await Promise.all([
          classSectionService.getAll(),
          subjectService.getAll(),
        ]);
        setClassSections(sections);
        setSubjects(subjectList);
        const first = sections.find((c) => c.status === "Active");
        if (first) setClassSectionId(first.id);
      } catch (err) {
        setErrors(getApiErrors(err, "Could not load classes."));
      } finally {
        setIsLoading(false);
      }
    })();
  }, []);

  // ----- roster loading -----
  const applyRoster = useCallback((r: AttendanceRosterResponse) => {
    setRoster(r);
    const next: Record<number, DraftEntry> = {};
    r.students.forEach((s) => {
      next[s.studentId] = { status: s.status, remarks: s.remarks ?? "" };
    });
    setDraft(next);
    setIsDirty(false);
    setStatusFilter("");
  }, []);

  useEffect(() => {
    if (!classSectionId || !date) return;
    if (tab === "subject" && !subjectId) {
      setRoster(null);
      return;
    }
    let cancelled = false;
    setIsRosterLoading(true);
    attendanceService
      .getRoster({
        classSectionId,
        date,
        subjectId: tab === "subject" ? subjectId : null,
      })
      .then((r) => {
        if (!cancelled) applyRoster(r);
      })
      .catch((err) => {
        if (cancelled) return;
        setRoster(null);
        setErrors(getApiErrors(err, "Could not load attendance."));
      })
      .finally(() => {
        if (!cancelled) setIsRosterLoading(false);
      });
    return () => {
      cancelled = true;
    };
  }, [classSectionId, date, subjectId, tab, applyRoster]);

  // Warn before closing the tab with unsaved changes.
  useEffect(() => {
    if (!isDirty) return;
    const handler = (e: BeforeUnloadEvent) => e.preventDefault();
    window.addEventListener("beforeunload", handler);
    return () => window.removeEventListener("beforeunload", handler);
  }, [isDirty]);

  // Run a filter change now, or after the user agrees to discard unsaved edits.
  const guard = (change: () => void) => {
    if (isDirty) setPendingChange(() => change);
    else change();
  };

  const firstSubjectOf = (classId: number) =>
    subjects.find((s) => s.classSectionId === classId && s.status === "Active")
      ?.id ?? null;

  const changeClass = (id: number) =>
    guard(() => {
      setClassSectionId(id);
      setSubjectId(firstSubjectOf(id));
    });

  const changeTab = (next: Tab) => {
    if (next === tab) return;
    guard(() => {
      setTab(next);
      if (next === "subject" && !subjectId && classSectionId)
        setSubjectId(firstSubjectOf(classSectionId));
    });
  };

  // ----- editing -----
  const canEdit = roster?.canEdit ?? false;

  const setStatus = (studentId: number, status: AttendanceStatus) => {
    if (!canEdit) return;
    setDraft((d) => ({
      ...d,
      [studentId]: { remarks: d[studentId]?.remarks ?? "", status },
    }));
    setIsDirty(true);
  };

  const setRemarks = (studentId: number, remarks: string) => {
    if (!canEdit) return;
    setDraft((d) => ({
      ...d,
      [studentId]: { status: d[studentId]?.status ?? null, remarks },
    }));
    setIsDirty(true);
  };

  const markAll = (status: AttendanceStatus) => {
    if (!roster || !canEdit) return;
    setDraft((d) => {
      const next = { ...d };
      roster.students.forEach((s) => {
        next[s.studentId] = {
          status,
          remarks: next[s.studentId]?.remarks ?? "",
        };
      });
      return next;
    });
    setIsDirty(true);
  };

  const copyFromPrevious = async () => {
    if (!roster?.previousSessionDate || !classSectionId || !canEdit) return;
    try {
      const prev = await attendanceService.getRoster({
        classSectionId,
        date: roster.previousSessionDate,
        subjectId: roster.subjectId,
      });
      setDraft((d) => {
        const next = { ...d };
        prev.students.forEach((s) => {
          if (next[s.studentId])
            next[s.studentId] = { status: s.status, remarks: s.remarks ?? "" };
        });
        return next;
      });
      setIsDirty(true);
    } catch (err) {
      setErrors(getApiErrors(err, "Could not copy the previous day."));
    }
  };

  const handleSave = async () => {
    if (!roster || !classSectionId) return;

    const unmarked = roster.students.filter(
      (s) => s.isActiveInClass && !draft[s.studentId]?.status,
    );
    if (unmarked.length > 0) {
      const names = unmarked
        .slice(0, 5)
        .map((s) => s.name)
        .join(", ");
      setErrors([
        `${unmarked.length} student(s) have no status yet: ${names}${unmarked.length > 5 ? "…" : ""}.`,
        'Tip: use "Mark All Present", then change the exceptions.',
      ]);
      return;
    }

    const records = roster.students.flatMap((s) => {
      const entry = draft[s.studentId];
      return entry?.status
        ? [
            {
              studentId: s.studentId,
              status: entry.status,
              remarks: entry.remarks.trim() || null,
            },
          ]
        : [];
    });

    setIsSaving(true);
    try {
      const saved = await attendanceService.save({
        classSectionId,
        date,
        subjectId: tab === "subject" ? subjectId : null,
        records,
      });
      applyRoster(saved);
    } catch (err) {
      setErrors(getApiErrors(err, "Could not save attendance."));
    } finally {
      setIsSaving(false);
    }
  };

  // ----- derived numbers -----
  const students = roster?.students ?? [];
  const counts = useMemo(() => {
    const c: Record<AttendanceStatus, number> = {
      Present: 0,
      Absent: 0,
      Late: 0,
      "Half Day": 0,
      Leave: 0,
    };
    let unmarked = 0;
    students.forEach((s) => {
      const st = draft[s.studentId]?.status;
      if (st) c[st] += 1;
      else unmarked += 1;
    });
    return { ...c, unmarked };
  }, [students, draft]);

  const percentage = attendancePercentage(counts);

  const visibleStudents = useMemo(() => {
    const q = search.trim().toLowerCase();
    return students.filter((s) => {
      const st = draft[s.studentId]?.status ?? null;
      if (statusFilter === "Unmarked" && st) return false;
      if (statusFilter && statusFilter !== "Unmarked" && st !== statusFilter)
        return false;
      if (q) {
        const hay = `${s.name} ${s.admNo} ${s.rollNumber}`.toLowerCase();
        if (!hay.includes(q)) return false;
      }
      return true;
    });
  }, [students, draft, statusFilter, search]);

  const chips: { key: StatusFilter; label: string }[] = [
    { key: "", label: `ALL (${students.length})` },
    ...ATTENDANCE_STATUSES.map((s) => ({
      key: s as StatusFilter,
      label: `${s.toUpperCase()} (${counts[s]})`,
    })),
    { key: "Unmarked", label: `NOT MARKED (${counts.unmarked})` },
  ];

  if (isLoading) return <p>Loading…</p>;

  return (
    <div>
      <div
        style={{
          display: "flex",
          justifyContent: "space-between",
          alignItems: "center",
          marginBottom: 16,
        }}
      >
        <h1 style={{ fontSize: 19, margin: 0 }}>Attendance</h1>
      </div>

      <div className="att-tabs">
        <button
          type="button"
          className={`att-tab ${tab === "daily" ? "att-tab--active" : ""}`}
          onClick={() => changeTab("daily")}
        >
          📅 DAILY ATTENDANCE
        </button>
        <button
          type="button"
          className={`att-tab ${tab === "subject" ? "att-tab--active" : ""}`}
          onClick={() => changeTab("subject")}
        >
          📘 SUBJECT-WISE
        </button>
      </div>

      <div className="card" style={{ marginBottom: 18 }}>
        <div
          style={{
            display: "flex",
            justifyContent: "space-between",
            alignItems: "center",
            marginBottom: 10,
          }}
        >
          <strong style={{ fontSize: 13 }}>Date · Class · Search</strong>
          <button
            className="btn btn--secondary btn--sm"
            onClick={() => guard(() => setDate(today))}
          >
            ⏮ Jump to Today
          </button>
        </div>
        <div className="filters">
          <div className="field">
            <label>Class</label>
            <select
              value={classSectionId ?? ""}
              onChange={(e) => changeClass(Number(e.target.value))}
            >
              {activeClasses.map((c) => (
                <option key={c.id} value={c.id}>
                  {c.displayName}
                </option>
              ))}
            </select>
          </div>
          <div className="field">
            <label>Date</label>
            <input
              type="date"
              value={date}
              max={today}
              onChange={(e) => {
                const value = e.target.value;
                if (value) guard(() => setDate(value));
              }}
            />
          </div>
          {tab === "subject" && (
            <div className="field">
              <label>Subject</label>
              <select
                value={subjectId ?? ""}
                onChange={(e) => {
                  const id = Number(e.target.value);
                  guard(() => setSubjectId(id));
                }}
              >
                {classSubjects.length === 0 && (
                  <option value="">No active subjects</option>
                )}
                {classSubjects.map((s) => (
                  <option key={s.id} value={s.id}>
                    {s.name}
                  </option>
                ))}
              </select>
            </div>
          )}
          <div className="field">
            <label>Search Students</label>
            <input
              placeholder="Name / admission / roll..."
              value={search}
              onChange={(e) => setSearch(e.target.value)}
            />
          </div>
        </div>
      </div>

      {activeClasses.length === 0 ? (
        <div className="card">
          <div className="empty-state">
            No active classes yet. Create a class first.
          </div>
        </div>
      ) : tab === "subject" && classSubjects.length === 0 ? (
        <div className="card">
          <div className="empty-state">
            This class has no active subjects, so there is nothing to mark
            subject-wise.
          </div>
        </div>
      ) : (
        <div className="card">
          {roster && !roster.canEdit && roster.cannotEditReason && (
            <div className="info-banner">🔒 {roster.cannotEditReason}</div>
          )}

          <div
            style={{
              display: "flex",
              justifyContent: "space-between",
              alignItems: "center",
              marginBottom: 14,
            }}
          >
            <strong>
              👥 Total Students
              {roster?.subjectName ? ` · ${roster.subjectName}` : ""}
            </strong>
            <div style={{ fontSize: 20, fontWeight: 700 }}>
              {students.length} ·{" "}
              <span style={{ color: "var(--green)" }}>
                {percentage === null ? "—" : `${percentage}% present`}
              </span>
            </div>
          </div>

          <div className="pipeline" style={{ marginBottom: 10 }}>
            {chips.map((c) => (
              <button
                key={c.key || "all"}
                type="button"
                className={`pipeline__step ${statusFilter === c.key ? "pipeline__step--active" : ""}`}
                onClick={() => setStatusFilter(c.key)}
              >
                {c.label}
              </button>
            ))}
          </div>

          <div style={{ marginBottom: 16 }}>
            <div
              style={{
                display: "flex",
                justifyContent: "space-between",
                fontSize: 12,
                color: "var(--muted)",
              }}
            >
              <span>Class Attendance</span>
              <span>
                {students.length - counts.unmarked}/{students.length} marked
              </span>
            </div>
            <div className="att-progress">
              <div
                className="att-progress__fill"
                style={{ width: `${percentage ?? 0}%` }}
              />
            </div>
          </div>

          <div style={{ display: "flex", gap: 10, marginBottom: 16 }}>
            <button
              className="btn btn--info"
              style={{ background: "var(--green)" }}
              disabled={!canEdit}
              onClick={() => markAll("Present")}
            >
              ✓ Mark All Present
            </button>
            <button
              className="btn btn--info"
              style={{ background: "var(--red)" }}
              disabled={!canEdit}
              onClick={() => markAll("Absent")}
            >
              ✕ Mark All Absent
            </button>
            <button
              className="btn btn--secondary"
              disabled={!canEdit || !roster?.previousSessionDate}
              onClick={copyFromPrevious}
              title={
                roster?.previousSessionDate
                  ? undefined
                  : "No earlier attendance to copy"
              }
            >
              ↻ Copy from{" "}
              {roster?.previousSessionDate
                ? formatDay(roster.previousSessionDate)
                : "Previous Day"}
            </button>
          </div>

          <div style={{ overflowX: "auto" }}>
            <table>
              <thead>
                <tr>
                  <th>Roll</th>
                  <th>Student</th>
                  <th>Last 7</th>
                  <th>Status</th>
                  <th>Remarks</th>
                </tr>
              </thead>
              <tbody>
                {isRosterLoading ? (
                  <tr>
                    <td colSpan={5}>
                      <div className="empty-state">Loading…</div>
                    </td>
                  </tr>
                ) : visibleStudents.length === 0 ? (
                  <tr>
                    <td colSpan={5}>
                      <div className="empty-state">
                        {students.length === 0
                          ? "No active students in this class."
                          : "No students match this filter."}
                      </div>
                    </td>
                  </tr>
                ) : (
                  visibleStudents.map((s) => {
                    const entry = draft[s.studentId];
                    return (
                      <tr key={s.studentId}>
                        <td>{s.rollNumber}</td>
                        <td>
                          <span className="avatar-circle">
                            {s.name.charAt(0)}
                          </span>
                          <strong>{s.name}</strong>
                          {!s.isActiveInClass && (
                            <span
                              className="badge badge--gray"
                              style={{ marginLeft: 6 }}
                            >
                              Left class
                            </span>
                          )}
                          <br />
                          <span style={{ fontSize: 11, color: "var(--muted)" }}>
                            {s.admNo}
                          </span>
                        </td>
                        <td>
                          <Last7 values={s.last7} />
                        </td>
                        <td style={{ whiteSpace: "nowrap" }}>
                          {ATTENDANCE_STATUSES.map((st) => (
                            <button
                              key={st}
                              type="button"
                              disabled={!canEdit}
                              className={`att-status-btn ${entry?.status === st ? `att-status-btn--${STATUS_COLOR[st]}` : ""}`}
                              onClick={() => setStatus(s.studentId, st)}
                            >
                              {st}
                            </button>
                          ))}
                        </td>
                        <td>
                          <input
                            className="att-remarks"
                            placeholder="(optional)"
                            maxLength={250}
                            disabled={!canEdit}
                            value={entry?.remarks ?? ""}
                            onChange={(e) =>
                              setRemarks(s.studentId, e.target.value)
                            }
                          />
                        </td>
                      </tr>
                    );
                  })
                )}
              </tbody>
            </table>
          </div>

          <div className="att-save-bar">
            <span
              style={{
                fontSize: 12.5,
                fontWeight: 600,
                color: isDirty
                  ? "var(--amber)"
                  : roster?.isSaved
                    ? "var(--green)"
                    : "var(--muted)",
              }}
            >
              {isDirty
                ? "⚠ Unsaved changes"
                : roster?.isSaved
                  ? `✓ Saved${roster.savedBy ? ` by ${roster.savedBy}` : ""}${roster.savedAtUtc ? ` · ${formatSavedAt(roster.savedAtUtc)}` : ""}`
                  : "Not saved yet"}
            </span>
            <button
              className="btn btn--primary"
              disabled={!canEdit || isSaving || !isDirty}
              onClick={handleSave}
            >
              {isSaving ? "Saving…" : "💾 Save Attendance"}
            </button>
          </div>
        </div>
      )}

      <ConfirmDialog
        isOpen={pendingChange !== null}
        title="Discard unsaved changes?"
        message="You have attendance changes that are not saved. Switching now will lose them."
        confirmLabel="Discard changes"
        cancelLabel="Keep editing"
        danger={false}
        onConfirm={() => {
          const run = pendingChange;
          setPendingChange(null);
          run?.();
        }}
        onCancel={() => setPendingChange(null)}
      />

      <ValidationModal errors={errors} onClose={() => setErrors(null)} />
    </div>
  );
}
