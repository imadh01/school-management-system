import { CircleCheck } from "lucide-react";
import { Dialog } from "@/components/Dialog";
import { DialogFooter } from "@/components/DialogFooter";
import { Notice } from "@/components/Notice";
import { useDialogSubmit } from "@/hooks/useDialogSubmit";
import type { AcademicYearResponse } from "@/services/academicYearService";

interface Props {
  /** The Upcoming year to make current. */
  year: AcademicYearResponse;
  /** The year that is current now (null if none). */
  current: AcademicYearResponse | null;
  onClose: () => void;
  onConfirm: () => Promise<unknown>;
}

/**
 * Making a year current changes what teachers see, so this says so and names both years.
 * The request carries the current year named here: if someone else switched it meanwhile, nothing changes.
 */
export function SetCurrentDialog({ year, current, onClose, onConfirm }: Props) {
  const { isSubmitting, errors, submit } = useDialogSubmit(onClose);

  return (
    <Dialog
      title="Make this the current year"
      icon={CircleCheck}
      subtitle={year.name}
      errors={errors}
      closable={!isSubmitting}
      onClose={onClose}
      footer={
        <DialogFooter
          onCancel={onClose}
          onSubmit={() => void submit(onConfirm, "Could not change the current year.")}
          label={`Make ${year.name} current`}
          busyLabel="Switching…"
          isSubmitting={isSubmitting}
          danger
        />
      }
    >
      <p style={{ fontSize: 14, lineHeight: 1.6, margin: "0 0 12px" }}>
        {current ? (
          <>
            <strong>{year.name}</strong> becomes the current year and{" "}
            <strong>{current.name}</strong> becomes Ended. Nothing in{" "}
            {current.name} is deleted or changed.
          </>
        ) : (
          <>
            <strong>{year.name}</strong> becomes the current year. There is no
            current year at the moment.
          </>
        )}
      </p>
      <Notice tone="warn">
        Teachers see only the classes and subjects of the current year, so after
        this they will see {year.name}.{" "}
        {year.classCount === 0
          ? `${year.name} has no classes yet, so teachers will see nothing until classes are created in it.`
          : `${year.name} has ${year.classCount} ${year.classCount === 1 ? "class" : "classes"}.`}{" "}
        New classes and the Timetable also default to the current year.
      </Notice>
    </Dialog>
  );
}
