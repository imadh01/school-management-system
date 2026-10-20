import { useMemo, useState } from "react";
import { Calendar, Copy } from "lucide-react";
import { Dialog } from "@/components/Dialog";
import { EmptyState } from "@/components/EmptyState";
import { Notice } from "@/components/Notice";
import { Panel } from "@/components/Panel";
import { useToast } from "@/components/Toast";
import { SearchableSelect } from "@/components/form/SearchableSelect";
import type { ClassSectionResponse } from "@/features/class-sections/types/classSection.types";
import { subjectService } from "@/features/subjects/services/subjectService";
import type { SubjectResponse } from "@/features/subjects/types/subject.types";
import { useAsyncData } from "@/hooks/useAsyncData";
import { useConfirmAction } from "@/hooks/useConfirmAction";
import { timetableService } from "../services/timetableService";
import {
  dayName,
  type CopyTimetableResult,
  type TimetableCellInput,
  type TimetableCellResponse,
  type TimetablePeriodResponse,
} from "../types/timetable.types";
import { CellDialog } from "./CellDialog";
import { CopyFromDialog } from "./CopyFromDialog";
import { TimetableGrid } from "./TimetableGrid";

interface Props {
  yearId: number | null;
  classSections: ClassSectionResponse[];
  canEdit: boolean;
}

interface CellTarget {
  day: number;
  period: TimetablePeriodResponse;
  cell: TimetableCellResponse | null;
}

/** One class's week. With Timetable.Edit every Teaching cell opens a dialog to set or clear its lesson. */
export function ClassTimetableTab({ yearId, classSections, canEdit }: Props) {
  const toast = useToast();

  const classes = useMemo(
    () =>
      classSections
        .filter((c) => c.academicYearId === yearId && c.status === "Active")
        .sort((a, b) => a.displayName.localeCompare(b.displayName)),
    [classSections, yearId],
  );
  const [pickedId, setPickedId] = useState<number | null>(null);
  // The picked class, or the first one when the year changed and the pick is not in it.
  const classId = classes.some((c) => c.id === pickedId)
    ? pickedId
    : (classes[0]?.id ?? null);

  const grid = useAsyncData(
    () =>
      classId === null
        ? Promise.resolve(null)
        : timetableService.getClass(classId),
    [classId],
    "Could not load this timetable.",
  );
  const subjects = useAsyncData(
    () =>
      canEdit
        ? subjectService.getAll()
        : Promise.resolve([] as SubjectResponse[]),
    [canEdit],
    "Could not load subjects.",
  );

  const [target, setTarget] = useState<CellTarget | null>(null);
  const [copying, setCopying] = useState(false);
  const [skipped, setSkipped] = useState<CopyTimetableResult | null>(null);

  const data = grid.data ?? null;
  const cellAt = useMemo(
    () =>
      new Map(
        (data?.cells ?? []).map((c) => [`${c.dayOfWeek}-${c.periodId}`, c]),
      ),
    [data],
  );

  const clear = useConfirmAction<CellTarget>({
    title: "Clear lesson",
    message: (t) =>
      `Remove ${t.cell?.subjectName ?? "this lesson"} from ${dayName(t.day)}, ${t.period.label}?`,
    confirmLabel: "Clear lesson",
    danger: true,
    run: async (t) => {
      await timetableService.clearCell(classId!, t.day, t.period.id);
      await grid.reload();
    },
    success: () => "Lesson cleared.",
    failure: "Could not clear this lesson.",
  });

  const saveCell = async (cell: TimetableCellInput) => {
    const updated = await timetableService.saveCells(classId!, [cell]);
    grid.setData(updated);
    toast.success("Lesson saved.");
  };

  const copyFrom = async (sourceId: number) => {
    const result = await timetableService.copyFrom(classId!, sourceId);
    await grid.reload();
    toast.success(
      `Copied ${result.copied} ${result.copied === 1 ? "lesson" : "lessons"}` +
        (result.replaced > 0 ? `, replacing ${result.replaced}.` : "."),
    );
    if (result.skipped.length > 0) setSkipped(result);
  };

  if (classes.length === 0) {
    return (
      <div className="card">
        <EmptyState
          icon={Calendar}
          title="No active classes in this academic year"
          message="Create a class for this year first, then build its timetable here."
        />
      </div>
    );
  }

  const classSubjects = (subjects.data ?? []).filter(
    (s) => s.classSectionId === classId && s.status === "Active",
  );

  return (
    <>
      <div className="card">
        <div className="filters">
          <div className="field">
            <label htmlFor="tt-class">Class</label>
            <SearchableSelect
              id="tt-class"
              options={classes.map((c) => ({
                value: c.id,
                label: c.displayName,
              }))}
              value={classId ?? ""}
              onChange={(v) => setPickedId(Number(v))}
            />
          </div>
        </div>
      </div>

      {grid.error && <Notice tone="warn">{grid.error}</Notice>}

      <Panel
        title={data?.className ?? "Timetable"}
        subtitle={
          data
            ? `${data.cells.length} ${data.cells.length === 1 ? "lesson" : "lessons"} scheduled`
            : undefined
        }
        icon={Calendar}
        actions={
          canEdit && data ? (
            <button
              type="button"
              className="btn btn--secondary"
              onClick={() => setCopying(true)}
            >
              <Copy size={16} aria-hidden="true" />
              Copy from…
            </button>
          ) : undefined
        }
        padded
      >
        {grid.isLoading && !data ? (
          <div className="empty-state" role="status">
            Loading…
          </div>
        ) : data && data.periods.length === 0 ? (
          <EmptyState
            icon={Calendar}
            title="No periods set up for this academic year"
            message="Add the day's periods on the Periods tab first."
          />
        ) : data ? (
          <TimetableGrid
            caption={`Weekly timetable of ${data.className}`}
            days={data.days}
            periods={data.periods}
            renderCell={(day, period) => {
              const cell = cellAt.get(`${day}-${period.id}`) ?? null;
              const content = cell ? (
                <>
                  <span className="tt-cell__subject">{cell.subjectName}</span>
                  <span className="tt-cell__line">{cell.subjectCode}</span>
                  <span className="tt-cell__line">
                    {cell.teacherName ?? "No teacher"}
                  </span>
                  {cell.effectiveRoom && (
                    <span className="tt-cell__line">{cell.effectiveRoom}</span>
                  )}
                </>
              ) : canEdit ? (
                <span className="tt-cell__add">+ Add</span>
              ) : (
                <span className="tt-empty">—</span>
              );
              if (!canEdit) return content;
              return (
                <button
                  type="button"
                  className="tt-cell--button"
                  aria-label={
                    cell
                      ? `Edit ${cell.subjectName}, ${dayName(day)}, ${period.label}`
                      : `Add a lesson, ${dayName(day)}, ${period.label}`
                  }
                  onClick={() => setTarget({ day, period, cell })}
                >
                  {content}
                </button>
              );
            }}
          />
        ) : null}
      </Panel>

      {target && data && (
        <CellDialog
          className={data.className}
          classRoom={data.classRoom}
          day={target.day}
          period={target.period}
          cell={target.cell}
          subjects={classSubjects}
          onClose={() => setTarget(null)}
          onSave={saveCell}
          onClear={() => {
            clear.ask(target);
            setTarget(null);
          }}
        />
      )}

      {copying && data && (
        <CopyFromDialog
          targetName={data.className}
          sources={classes
            .filter((c) => c.id !== classId)
            .map((c) => ({ id: c.id, name: c.displayName }))}
          replaceCount={data.cells.length}
          onClose={() => setCopying(false)}
          onCopy={copyFrom}
        />
      )}

      {skipped && (
        <Dialog
          title="Some lessons were not copied"
          onClose={() => setSkipped(null)}
          footer={
            <button
              type="button"
              className="btn btn--primary"
              onClick={() => setSkipped(null)}
            >
              Close
            </button>
          }
        >
          <p style={{ margin: 0 }}>
            {skipped.copied} copied, {skipped.skipped.length} skipped:
          </p>
          <ul className="tt-skipped">
            {skipped.skipped.map((s, i) => (
              <li key={i}>
                {dayName(s.dayOfWeek)}, {s.periodLabel}: {s.subjectName} (
                {s.reason})
              </li>
            ))}
          </ul>
        </Dialog>
      )}

      {clear.dialog}
    </>
  );
}
