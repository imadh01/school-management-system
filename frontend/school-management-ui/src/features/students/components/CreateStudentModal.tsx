import { useState } from "react";
import type { AdmissionResponse } from "@/features/admissions/types/admission.types";
import type { CreateStudentFromAdmissionRequest } from "../types/student.types";

interface Props {
  admission: AdmissionResponse | null;
  onClose: () => void;
  onSubmit: (
    admissionId: number,
    data: CreateStudentFromAdmissionRequest,
  ) => Promise<void>;
}

export function CreateStudentModal({ admission, onClose, onSubmit }: Props) {
  const [form, setForm] = useState<CreateStudentFromAdmissionRequest>({
    nationality: null,
    curriculumTrack: null,
    englishProficiency: null,
    ealCode: null,
    house: null,
    allergies: null,
  });
  const [isSubmitting, setIsSubmitting] = useState(false);
  const [error, setError] = useState<string | null>(null);

  if (!admission) return null;

  const field = (key: keyof CreateStudentFromAdmissionRequest, value: string) =>
    setForm((f) => ({ ...f, [key]: value === "" ? null : value }));

  const handleSubmit = async () => {
    setError(null);
    setIsSubmitting(true);
    try {
      await onSubmit(admission.id, form);
      onClose();
    } catch {
      setError(
        "Could not create a student record. This admission may already have been converted.",
      );
    } finally {
      setIsSubmitting(false);
    }
  };

  return (
    <div className="modal-backdrop">
      <div className="modal">
        <div className="modal__header">
          <h2>🧑‍🎓 Create Student Record</h2>
          <button className="modal__close" onClick={onClose}>
            ✕
          </button>
        </div>
        <div className="modal__body">
          <div className="modal__note" style={{ marginTop: 0 }}>
            {admission.firstName} {admission.lastName} · {admission.regNo} ·
            Roll {admission.rollNumber} · Adm # {admission.admissionNumber}
          </div>
          <div className="modal__note">
            Identity, roll number, admission number, and class come directly
            from the enrollment record. The fields below are additional student
            profile details not captured during admission.
          </div>

          <div className="modal__section-title">Additional Profile</div>
          <div className="modal__grid">
            <div className="field">
              <label>Nationality</label>
              <input
                value={form.nationality ?? ""}
                onChange={(e) => field("nationality", e.target.value)}
              />
            </div>
            <div className="field">
              <label>Curriculum Track</label>
              <input
                value={form.curriculumTrack ?? ""}
                onChange={(e) => field("curriculumTrack", e.target.value)}
              />
            </div>
            <div className="field">
              <label>English Proficiency</label>
              <input
                value={form.englishProficiency ?? ""}
                onChange={(e) => field("englishProficiency", e.target.value)}
              />
            </div>
            <div className="field">
              <label>EAL Code</label>
              <input
                value={form.ealCode ?? ""}
                onChange={(e) => field("ealCode", e.target.value)}
              />
            </div>
            <div className="field">
              <label>House</label>
              <input
                value={form.house ?? ""}
                onChange={(e) => field("house", e.target.value)}
              />
            </div>
            <div className="field">
              <label>Allergies</label>
              <input
                value={form.allergies ?? ""}
                onChange={(e) => field("allergies", e.target.value)}
              />
            </div>
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
            {isSubmitting ? "Creating…" : "Create Student"}
          </button>
          <button className="btn btn--secondary" onClick={onClose}>
            Cancel
          </button>
        </div>
      </div>
    </div>
  );
}
