import { useState } from "react";
import { Lock } from "lucide-react";
import { Dialog } from "@/components/Dialog";
import { DialogFooter } from "@/components/DialogFooter";
import { Notice } from "@/components/Notice";
import { TextField } from "@/components/form/Inputs";
import { useDialogSubmit } from "@/hooks/useDialogSubmit";
import type { AcademicYearResponse } from "@/services/academicYearService";

interface Props {
  /** The Ended year to close. */
  year: AcademicYearResponse;
  onClose: () => void;
  onConfirm: (confirmName: string) => Promise<unknown>;
}

/** Closing is one-way, so the year's name must be typed. The button stays off until it matches. */
export function CloseYearDialog({ year, onClose, onConfirm }: Props) {
  const [typed, setTyped] = useState("");
  const { isSubmitting, errors, submit } = useDialogSubmit(onClose);
  const blocked = year.activeEnrollmentCount > 0;

  return (
    <Dialog
      title="Close academic year"
      icon={Lock}
      subtitle={year.name}
      errors={errors}
      closable={!isSubmitting}
      onClose={onClose}
      footer={
        <DialogFooter
          onCancel={onClose}
          onSubmit={() => void submit(() => onConfirm(typed.trim()), "Could not close this year.")}
          label="Close year"
          busyLabel="Closing…"
          isSubmitting={isSubmitting}
          danger
          disabled={blocked || typed.trim() !== year.name}
        />
      }
    >
      <Notice tone="warn">
        Closing {year.name} makes it read-only history: no new classes or
        timetable changes. It cannot be reopened.
      </Notice>
      {blocked ? (
        <Notice tone="warn" style={{ marginTop: 12 }}>
          {year.activeEnrollmentCount}{" "}
          {year.activeEnrollmentCount === 1 ? "student is" : "students are"}{" "}
          still Active in {year.name}. Promote, repeat or transfer them first.
        </Notice>
      ) : (
        <div style={{ marginTop: 16 }}>
          <TextField
            id="ay-close-confirm"
            label={`Type ${year.name} to confirm`}
            required
            value={typed}
            onChange={setTyped}
            autoComplete="off"
            autoFocus
          />
        </div>
      )}
    </Dialog>
  );
}
