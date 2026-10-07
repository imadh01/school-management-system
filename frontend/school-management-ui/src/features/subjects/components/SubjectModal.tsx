import { useEffect, useState } from "react";
import { ValidationModal } from "@/components/ValidationModal";
import type { ClassSectionResponse } from "@/features/class-sections/types/classSection.types";
import { getApiErrors } from "@/utils/apiError";
import {
  SUBJECT_TYPES,
  type SubjectRequest,
  type SubjectResponse,
  type SubjectType,
} from "../types/subject.types";

interface Props {
  isOpen: boolean;
  onClose: () => void;
  editing: SubjectResponse | null;
  classSections: ClassSectionResponse[];
  onSave: (data: SubjectRequest) => Promise<void>;
}

interface FormState {
  name: string;
  code: string;
  classSectionId: number | "";
  type: SubjectType;
  maxMarks: string;
  passMarks: string;
  theoryMax: string;
  theoryPass: string;
  practicalMax: string;
  practicalPass: string;
  isActive: boolean;
}

const emptyForm: FormState = {
  name: "",
  code: "",
  classSectionId: "",
  type: "Theory",
  maxMarks: "100",
  passMarks: "33",
  theoryMax: "80",
  theoryPass: "27",
  practicalMax: "20",
  practicalPass: "6",
  isActive: true,
};

const num = (v: string) => (v.trim() === "" ? null : Number(v));

export function SubjectModal({
  isOpen,
  onClose,
  editing,
  classSections,
  onSave,
}: Props) {
  const [form, setForm] = useState<FormState>(emptyForm);
  const [isSubmitting, setIsSubmitting] = useState(false);
  const [errors, setErrors] = useState<string[] | null>(null);

  useEffect(() => {
    if (!isOpen) return;
    setErrors(null);
    if (editing) {
      setForm({
        name: editing.name,
        code: editing.code,
        classSectionId: editing.classSectionId,
        type: editing.type as SubjectType,
        maxMarks: editing.maxMarks?.toString() ?? "100",
        passMarks: editing.passMarks?.toString() ?? "33",
        theoryMax: editing.theoryMax?.toString() ?? "80",
        theoryPass: editing.theoryPass?.toString() ?? "27",
        practicalMax: editing.practicalMax?.toString() ?? "20",
        practicalPass: editing.practicalPass?.toString() ?? "6",
        isActive: editing.status === "Active",
      });
    } else {
      setForm(emptyForm);
    }
  }, [isOpen, editing]);

  if (!isOpen) return null;

  const set = <K extends keyof FormState>(key: K, value: FormState[K]) =>
    setForm((f) => ({ ...f, [key]: value }));

  const isBoth = form.type === "Both";

  // Active classes, plus the subject's own class when editing (even if inactive).
  const classOptions = classSections.filter(
    (c) => c.status === "Active" || c.id === editing?.classSectionId,
  );

  const handleSubmit = async () => {
    const problems: string[] = [];
    if (!form.name.trim()) problems.push("Subject name is required.");
    if (form.classSectionId === "") problems.push("Class is required.");

    const checkPair = (label: string, max: string, pass: string) => {
      const m = num(max);
      const p = num(pass);
      if (m === null || Number.isNaN(m) || m < 0)
        problems.push(`${label} max marks must be a number of 0 or more.`);
      else if (p === null || Number.isNaN(p) || p < 0)
        problems.push(`${label} pass marks must be a number of 0 or more.`);
      else if (p > m)
        problems.push(`${label} pass marks cannot be more than max marks.`);
    };
    if (isBoth) {
      checkPair("Theory", form.theoryMax, form.theoryPass);
      checkPair("Practical", form.practicalMax, form.practicalPass);
    } else {
      checkPair("", form.maxMarks, form.passMarks);
    }
    if (problems.length > 0) {
      setErrors(problems.map((p) => p.replace(/^ /, "")));
      return;
    }

    setIsSubmitting(true);
    try {
      await onSave({
        name: form.name.trim(),
        code: form.code.trim() || null,
        classSectionId: form.classSectionId as number,
        type: form.type,
        maxMarks: isBoth ? null : num(form.maxMarks),
        passMarks: isBoth ? null : num(form.passMarks),
        theoryMax: isBoth ? num(form.theoryMax) : null,
        theoryPass: isBoth ? num(form.theoryPass) : null,
        practicalMax: isBoth ? num(form.practicalMax) : null,
        practicalPass: isBoth ? num(form.practicalPass) : null,
        isActive: form.isActive,
      });
      onClose();
    } catch (err) {
      setErrors(getApiErrors(err, "Could not save this subject."));
    } finally {
      setIsSubmitting(false);
    }
  };

  return (
    <>
      <div className="modal-backdrop open">
        <div className="modal">
          <div className="modal__header">
            <h2>{editing ? "✏️ Edit Subject" : "📘 Add Subject"}</h2>
            <button className="modal__close" onClick={onClose}>
              ✕
            </button>
          </div>
          <div className="modal__body">
            <div className="modal__section-title">Basics</div>
            <div className="modal__grid">
              <div className="field">
                <label>
                  Subject Name<span className="required">*</span>
                </label>
                <input
                  value={form.name}
                  onChange={(e) => set("name", e.target.value)}
                  placeholder="e.g. Mathematics"
                />
              </div>
              <div className="field">
                <label>Subject Code</label>
                <input
                  value={form.code}
                  onChange={(e) => set("code", e.target.value.toUpperCase())}
                  placeholder="e.g. MATH (auto if blank)"
                />
              </div>
              <div className="field">
                <label>
                  Class<span className="required">*</span>
                </label>
                <select
                  value={form.classSectionId}
                  onChange={(e) =>
                    set(
                      "classSectionId",
                      e.target.value ? Number(e.target.value) : "",
                    )
                  }
                >
                  <option value="">Select class...</option>
                  {classOptions.map((c) => (
                    <option key={c.id} value={c.id}>
                      {c.displayName}
                    </option>
                  ))}
                </select>
              </div>
              <div className="field">
                <label>
                  Subject Type<span className="required">*</span>
                </label>
                <select
                  value={form.type}
                  onChange={(e) => set("type", e.target.value as SubjectType)}
                >
                  {SUBJECT_TYPES.map((t) => (
                    <option key={t}>{t}</option>
                  ))}
                </select>
              </div>
            </div>

            <div className="modal__section-title">Marks Configuration</div>
            <div className="modal__grid">
              {isBoth ? (
                <>
                  <div className="field">
                    <label>
                      Theory Max<span className="required">*</span>
                    </label>
                    <input
                      type="number"
                      min={0}
                      value={form.theoryMax}
                      onChange={(e) => set("theoryMax", e.target.value)}
                    />
                  </div>
                  <div className="field">
                    <label>
                      Theory Pass<span className="required">*</span>
                    </label>
                    <input
                      type="number"
                      min={0}
                      value={form.theoryPass}
                      onChange={(e) => set("theoryPass", e.target.value)}
                    />
                  </div>
                  <div className="field">
                    <label>
                      Practical Max<span className="required">*</span>
                    </label>
                    <input
                      type="number"
                      min={0}
                      value={form.practicalMax}
                      onChange={(e) => set("practicalMax", e.target.value)}
                    />
                  </div>
                  <div className="field">
                    <label>
                      Practical Pass<span className="required">*</span>
                    </label>
                    <input
                      type="number"
                      min={0}
                      value={form.practicalPass}
                      onChange={(e) => set("practicalPass", e.target.value)}
                    />
                  </div>
                </>
              ) : (
                <>
                  <div className="field">
                    <label>
                      Max Marks<span className="required">*</span>
                    </label>
                    <input
                      type="number"
                      min={0}
                      value={form.maxMarks}
                      onChange={(e) => set("maxMarks", e.target.value)}
                    />
                  </div>
                  <div className="field">
                    <label>
                      Pass Marks<span className="required">*</span>
                    </label>
                    <input
                      type="number"
                      min={0}
                      value={form.passMarks}
                      onChange={(e) => set("passMarks", e.target.value)}
                    />
                  </div>
                </>
              )}
            </div>

            <div className="modal__section-title">Status</div>
            <div className="checkbox-row" style={{ marginTop: 0 }}>
              <input
                type="checkbox"
                id="sub-active"
                checked={form.isActive}
                onChange={(e) => set("isActive", e.target.checked)}
              />
              <label htmlFor="sub-active">
                Active — available in marks entry, exams, timetable
              </label>
            </div>

            <div className="modal__note">
              <strong>Note:</strong> Subject Code + Class must be unique. The
              same code can exist in different classes. The teacher is assigned
              from the Teachers page (Manage).
            </div>
          </div>
          <div className="modal__footer">
            <button
              className="btn btn--primary"
              onClick={handleSubmit}
              disabled={isSubmitting}
            >
              {isSubmitting ? "Saving…" : editing ? "Save Changes" : "Save"}
            </button>
            <button className="btn btn--secondary" onClick={onClose}>
              Cancel
            </button>
          </div>
        </div>
      </div>
      <ValidationModal errors={errors} onClose={() => setErrors(null)} />
    </>
  );
}
