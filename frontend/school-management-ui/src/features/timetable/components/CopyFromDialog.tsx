import { useState } from "react";
import { Copy } from "lucide-react";
import { Dialog } from "@/components/Dialog";
import { DialogFooter } from "@/components/DialogFooter";
import { Notice } from "@/components/Notice";
import { SelectField } from "@/components/form/Inputs";
import { useDialogSubmit } from "@/hooks/useDialogSubmit";

interface Props {
  targetName: string;
  /** Other classes of the same academic year. */
  sources: { id: number; name: string }[];
  /** Lessons now on the target; they are replaced. */
  replaceCount: number;
  onClose: () => void;
  onCopy: (sourceId: number) => Promise<unknown>;
}

/** Copy one class's whole week onto this class. Replaces what is there, so it says how much. */
export function CopyFromDialog({
  targetName,
  sources,
  replaceCount,
  onClose,
  onCopy,
}: Props) {
  const [sourceId, setSourceId] = useState("");
  const { isSubmitting, errors, setErrors, submit } = useDialogSubmit(onClose);

  const copy = () => {
    if (!sourceId) {
      setErrors(["Choose the class to copy from."]);
      return;
    }
    void submit(() => onCopy(Number(sourceId)), "Could not copy the timetable.");
  };

  return (
    <Dialog
      title="Copy timetable from another class"
      icon={Copy}
      subtitle={`Into ${targetName}`}
      errors={errors}
      closable={!isSubmitting}
      onClose={onClose}
      footer={
        <DialogFooter
          onCancel={onClose}
          onSubmit={copy}
          label="Copy timetable"
          busyLabel="Copying…"
          isSubmitting={isSubmitting}
          danger={replaceCount > 0}
          disabled={sources.length === 0}
        />
      }
    >
      {sources.length === 0 ? (
        <Notice tone="warn">
          There is no other class in this academic year to copy from.
        </Notice>
      ) : (
        <>
          <SelectField
            id="tt-copy-source"
            label="Copy from"
            required
            emptyLabel="Choose a class"
            options={sources.map((s) => ({ value: s.id, label: s.name }))}
            value={sourceId}
            onChange={setSourceId}
          />
          <Notice tone={replaceCount > 0 ? "warn" : "info"} style={{ marginTop: 16 }}>
            {replaceCount > 0
              ? `The ${replaceCount} ${replaceCount === 1 ? "lesson" : "lessons"} now on ${targetName} will be replaced.`
              : `${targetName} has no lessons yet, so nothing is replaced.`}{" "}
            Lessons are matched by subject code; any that cannot be copied (a
            missing subject, a day the school is closed, or a teacher who
            would be in two places) are listed afterwards.
          </Notice>
        </>
      )}
    </Dialog>
  );
}
