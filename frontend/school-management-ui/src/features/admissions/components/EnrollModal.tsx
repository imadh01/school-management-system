import { useState } from "react";
import type {
  AdmissionResponse,
  EnrollAdmissionRequest,
} from "../types/admission.types";
import type { ClassSectionResponse } from "@/features/class-sections/types/classSection.types";

interface Props {
  admission: AdmissionResponse | null;
  classSections: ClassSectionResponse[];
  onClose: () => void;
  onSubmit: (id: number, data: EnrollAdmissionRequest) => Promise<void>;
}

export function EnrollModal({
  admission,
  classSections,
  onClose,
  onSubmit,
}: Props) {
  const [form, setForm] = useState<EnrollAdmissionRequest>({
    rollNumber: "",
    admissionNumber: "",
    admissionDate: new Date().toISOString().slice(0, 10),
    entryPoint: "",
    transportRequired: false,
    allottedClassSectionId: 0,
  });
  const [isSubmitting, setIsSubmitting] = useState(false);
  const [error, setError] = useState<string | null>(null);

  if (!admission) return null;

  const handleSubmit = async () => {
    setError(null);
    if (
      !form.rollNumber ||
      !form.admissionNumber ||
      !form.allottedClassSectionId
    ) {
      setError(
        "Roll number, admission number, and allotted class are required.",
      );
      return;
    }
    setIsSubmitting(true);
    try {
      await onSubmit(admission.id, form);
      onClose();
    } catch {
      setError(
        "Could not enroll this student. The application may not be in Admitted status.",
      );
    } finally {
      setIsSubmitting(false);
    }
  };

  return (
    <div className="modal-backdrop">
      <div className="modal">
        <div className="modal__header">
          <h2>🎓 Enroll to Class</h2>
          <button className="modal__close" onClick={onClose}>
            ✕
          </button>
        </div>
        <div className="modal__body">
          <div className="modal__note" style={{ marginTop: 0 }}>
            {admission.firstName} {admission.lastName} · {admission.regNo}
          </div>

          <div className="modal__section-title">Class &amp; Identity</div>
          <div className="modal__grid">
            <div className="field">
              <label>
                Allotted Class Section<span className="required">*</span>
              </label>
              <select
                value={form.allottedClassSectionId || ""}
                onChange={(e) =>
                  setForm((f) => ({
                    ...f,
                    allottedClassSectionId: Number(e.target.value),
                  }))
                }
              >
                <option value="">Select a class...</option>
                {classSections.map((c) => (
                  <option key={c.id} value={c.id}>
                    {c.displayName}
                  </option>
                ))}
              </select>
            </div>
            <div className="field">
              <label>
                Roll Number<span className="required">*</span>
              </label>
              <input
                value={form.rollNumber}
                onChange={(e) =>
                  setForm((f) => ({ ...f, rollNumber: e.target.value }))
                }
              />
            </div>
            <div className="field">
              <label>
                Admission Number<span className="required">*</span>
              </label>
              <input
                value={form.admissionNumber}
                onChange={(e) =>
                  setForm((f) => ({ ...f, admissionNumber: e.target.value }))
                }
              />
            </div>
            <div className="field">
              <label>Admission Date</label>
              <input
                type="date"
                value={form.admissionDate}
                onChange={(e) =>
                  setForm((f) => ({ ...f, admissionDate: e.target.value }))
                }
              />
            </div>
            <div className="field">
              <label>Entry Point</label>
              <input
                value={form.entryPoint ?? ""}
                onChange={(e) =>
                  setForm((f) => ({ ...f, entryPoint: e.target.value }))
                }
              />
            </div>
          </div>

          <div className="modal__section-title">Transport</div>
          <div className="field">
            <label>
              <input
                type="checkbox"
                checked={form.transportRequired}
                onChange={(e) =>
                  setForm((f) => ({
                    ...f,
                    transportRequired: e.target.checked,
                  }))
                }
                style={{ marginRight: 8 }}
              />
              Transport required
            </label>
          </div>

          <div className="modal__note">
            This enrolls the student record but does not create a portal login —
            that stays a separate, manual step.
          </div>

          {error && (
            <div
              className="modal__note"
              style={{ background: "var(--red-bg)", color: "var(--red)" }}
            >
              {error}
            </div>
          )}
        </div>
        <div className="modal__footer">
          <button
            className="btn btn--primary"
            onClick={handleSubmit}
            disabled={isSubmitting}
          >
            {isSubmitting ? "Enrolling…" : "Enroll Student"}
          </button>
          <button className="btn btn--secondary" onClick={onClose}>
            Cancel
          </button>
        </div>
      </div>
    </div>
  );
}
