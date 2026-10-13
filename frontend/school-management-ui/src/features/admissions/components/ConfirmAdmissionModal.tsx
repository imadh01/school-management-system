import { useState } from "react";
import { CircleCheck } from "lucide-react";
import type {
  AdmissionResponse,
  ConfirmAdmissionRequest,
} from "../types/admission.types";
import { Dialog } from "@/components/Dialog";
import { Field } from "@/components/form/Field";
import { getApiErrors } from "@/utils/apiError";

interface Props {
  admission: AdmissionResponse;
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

/** Registered → Admitted: the fee is recorded and a few profile details are added. */
export function ConfirmAdmissionModal({ admission, onClose, onSubmit }: Props) {
  // Remarks entered at registration are kept and shown here, because confirming replaces the
  // remarks with whatever is in this box.
  const [form, setForm] = useState<ConfirmAdmissionRequest>({
    admissionFee: null,
    admissionFeeReference: null,
    remarks: admission.remarks,
    bloodGroup: admission.bloodGroup,
    religion: admission.religion,
    category: admission.category,
    medicalNotes: admission.medicalNotes,
  });
  const [isSubmitting, setIsSubmitting] = useState(false);
  const [errors, setErrors] = useState<string[]>([]);

  const set = (patch: Partial<ConfirmAdmissionRequest>) =>
    setForm((f) => ({ ...f, ...patch }));
  const text = (v: string) => (v.trim() === "" ? null : v);

  const handleSubmit = async () => {
    setErrors([]);
    setIsSubmitting(true);
    try {
      await onSubmit(admission.id, form);
      onClose();
    } catch (err) {
      setErrors(
        getApiErrors(
          err,
          "Could not confirm this admission. It may have already moved past Registered.",
        ),
      );
      setIsSubmitting(false);
    }
  };

  return (
    <Dialog
      title="Confirm admission"
      icon={CircleCheck}
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
            className="btn btn--primary"
            onClick={handleSubmit}
            disabled={isSubmitting}
          >
            {isSubmitting ? "Confirming…" : "Confirm admission"}
          </button>
        </>
      }
    >
      <h3 className="form-subhead">Admission fee</h3>
      <div className="form-grid" style={{ marginBottom: 18 }}>
        <Field id="confirm-fee" label="Fee amount">
          {(c) => (
            <input
              {...c}
              type="number"
              min={0}
              value={form.admissionFee ?? ""}
              onChange={(e) =>
                set({
                  admissionFee:
                    e.target.value === "" ? null : Number(e.target.value),
                })
              }
            />
          )}
        </Field>
        <Field id="confirm-fee-ref" label="Payment reference">
          {(c) => (
            <input
              {...c}
              value={form.admissionFeeReference ?? ""}
              onChange={(e) =>
                set({ admissionFeeReference: text(e.target.value) })
              }
            />
          )}
        </Field>
      </div>

      <h3 className="form-subhead">Profile details</h3>
      <div className="form-grid">
        <Field id="confirm-blood" label="Blood group">
          {(c) => (
            <select
              {...c}
              value={form.bloodGroup ?? ""}
              onChange={(e) => set({ bloodGroup: text(e.target.value) })}
            >
              <option value="">Unknown</option>
              {BLOOD_GROUPS.map((b) => (
                <option key={b}>{b}</option>
              ))}
            </select>
          )}
        </Field>
        <Field id="confirm-religion" label="Religion">
          {(c) => (
            <select
              {...c}
              value={form.religion ?? ""}
              onChange={(e) => set({ religion: text(e.target.value) })}
            >
              <option value="">Not specified</option>
              {RELIGIONS.map((r) => (
                <option key={r}>{r}</option>
              ))}
            </select>
          )}
        </Field>
        <Field id="confirm-category" label="Category">
          {(c) => (
            <select
              {...c}
              value={form.category ?? ""}
              onChange={(e) => set({ category: text(e.target.value) })}
            >
              <option value="">General (default)</option>
              {CATEGORIES.map((cat) => (
                <option key={cat}>{cat}</option>
              ))}
            </select>
          )}
        </Field>
        <Field id="confirm-medical" label="Medical notes">
          {(c) => (
            <input
              {...c}
              value={form.medicalNotes ?? ""}
              onChange={(e) => set({ medicalNotes: text(e.target.value) })}
            />
          )}
        </Field>
        <Field id="confirm-remarks" label="Remarks" span="full">
          {(c) => (
            <textarea
              {...c}
              rows={2}
              value={form.remarks ?? ""}
              onChange={(e) => set({ remarks: text(e.target.value) })}
            />
          )}
        </Field>
      </div>
    </Dialog>
  );
}
