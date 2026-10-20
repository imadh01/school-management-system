import { useState } from "react";
import { CalendarRange } from "lucide-react";
import { Dialog } from "@/components/Dialog";
import { DialogFooter } from "@/components/DialogFooter";
import { TextField } from "@/components/form/Inputs";
import { useDialogSubmit } from "@/hooks/useDialogSubmit";
import type { AcademicYearResponse } from "@/services/academicYearService";

interface Props {
  /** The year being edited, or null to add one. */
  year: AcademicYearResponse | null;
  onClose: () => void;
  onSave: (values: { name: string; startDate: string; endDate: string }) => Promise<unknown>;
}

/** Add or edit an academic year. Once a year has started its start date is fixed. */
export function AcademicYearDialog({ year, onClose, onSave }: Props) {
  const [name, setName] = useState(year?.name ?? "");
  const [start, setStart] = useState(year?.startDate ?? "");
  const [end, setEnd] = useState(year?.endDate ?? "");
  const { isSubmitting, errors, setErrors, submit } = useDialogSubmit(onClose);

  // Upcoming years may change both dates; Current and Ended years may only change the end date.
  const startLocked = year !== null && year.status !== "Upcoming";

  const save = () => {
    const problems: string[] = [];
    if (!name.trim()) problems.push("Enter the name, for example 2027-2028.");
    if (!start || !end) problems.push("Enter the start and end date.");
    else if (end <= start) problems.push("The end date must be after the start date.");
    if (problems.length > 0) {
      setErrors(problems);
      return;
    }
    void submit(
      () => onSave({ name: name.trim(), startDate: start, endDate: end }),
      "Could not save this academic year.",
    );
  };

  return (
    <Dialog
      title={year ? "Edit academic year" : "Add academic year"}
      icon={CalendarRange}
      errors={errors}
      closable={!isSubmitting}
      onClose={onClose}
      footer={
        <DialogFooter
          onCancel={onClose}
          onSubmit={save}
          label={year ? "Save changes" : "Add year"}
          busyLabel="Saving…"
          isSubmitting={isSubmitting}
        />
      }
    >
      <div className="form-grid">
        <TextField
          id="ay-name"
          label="Name"
          required
          span="full"
          maxLength={20}
          placeholder="2027-2028"
          value={name}
          onChange={setName}
          autoFocus
        />
        <TextField
          id="ay-start"
          label="Starts"
          type="date"
          required
          value={start}
          onChange={setStart}
          disabled={startLocked}
          hint={startLocked ? "Fixed once the year has started." : undefined}
        />
        <TextField
          id="ay-end"
          label="Ends"
          type="date"
          required
          value={end}
          onChange={setEnd}
          hint="Years cannot overlap, and the dates must cover every record already saved."
        />
      </div>
    </Dialog>
  );
}
