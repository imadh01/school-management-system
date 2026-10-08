import { useEffect, useState } from "react";
import type { AdmissionResponse } from "../types/admission.types";
import { ValidationModal } from "@/components/ValidationModal";
import { getApiErrors } from "@/utils/apiError";

interface Props {
  admission: AdmissionResponse | null;
  onClose: () => void;
  onSubmit: (id: number, reason: string) => Promise<void>;
}

export function RejectAdmissionModal({ admission, onClose, onSubmit }: Props) {
  const [reason, setReason] = useState("");
  const [isSubmitting, setIsSubmitting] = useState(false);
  const [errors, setErrors] = useState<string[] | null>(null);

  useEffect(() => {
    setReason("");
    setErrors(null);
  }, [admission]);

  if (!admission) return null;

  const handleSubmit = async () => {
    setIsSubmitting(true);
    try {
      await onSubmit(admission.id, reason.trim());
      onClose();
    } catch (err) {
      setErrors(getApiErrors(err, "Could not reject this application."));
    } finally {
      setIsSubmitting(false);
    }
  };

  return (
    <div className="modal-backdrop">
      <div className="modal" style={{ maxWidth: 440 }}>
        <div className="modal__header">
          <h2>✖ Reject Application</h2>
          <button className="modal__close" onClick={onClose}>
            ✕
          </button>
        </div>
        <div className="modal__body">
          <div className="modal__note" style={{ marginTop: 0 }}>
            {admission.firstName} {admission.lastName} · {admission.regNo}
          </div>
          <div className="field">
            <label>Reason (optional)</label>
            <textarea
              rows={3}
              value={reason}
              onChange={(e) => setReason(e.target.value)}
            />
          </div>
        </div>
        <div className="modal__footer">
          <button
            className="btn btn--danger"
            onClick={handleSubmit}
            disabled={isSubmitting}
          >
            {isSubmitting ? "Rejecting…" : "Reject"}
          </button>
          <button className="btn btn--secondary" onClick={onClose}>
            Cancel
          </button>
        </div>
      </div>
      <ValidationModal errors={errors} onClose={() => setErrors(null)} />
    </div>
  );
}
