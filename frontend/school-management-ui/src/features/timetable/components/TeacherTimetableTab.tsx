import { useMemo, useState } from "react";
import { UserRound } from "lucide-react";
import { EmptyState } from "@/components/EmptyState";
import { Notice } from "@/components/Notice";
import { Panel } from "@/components/Panel";
import { SearchableSelect } from "@/components/form/SearchableSelect";
import { teacherService } from "@/features/teachers/services/teacherService";
import { useAsyncData } from "@/hooks/useAsyncData";
import { useAuth } from "@/store/AuthContext";
import { timetableService } from "../services/timetableService";
import { TimetableGrid } from "./TimetableGrid";

interface Props {
  yearId: number | null;
  /**
   * True for a user who may see only their own week (Timetable.ViewScoped without Timetable.View):
   * no teacher picker, their own timetable opens straight away.
   */
  ownOnly: boolean;
}

/** A teacher's week: where they teach, in which class and room, each day and period. Read only. */
export function TeacherTimetableTab({ yearId, ownOnly }: Props) {
  const { user } = useAuth();
  const teachers = useAsyncData(
    () => teacherService.getAll(),
    [],
    "Could not load teachers.",
  );

  const list = useMemo(
    () =>
      [...(teachers.data ?? [])].sort((a, b) => a.name.localeCompare(b.name)),
    [teachers.data],
  );
  const own = list.find((t) => t.userId === user?.userId) ?? null;

  const [pickedId, setPickedId] = useState<number | null>(null);
  const teacherId = ownOnly
    ? (own?.id ?? null)
    : list.some((t) => t.id === pickedId)
      ? pickedId
      : (list[0]?.id ?? null);

  const week = useAsyncData(
    () =>
      teacherId === null
        ? Promise.resolve(null)
        : timetableService.getTeacher(teacherId, yearId),
    [teacherId, yearId],
    "Could not load this timetable.",
  );

  const data = week.data ?? null;
  const cellAt = useMemo(
    () =>
      new Map(
        (data?.cells ?? []).map((c) => [`${c.dayOfWeek}-${c.periodId}`, c]),
      ),
    [data],
  );

  if (teachers.error) return <Notice tone="warn">{teachers.error}</Notice>;
  if (!teachers.isLoading && teacherId === null) {
    return (
      <div className="card">
        <EmptyState
          icon={UserRound}
          title={ownOnly ? "No teaching profile on your account" : "No teachers yet"}
          message={
            ownOnly
              ? "Your account is not linked to a teacher, so there is no week to show."
              : "Add a teacher on the Users page first."
          }
        />
      </div>
    );
  }

  return (
    <>
      {!ownOnly && (
        <div className="card">
          <div className="filters">
            <div className="field">
              <label htmlFor="tt-teacher">Teacher</label>
              <SearchableSelect
                id="tt-teacher"
                options={list.map((t) => ({ value: t.id, label: t.name }))}
                value={teacherId ?? ""}
                onChange={(v) => setPickedId(Number(v))}
              />
            </div>
          </div>
        </div>
      )}

      {week.error && <Notice tone="warn">{week.error}</Notice>}

      <Panel
        title={ownOnly ? "My timetable" : (data?.teacherName ?? "Timetable")}
        subtitle={
          data
            ? `${data.cells.length} ${data.cells.length === 1 ? "lesson" : "lessons"} a week`
            : undefined
        }
        icon={UserRound}
        padded
      >
        {(week.isLoading || teachers.isLoading) && !data ? (
          <div className="empty-state" role="status">
            Loading…
          </div>
        ) : data && data.periods.length === 0 ? (
          <EmptyState
            icon={UserRound}
            title="No periods set up for this academic year"
          />
        ) : data ? (
          <TimetableGrid
            caption={`Weekly timetable of ${data.teacherName}`}
            days={data.days}
            periods={data.periods}
            renderCell={(day, period) => {
              const cell = cellAt.get(`${day}-${period.id}`);
              return cell ? (
                <>
                  <span className="tt-cell__subject">{cell.subjectName}</span>
                  <span className="tt-cell__line">{cell.className}</span>
                  {cell.effectiveRoom && (
                    <span className="tt-cell__line">{cell.effectiveRoom}</span>
                  )}
                </>
              ) : (
                <span className="tt-empty">—</span>
              );
            }}
          />
        ) : null}
      </Panel>
    </>
  );
}
