import { useState } from "react";
import { Ban } from "lucide-react";
import type { AdmissionResponse } from "../types/admission.types";
import { Dialog } from "@/components/Dialog";
import { Field } from "@/components/form/Field";
import { getApiErrors } from "@/utils/apiError";

interface Props {
  admission: AdmissionResponse;
  onClose: () => void;
  onSubmit: (id: number, reason: string) => Promise<void>;
}

/** Registered/Admitted → Rejected. A rejected application is closed: it cannot be edited or enrolled. */
export function RejectAdmissionModal({ admission, onClose, onSubmit }: Props) {
  const [reason, setReason] = useState("");
  const [isSubmitting, setIsSubmitting] = useState(false);
  const [errors, setErrors] = useState<string[]>([]);

  const handleSubmit = async () => {
    setErrors([]);
    setIsSubmitting(true);
    try {
      await onSubmit(admission.id, reason.trim());
      onClose();
    } catch (err) {
      setErrors(getApiErrors(err, "Could not reject this application."));
      setIsSubmitting(false);
    }
  };

  return (
    <Dialog
      title="Reject application"
      icon={Ban}
      subtitle={`${admission.firstName} ${admission.lastName} · ${admission.regNo}`}
      errors={errors}
      onClose={onClose}
      closable={!isSubmitting}
      footer={
        <>
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
            className="btn btn--danger"
            onClick={handleSubmit}
            disabled={isSubmitting}
          >
            {isSubmitting ? "Rejecting…" : "Reject application"}
          </button>
        </>
      }
    >
      <div className="notice notice--warn" style={{ marginBottom: 16 }}>
        <Ban size={16} aria-hidden="true" />
        <span>
          A rejected application is closed: it can no longer be edited or
          enrolled.
        </span>
      </div>
      <Field
        id="reject-reason"
        label="Reason (optional)"
        hint="Shown on the application."
      >
        {(c) => (
          <textarea
            {...c}
            rows={3}
            value={reason}
            onChange={(e) => setReason(e.target.value)}
          />
        )}
      </Field>
    </Dialog>
  );
}
