import { useState } from "react";
import { CalendarClock } from "lucide-react";
import { Dialog } from "@/components/Dialog";
import { Notice } from "@/components/Notice";
import { SelectField, TextField } from "@/components/form/Inputs";
import { useDialogSubmit } from "@/hooks/useDialogSubmit";
import type { SubjectResponse } from "@/features/subjects/types/subject.types";
import {
  dayName,
  type TimetableCellInput,
  type TimetableCellResponse,
  type TimetablePeriodResponse,
} from "../types/timetable.types";

interface Props {
  className: string;
  classRoom: string | null;
  day: number;
  period: TimetablePeriodResponse;
  /** The lesson already in this cell, or null for an empty one. */
  cell: TimetableCellResponse | null;
  /** The class's active subjects. */
  subjects: SubjectResponse[];
  onClose: () => void;
  onSave: (cell: TimetableCellInput) => Promise<unknown>;
  /** Asks to remove the lesson (the page confirms and closes this dialog). */
  onClear: () => void;
}

/** Set, change or remove the lesson in one cell: a subject (its teacher comes with it) and a room. */
export function CellDialog({
  className,
  classRoom,
  day,
  period,
  cell,
  subjects,
  onClose,
  onSave,
  onClear,
}: Props) {
  const [subjectId, setSubjectId] = useState(
    cell ? String(cell.subjectId) : "",
  );
  const [room, setRoom] = useState(cell?.room ?? "");
  const { isSubmitting, errors, setErrors, submit } = useDialogSubmit(onClose);

  const save = () => {
    if (!subjectId) {
      setErrors(["Choose a subject."]);
      return;
    }
    void submit(
      () =>
        onSave({
          dayOfWeek: day,
          periodId: period.id,
          subjectId: Number(subjectId),
          room: room.trim() === "" ? null : room.trim(),
        }),
      "Could not save this lesson.",
    );
  };

  const chosen = subjects.find((s) => String(s.id) === subjectId);

  return (
    <Dialog
      title={`${dayName(day)} · ${period.label}`}
      icon={CalendarClock}
      subtitle={`${className} · ${period.startTime}–${period.endTime}`}
      errors={errors}
      closable={!isSubmitting}
      onClose={onClose}
      footer={
        <>
          {cell && (
            <button
              type="button"
              className="btn btn--secondary"
              style={{ marginRight: "auto" }}
              onClick={onClear}
              disabled={isSubmitting}
            >
              Clear lesson
            </button>
          )}
          <button
            type="button"
            className="btn btn--secondary"
            onClick={onClose}
            disabled={isSubmitting}
          >
            Cancel
          </button>
          <button
            type="button"
            className="btn btn--primary"
            onClick={save}
            disabled={isSubmitting || subjects.length === 0}
          >
            {isSubmitting ? "Saving…" : "Save lesson"}
          </button>
        </>
      }
    >
      {subjects.length === 0 ? (
        <Notice tone="warn">
          This class has no active subjects yet. Add subjects first, then
          schedule them here.
        </Notice>
      ) : (
        <div className="form-grid">
          <SelectField
            id="tt-cell-subject"
            label="Subject"
            required
            span="full"
            emptyLabel="Choose a subject"
            options={subjects.map((s) => ({
              value: s.id,
              label: `${s.name} (${s.code})`,
            }))}
            value={subjectId}
            onChange={setSubjectId}
            hint={
              chosen
                ? chosen.teacherName
                  ? `Teacher: ${chosen.teacherName}`
                  : "This subject has no teacher yet. Assign one from the Teachers page."
                : "The teacher comes from the subject."
            }
          />
          <TextField
            id="tt-cell-room"
            label="Room"
            span="full"
            maxLength={30}
            value={room}
            onChange={setRoom}
            hint={
              classRoom
                ? `Leave empty to use the class room (${classRoom}).`
                : "Optional."
            }
          />
        </div>
      )}
    </Dialog>
  );
}
