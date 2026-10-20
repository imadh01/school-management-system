import { useState } from "react";
import { Copy } from "lucide-react";
import { Dialog } from "@/components/Dialog";
import { DialogFooter } from "@/components/DialogFooter";
import { Notice } from "@/components/Notice";
import { SelectField } from "@/components/form/Inputs";
import { useDialogSubmit } from "@/hooks/useDialogSubmit";
import type { AcademicYearResponse } from "@/services/academicYearService";

interface Props {
  /** The year that receives the periods. */
  target: AcademicYearResponse;
  /** Other years that have periods. */
  sources: AcademicYearResponse[];
  onClose: () => void;
  onCopy: (sourceId: number) => Promise<unknown>;
}

/** Copy the timetable periods (names, times, breaks, order) of one year into this one. Never merges. */
export function CopyPeriodsDialog({ target, sources, onClose, onCopy }: Props) {
  const [sourceId, setSourceId] = useState("");
  const { isSubmitting, errors, setErrors, submit } = useDialogSubmit(onClose);
  const blocked = target.periodCount > 0;

  const copy = () => {
    if (!sourceId) {
      setErrors(["Choose the year to copy the periods from."]);
      return;
    }
    void submit(() => onCopy(Number(sourceId)), "Could not copy the periods.");
  };

  return (
    <Dialog
      title="Copy periods from another year"
      icon={Copy}
      subtitle={`Into ${target.name}`}
      errors={errors}
      closable={!isSubmitting}
      onClose={onClose}
      footer={
        <DialogFooter
          onCancel={onClose}
          onSubmit={copy}
          label="Copy periods"
          busyLabel="Copying…"
          isSubmitting={isSubmitting}
          disabled={blocked || sources.length === 0}
        />
      }
    >
      {blocked ? (
        <Notice tone="warn">
          {target.name} already has {target.periodCount}{" "}
          {target.periodCount === 1 ? "period" : "periods"}. Copying never
          merges: delete them on the Timetable page first.
        </Notice>
      ) : sources.length === 0 ? (
        <Notice tone="warn">No other academic year has periods to copy.</Notice>
      ) : (
        <>
          <SelectField
            id="ay-copy-source"
            label="Copy from"
            required
            emptyLabel="Choose a year"
            options={sources.map((s) => ({
              value: s.id,
              label: `${s.name} (${s.periodCount} ${s.periodCount === 1 ? "period" : "periods"})`,
            }))}
            value={sourceId}
            onChange={setSourceId}
          />
          <Notice style={{ marginTop: 16 }}>
            The periods (names, times, breaks and order) are copied. Lessons are
            not: each class's timetable is filled in separately.
          </Notice>
        </>
      )}
    </Dialog>
  );
}
