import { useState } from "react";
import { Clock } from "lucide-react";
import { Dialog } from "@/components/Dialog";
import { DialogFooter } from "@/components/DialogFooter";
import { Field } from "@/components/form/Field";
import { SelectField, TextField } from "@/components/form/Inputs";
import { useDialogSubmit } from "@/hooks/useDialogSubmit";
import {
  PERIOD_KINDS,
  type PeriodKind,
  type TimetablePeriodResponse,
} from "../types/timetable.types";

interface Props {
  /** The period being edited, or null to add one. */
  period: TimetablePeriodResponse | null;
  /** The year's periods, to suggest the next order and start time. */
  existing: TimetablePeriodResponse[];
  onClose: () => void;
  onSave: (values: {
    label: string;
    startTime: string;
    endTime: string;
    kind: PeriodKind;
    sortOrder: number;
  }) => Promise<unknown>;
}

/** Add or edit one period of the school day (a Teaching period or a Break). */
export function PeriodDialog({
  period,
  existing,
  onClose,
  onSave,
}: Props) {
  const last = [...existing].sort((a, b) => a.sortOrder - b.sortOrder).at(-1);
  const [label, setLabel] = useState(period?.label ?? "");
  const [start, setStart] = useState(period?.startTime ?? last?.endTime ?? "");
  const [end, setEnd] = useState(period?.endTime ?? "");
  const [kind, setKind] = useState<string>(period?.kind ?? "Teaching");
  const [order, setOrder] = useState(
    String(period?.sortOrder ?? (last?.sortOrder ?? 0) + 1),
  );
  const { isSubmitting, errors, setErrors, submit } = useDialogSubmit(onClose);

  const save = () => {
    const problems: string[] = [];
    if (!label.trim()) problems.push("Enter a name for the period.");
    if (!start || !end) problems.push("Enter the start and end time.");
    else if (end <= start) problems.push("The end time must be after the start time.");
    const sortOrder = Number(order);
    if (!Number.isInteger(sortOrder) || sortOrder < 1)
      problems.push("The order must be a whole number from 1.");
    if (problems.length > 0) {
      setErrors(problems);
      return;
    }
    void submit(
      () =>
        onSave({
          label: label.trim(),
          startTime: start,
          endTime: end,
          kind: kind as PeriodKind,
          sortOrder,
        }),
      "Could not save this period.",
    );
  };

  return (
    <Dialog
      title={period ? "Edit period" : "Add period"}
      icon={Clock}
      errors={errors}
      closable={!isSubmitting}
      onClose={onClose}
      footer={
        <DialogFooter
          onCancel={onClose}
          onSubmit={save}
          label={period ? "Save changes" : "Add period"}
          busyLabel="Saving…"
          isSubmitting={isSubmitting}
        />
      }
    >
      <div className="form-grid">
        <TextField
          id="tt-period-label"
          label="Name"
          required
          span="full"
          maxLength={50}
          placeholder="Period 1, Lunch…"
          value={label}
          onChange={setLabel}
          autoFocus
        />
        <Field id="tt-period-start" label="Starts" required>
          {(c) => (
            <input
              {...c}
              type="time"
              value={start}
              onChange={(e) => setStart(e.target.value)}
            />
          )}
        </Field>
        <Field id="tt-period-end" label="Ends" required>
          {(c) => (
            <input
              {...c}
              type="time"
              value={end}
              onChange={(e) => setEnd(e.target.value)}
            />
          )}
        </Field>
        <SelectField
          id="tt-period-kind"
          label="Type"
          required
          options={PERIOD_KINDS}
          value={kind}
          onChange={setKind}
          hint={
            period && period.cellCount > 0
              ? `Used by ${period.cellCount} ${period.cellCount === 1 ? "lesson" : "lessons"}, so it must stay a Teaching period.`
              : "Breaks have no lessons."
          }
        />
        <TextField
          id="tt-period-order"
          label="Order"
          required
          type="number"
          min={1}
          value={order}
          onChange={setOrder}
          hint="Position in the day. Each order is used once."
        />
      </div>
    </Dialog>
  );
}
