import { useState } from "react";
import { CircleX } from "lucide-react";
import { Dialog } from "@/components/Dialog";
import { DialogFooter } from "@/components/DialogFooter";
import { Notice } from "@/components/Notice";
import { TextAreaField } from "@/components/form/Inputs";
import { useDialogSubmit } from "@/hooks/useDialogSubmit";
import type { DocumentResponse } from "../types/document.types";

/** G1b: Pending → Rejected. The reason is required; the fix is a new upload, not an edit. */
export function RejectDocumentDialog({
  document,
  onClose,
  onSubmit,
}: {
  document: DocumentResponse;
  onClose: () => void;
  onSubmit: (reason: string) => Promise<void>;
}) {
  const [reason, setReason] = useState("");
  const [reasonError, setReasonError] = useState<string>();
  const { isSubmitting, errors, submit } = useDialogSubmit(onClose);

  const reject = () => {
    if (!reason.trim()) {
      setReasonError(
        "Give the reason, so the person who uploads it again knows what to fix.",
      );
      return;
    }
    setReasonError(undefined);
    void submit(
      () => onSubmit(reason.trim()),
      "Could not reject this document.",
    );
  };

  return (
    <Dialog
      title="Reject document"
      icon={CircleX}
      subtitle={`${document.documentTypeName} · ${document.ownerName}`}
      errors={errors}
      onClose={onClose}
      closable={!isSubmitting}
      footer={
        <DialogFooter
          onCancel={onClose}
          onSubmit={reject}
          label="Reject document"
          busyLabel="Rejecting…"
          isSubmitting={isSubmitting}
          danger
        />
      }
    >
      <Notice tone="warn" icon={CircleX} style={{ marginBottom: 16 }}>
        A rejected document is not edited: the correct file is uploaded as a new
        document.
      </Notice>
      <TextAreaField
        id="reject-document-reason"
        label="Reason"
        required
        placeholder="e.g. The scan is blurred; the expiry date cannot be read."
        value={reason}
        onChange={(v) => {
          setReason(v);
          setReasonError(undefined);
        }}
        error={reasonError}
      />
    </Dialog>
  );
}
