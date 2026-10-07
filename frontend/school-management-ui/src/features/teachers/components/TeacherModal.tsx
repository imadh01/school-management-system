import { useEffect, useState } from "react";
import { ValidationModal } from "@/components/ValidationModal";
import { getApiErrors } from "@/utils/apiError";
import {
  TEACHER_STATUSES,
  type CreateTeacherRequest,
  type TeacherResponse,
  type TeacherStatus,
  type UpdateTeacherRequest,
} from "../types/teacher.types";

interface Props {
  isOpen: boolean;
  onClose: () => void;
  editing: TeacherResponse | null;
  onCreate: (data: CreateTeacherRequest) => Promise<void>;
  onUpdate: (id: number, data: UpdateTeacherRequest) => Promise<void>;
}

interface FormState {
  name: string;
  username: string;
  email: string;
  phone: string;
  password: string; // create: required, edit: optional "new password"
  specialization: string;
  status: TeacherStatus;
}

const emptyForm: FormState = {
  name: "",
  username: "",
  email: "",
  phone: "",
  password: "",
  specialization: "",
  status: "Active",
};

// Mirrors the backend password rules (the backend is still authoritative).
function passwordProblems(password: string): string[] {
  const problems: string[] = [];
  if (password.length < 8) problems.push("at least 8 characters");
  if (!/[A-Z]/.test(password)) problems.push("an uppercase letter");
  if (!/[a-z]/.test(password)) problems.push("a lowercase letter");
  if (!/[0-9]/.test(password)) problems.push("a digit");
  if (!/[^a-zA-Z0-9]/.test(password)) problems.push("a special character");
  return problems;
}

export function TeacherModal({
  isOpen,
  onClose,
  editing,
  onCreate,
  onUpdate,
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
        username: editing.username,
        email: editing.email,
        phone: editing.phone ?? "",
        password: "",
        specialization: editing.specialization ?? "",
        status: editing.status as TeacherStatus,
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
    if (!form.name.trim()) problems.push("Full name is required.");
    if (!form.username.trim()) problems.push("Username is required.");
    if (!form.email.trim()) problems.push("Email is required.");
    else if (!/^\S+@\S+\.\S+$/.test(form.email.trim()))
      problems.push("Email is not a valid address.");

    if (!editing && !form.password) {
      problems.push("Login password is required.");
    }
    if (form.password) {
      const missing = passwordProblems(form.password);
      if (missing.length > 0)
        problems.push(`Password needs ${missing.join(", ")}.`);
    }
    if (problems.length > 0) {
      setErrors(problems);
      return;
    }

    setIsSubmitting(true);
    try {
      const common = {
        name: form.name.trim(),
        username: form.username.trim(),
        email: form.email.trim(),
        phone: form.phone.trim() || null,
        specialization: form.specialization.trim() || null,
        status: form.status,
      };
      if (editing) {
        await onUpdate(editing.id, {
          ...common,
          newPassword: form.password || null,
        });
      } else {
        await onCreate({ ...common, password: form.password });
      }
      onClose();
    } catch (err) {
      setErrors(getApiErrors(err, "Could not save this teacher."));
    } finally {
      setIsSubmitting(false);
    }
  };

  return (
    <>
      <div className="modal-backdrop open">
        <div className="modal">
          <div className="modal__header">
            <h2>{editing ? "✏️ Edit Teacher" : "👩‍🏫 Add Teacher"}</h2>
            <button className="modal__close" onClick={onClose}>
              ✕
            </button>
          </div>
          <div className="modal__body">
            <div className="modal__grid">
              <div className="field">
                <label>
                  Full Name<span className="required">*</span>
                </label>
                <input
                  value={form.name}
                  onChange={(e) => set("name", e.target.value)}
                />
              </div>
              <div className="field">
                <label>
                  Username<span className="required">*</span> (unique)
                </label>
                <input
                  value={form.username}
                  onChange={(e) => set("username", e.target.value)}
                  autoComplete="off"
                />
              </div>
              <div className="field">
                <label>
                  Email<span className="required">*</span> (unique)
                </label>
                <input
                  type="email"
                  value={form.email}
                  onChange={(e) => set("email", e.target.value)}
                />
              </div>
              <div className="field">
                <label>Phone</label>
                <input
                  value={form.phone}
                  onChange={(e) => set("phone", e.target.value)}
                />
              </div>
              <div className="field">
                <label>
                  {editing
                    ? "New Password (leave blank to keep)"
                    : "Login Password"}
                  {!editing && <span className="required">*</span>}
                </label>
                <input
                  type="password"
                  value={form.password}
                  onChange={(e) => set("password", e.target.value)}
                  autoComplete="new-password"
                />
              </div>
              <div className="field">
                <label>Specialization</label>
                <input
                  value={form.specialization}
                  onChange={(e) => set("specialization", e.target.value)}
                  placeholder="e.g. Mathematics"
                />
              </div>
              <div className="field" style={{ gridColumn: "span 2" }}>
                <label>Status</label>
                <select
                  value={form.status}
                  onChange={(e) =>
                    set("status", e.target.value as TeacherStatus)
                  }
                >
                  {TEACHER_STATUSES.map((s) => (
                    <option key={s}>{s}</option>
                  ))}
                </select>
              </div>
            </div>
            <div className="modal__note">
              <strong>Note:</strong> Password needs 8+ characters with an
              uppercase letter, a lowercase letter, a digit and a special
              character. Inactive or Suspended teachers cannot log in.
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
