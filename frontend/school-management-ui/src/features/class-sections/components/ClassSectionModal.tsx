import { useEffect, useState } from "react";
import { ValidationModal } from "@/components/ValidationModal";
import { getApiErrors } from "@/utils/apiError";
import {
  CLASS_MEDIUMS,
  CLASS_STAGES,
  CLASS_STREAMS,
  type ClassSectionResponse,
  type UpdateClassSectionRequest,
} from "../types/classSection.types";

interface Props {
  isOpen: boolean;
  onClose: () => void;
  editing: ClassSectionResponse | null;
  onSave: (data: UpdateClassSectionRequest) => Promise<void>;
}

interface FormState {
  name: string;
  section: string;
  grade: string;
  stage: string;
  medium: string;
  stream: string;
  capacity: string;
  building: string;
  floor: string;
  room: string;
  isActive: boolean;
}

const emptyForm: FormState = {
  name: "",
  section: "",
  grade: "",
  stage: "Primary",
  medium: "English",
  stream: "General",
  capacity: "40",
  building: "",
  floor: "",
  room: "",
  isActive: true,
};

// Mirrors ClassSection.Code on the backend. Preview only — the server is
// the source of truth.
function previewCode(name: string, section: string) {
  if (!name.trim() || !section.trim()) return "";
  return (
    name.trim().replace("Class ", "C").replace(/ /g, "") + section.trim()
  ).toUpperCase();
}

export function ClassSectionModal({ isOpen, onClose, editing, onSave }: Props) {
  const [form, setForm] = useState<FormState>(emptyForm);
  const [isSubmitting, setIsSubmitting] = useState(false);
  const [errors, setErrors] = useState<string[] | null>(null);

  useEffect(() => {
    if (!isOpen) return;
    setErrors(null);
    if (editing) {
      setForm({
        name: editing.name,
        section: editing.section,
        grade: editing.grade?.toString() ?? "",
        stage: editing.stage,
        medium: editing.medium,
        stream: editing.stream,
        capacity: editing.capacity?.toString() ?? "",
        building: editing.building ?? "",
        floor: editing.floor?.toString() ?? "",
        room: editing.room ?? "",
        isActive: editing.status === "Active",
      });
    } else {
      setForm(emptyForm);
    }
  }, [isOpen, editing]);

  if (!isOpen) return null;

  const set = <K extends keyof FormState>(key: K, value: FormState[K]) =>
    setForm((f) => ({ ...f, [key]: value }));

  const handleSubmit = async () => {
    const problems: string[] = [];
    if (!form.name.trim()) problems.push("Class name is required.");
    if (!form.section.trim()) problems.push("Section is required.");
    const capacity = Number(form.capacity);
    if (!form.capacity || !Number.isInteger(capacity) || capacity < 1)
      problems.push("Capacity must be a whole number of at least 1.");
    if (form.grade && !Number.isInteger(Number(form.grade)))
      problems.push("Grade must be a whole number.");
    if (form.floor && !Number.isInteger(Number(form.floor)))
      problems.push("Floor must be a whole number.");
    if (problems.length > 0) {
      setErrors(problems);
      return;
    }

    setIsSubmitting(true);
    try {
      await onSave({
        name: form.name.trim(),
        section: form.section.trim().toUpperCase(),
        grade: form.grade ? Number(form.grade) : null,
        stage: form.stage,
        medium: form.medium,
        stream: form.stream,
        capacity,
        building: form.building.trim() || null,
        floor: form.floor ? Number(form.floor) : null,
        room: form.room.trim() || null,
        isActive: form.isActive,
      });
      onClose();
    } catch (err) {
      setErrors(getApiErrors(err, "Could not save this class."));
    } finally {
      setIsSubmitting(false);
    }
  };

  const code = previewCode(form.name, form.section);

  return (
    <>
      <div className="modal-backdrop open">
        <div className="modal modal--wide">
          <div className="modal__header">
            <h2>{editing ? "✏️ Edit Class" : "🏫 Add Class"}</h2>
            <button className="modal__close" onClick={onClose}>
              ✕
            </button>
          </div>
          <div className="modal__body">
            <div className="modal__section-title">Class</div>
            <div className="modal__grid">
              <div className="field">
                <label>
                  Class Name<span className="required">*</span>
                </label>
                <input
                  value={form.name}
                  onChange={(e) => set("name", e.target.value)}
                  placeholder="e.g. Class 5, LKG"
                />
              </div>
              <div className="field">
                <label>
                  Section<span className="required">*</span>
                </label>
                <input
                  value={form.section}
                  maxLength={5}
                  onChange={(e) => set("section", e.target.value.toUpperCase())}
                  placeholder="A"
                />
              </div>
              <div className="field">
                <label>Grade (number, blank for LKG/UKG)</label>
                <input
                  type="number"
                  min={1}
                  value={form.grade}
                  onChange={(e) => set("grade", e.target.value)}
                />
              </div>
              <div className="field">
                <label>Code (auto)</label>
                <input
                  value={editing && !code ? editing.code : code}
                  disabled
                />
              </div>
              <div className="field">
                <label>Academic Year</label>
                <input
                  value={
                    editing ? editing.academicYearName : "Current academic year"
                  }
                  disabled
                />
              </div>
              <div className="field" style={{ alignSelf: "end" }}>
                <div className="checkbox-row" style={{ marginTop: 0 }}>
                  <input
                    type="checkbox"
                    id="cs-active"
                    checked={form.isActive}
                    onChange={(e) => set("isActive", e.target.checked)}
                  />
                  <label htmlFor="cs-active">Active</label>
                </div>
              </div>
            </div>

            <div className="modal__section-title">Academic</div>
            <div className="modal__grid">
              <div className="field">
                <label>Stage</label>
                <select
                  value={form.stage}
                  onChange={(e) => set("stage", e.target.value)}
                >
                  {CLASS_STAGES.map((s) => (
                    <option key={s}>{s}</option>
                  ))}
                </select>
              </div>
              <div className="field">
                <label>Medium</label>
                <select
                  value={form.medium}
                  onChange={(e) => set("medium", e.target.value)}
                >
                  {CLASS_MEDIUMS.map((m) => (
                    <option key={m}>{m}</option>
                  ))}
                </select>
              </div>
              <div className="field">
                <label>Stream</label>
                <select
                  value={form.stream}
                  onChange={(e) => set("stream", e.target.value)}
                >
                  {CLASS_STREAMS.map((s) => (
                    <option key={s}>{s}</option>
                  ))}
                </select>
              </div>
              <div className="field">
                <label>
                  Capacity<span className="required">*</span>
                </label>
                <input
                  type="number"
                  min={1}
                  value={form.capacity}
                  onChange={(e) => set("capacity", e.target.value)}
                />
              </div>
            </div>

            <div className="modal__section-title">Location</div>
            <div className="modal__grid">
              <div className="field">
                <label>Building</label>
                <input
                  value={form.building}
                  onChange={(e) => set("building", e.target.value)}
                  placeholder="Main"
                />
              </div>
              <div className="field">
                <label>Floor</label>
                <input
                  type="number"
                  min={0}
                  value={form.floor}
                  onChange={(e) => set("floor", e.target.value)}
                />
              </div>
              <div className="field">
                <label>Room</label>
                <input
                  value={form.room}
                  onChange={(e) => set("room", e.target.value)}
                  placeholder="101"
                />
              </div>
            </div>

            <div className="modal__note">
              <strong>Note:</strong> A class name + section can exist only once
              per academic year. Capacity cannot be set below the number of
              students already enrolled. Class teacher assignment comes with the
              Teachers module.
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
