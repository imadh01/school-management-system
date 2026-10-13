import { useState } from "react";
import { Ban } from "lucide-react";
import type { AdmissionResponse } from "../types/admission.types";
import { Dialog } from "@/components/Dialog";
import { DialogFooter } from "@/components/DialogFooter";
import { Notice } from "@/components/Notice";
import { TextAreaField } from "@/components/form/Inputs";
import { useDialogSubmit } from "@/hooks/useDialogSubmit";

interface Props {
  admission: AdmissionResponse;
  onClose: () => void;
  onSubmit: (id: number, reason: string) => Promise<void>;
}

/** Registered/Admitted → Rejected. A rejected application is closed: it cannot be edited or enrolled. */
export function RejectAdmissionModal({ admission, onClose, onSubmit }: Props) {
  const [reason, setReason] = useState("");
  const { isSubmitting, errors, submit } = useDialogSubmit(onClose);

  return (
    <Dialog
      title="Reject application"
      icon={Ban}
      subtitle={`${admission.firstName} ${admission.lastName} · ${admission.regNo}`}
      errors={errors}
      onClose={onClose}
      closable={!isSubmitting}
      footer={
        <DialogFooter
          onCancel={onClose}
          onSubmit={() =>
            submit(
              () => onSubmit(admission.id, reason.trim()),
              "Could not reject this application.",
            )
          }
          label="Reject application"
          busyLabel="Rejecting…"
          isSubmitting={isSubmitting}
          danger
        />
      }
    >
      <Notice tone="warn" icon={Ban} style={{ marginBottom: 16 }}>
        A rejected application is closed: it can no longer be edited or
        enrolled.
      </Notice>
      <TextAreaField
        id="reject-reason"
        label="Reason (optional)"
        hint="Shown on the application."
        value={reason}
        onChange={setReason}
      />
    </Dialog>
  );
}
