import { useEffect, useState } from "react";
import type {
  AdmissionResponse,
  ConfirmAdmissionRequest,
} from "../types/admission.types";
import { ValidationModal } from "@/components/ValidationModal";
import { getApiErrors } from "@/utils/apiError";

interface Props {
  admission: AdmissionResponse | null;
  onClose: () => void;
  onSubmit: (id: number, data: ConfirmAdmissionRequest) => Promise<void>;
}

const BLOOD_GROUPS = ["A+", "A-", "B+", "B-", "AB+", "AB-", "O+", "O-"];
const RELIGIONS = [
  "Hindu",
  "Muslim",
  "Christian",
  "Sikh",
  "Buddhist",
  "Jain",
  "Other",
];
const CATEGORIES = ["General", "OBC", "SC", "ST", "EWS"];

const emptyForm: ConfirmAdmissionRequest = {
  admissionFee: null,
  admissionFeeReference: null,
  bloodGroup: null,
  religion: null,
  category: null,
  medicalNotes: null,
  remarks: null,
};

export function ConfirmAdmissionModal({ admission, onClose, onSubmit }: Props) {
  const [form, setForm] = useState<ConfirmAdmissionRequest>(emptyForm);
  const [isSubmitting, setIsSubmitting] = useState(false);
  const [errors, setErrors] = useState<string[] | null>(null);

  // Remarks entered at registration are kept and shown here, because
  // confirming replaces the remarks with whatever is in this box.
  useEffect(() => {
    setForm(
      admission
        ? {
            ...emptyForm,
            remarks: admission.remarks,
            bloodGroup: admission.bloodGroup,
            religion: admission.religion,
            category: admission.category,
            medicalNotes: admission.medicalNotes,
          }
        : emptyForm,
    );
    setErrors(null);
  }, [admission]);

  if (!admission) return null;

  const field = (key: keyof ConfirmAdmissionRequest, value: string) =>
    setForm((f) => ({ ...f, [key]: value === "" ? null : value }));

  const handleSubmit = async () => {
    setErrors(null);
    setIsSubmitting(true);
    try {
      await onSubmit(admission.id, form);
      onClose();
    } catch (err) {
      setErrors(
        getApiErrors(
          err,
          "Could not confirm this admission. It may have already progressed past Registered.",
        ),
      );
    } finally {
      setIsSubmitting(false);
    }
  };

  return (
    <div className="modal-backdrop">
      <div className="modal">
        <div className="modal__header">
          <h2>✅ Confirm Admission</h2>
          <button className="modal__close" onClick={onClose}>
            ✕
          </button>
        </div>
        <div className="modal__body">
          <div className="modal__note" style={{ marginTop: 0 }}>
            {admission.firstName} {admission.lastName} · {admission.regNo}
          </div>

          <div className="modal__section-title">Admission Fee</div>
          <div className="modal__grid">
            <div className="field">
              <label>Fee Amount</label>
              <input
                type="number"
                value={form.admissionFee ?? ""}
                onChange={(e) =>
                  setForm((f) => ({
                    ...f,
                    admissionFee:
                      e.target.value === "" ? null : Number(e.target.value),
                  }))
                }
              />
            </div>
            <div className="field">
              <label>Payment Reference</label>
              <input
                value={form.admissionFeeReference ?? ""}
                onChange={(e) => field("admissionFeeReference", e.target.value)}
              />
            </div>
          </div>

          <div className="modal__section-title">Profile Details</div>
          <div className="modal__grid">
            <div className="field">
              <label>Blood Group</label>
              <select
                value={form.bloodGroup ?? ""}
                onChange={(e) => field("bloodGroup", e.target.value)}
              >
                <option value="">Unknown</option>
                {BLOOD_GROUPS.map((b) => (
                  <option key={b} value={b}>
                    {b}
                  </option>
                ))}
              </select>
            </div>
            <div className="field">
              <label>Religion</label>
              <select
                value={form.religion ?? ""}
                onChange={(e) => field("religion", e.target.value)}
              >
                <option value="">Not specified</option>
                {RELIGIONS.map((r) => (
                  <option key={r} value={r}>
                    {r}
                  </option>
                ))}
              </select>
            </div>
            <div className="field">
              <label>Category</label>
              <select
                value={form.category ?? ""}
                onChange={(e) => field("category", e.target.value)}
              >
                <option value="">General (default)</option>
                {CATEGORIES.map((c) => (
                  <option key={c} value={c}>
                    {c}
                  </option>
                ))}
              </select>
            </div>
            <div className="field">
              <label>Medical Notes</label>
              <input
                value={form.medicalNotes ?? ""}
                onChange={(e) => field("medicalNotes", e.target.value)}
              />
            </div>
          </div>
          <div className="field" style={{ marginTop: 14 }}>
            <label>Remarks</label>
            <textarea
              rows={2}
              value={form.remarks ?? ""}
              onChange={(e) => field("remarks", e.target.value)}
            />
          </div>
        </div>
        <div className="modal__footer">
          <button
            className="btn btn--primary"
            onClick={handleSubmit}
            disabled={isSubmitting}
          >
            {isSubmitting ? "Confirming…" : "Confirm Admission"}
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
