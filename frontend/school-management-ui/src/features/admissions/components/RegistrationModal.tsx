import { useEffect, useState } from "react";
import type {
  AdmissionResponse,
  CreateAdmissionRequest,
} from "../types/admission.types";
import type { ClassSectionResponse } from "@/features/class-sections/types/classSection.types";

interface Props {
  isOpen: boolean;
  editingAdmission: AdmissionResponse | null; // null = creating new
  classSections: ClassSectionResponse[];
  onClose: () => void;
  onSubmit: (data: CreateAdmissionRequest) => Promise<void>;
}

const emptyForm: CreateAdmissionRequest = {
  firstName: "",
  middleName: "",
  lastName: "",
  gender: "Male",
  dateOfBirth: "",
  appliedForClassSectionId: 0,
  admissionType: "New",
  previousSchool: "",
  phone: "",
  email: "",
  fatherName: "",
  fatherMobile: "",
  motherName: "",
  motherMobile: "",
  guardianName: "",
  guardianRelation: "",
  guardianMobile: "",
  addressLine: "",
  city: "",
  state: "",
  pincode: "",
};

export function RegistrationModal({
  isOpen,
  editingAdmission,
  classSections,
  onClose,
  onSubmit,
}: Props) {
  const [form, setForm] = useState<CreateAdmissionRequest>(emptyForm);
  const [isSubmitting, setIsSubmitting] = useState(false);
  const [error, setError] = useState<string | null>(null);

  useEffect(() => {
    if (editingAdmission) {
      setForm({
        firstName: editingAdmission.firstName,
        middleName: editingAdmission.middleName,
        lastName: editingAdmission.lastName,
        gender: editingAdmission.gender,
        dateOfBirth: editingAdmission.dateOfBirth,
        appliedForClassSectionId: editingAdmission.appliedForClassSectionId,
        admissionType: editingAdmission.admissionType,
        previousSchool: editingAdmission.previousSchool,
        phone: editingAdmission.phone,
        email: editingAdmission.email,
        fatherName: editingAdmission.fatherName,
        fatherMobile: editingAdmission.fatherMobile,
        motherName: editingAdmission.motherName,
        motherMobile: editingAdmission.motherMobile,
        guardianName: editingAdmission.guardianName,
        guardianRelation: editingAdmission.guardianRelation,
        guardianMobile: editingAdmission.guardianMobile,
        addressLine: editingAdmission.addressLine,
        city: editingAdmission.city,
        state: editingAdmission.state,
        pincode: editingAdmission.pincode,
      });
    } else {
      setForm(emptyForm);
    }
    setError(null);
  }, [editingAdmission, isOpen]);

  if (!isOpen) return null;

  const field = (key: keyof CreateAdmissionRequest, value: string) =>
    setForm((f) => ({ ...f, [key]: value === "" ? null : value }));

  const handleSubmit = async () => {
    setError(null);
    if (
      !form.firstName ||
      !form.lastName ||
      !form.phone ||
      !form.dateOfBirth ||
      !form.appliedForClassSectionId
    ) {
      setError(
        "First name, last name, phone, date of birth, and applied class are required.",
      );
      return;
    }
    setIsSubmitting(true);
    try {
      await onSubmit(form);
      onClose();
    } catch {
      setError(
        "Could not save this registration. Check the details and try again.",
      );
    } finally {
      setIsSubmitting(false);
    }
  };

  return (
    <div className="modal-backdrop">
      <div className="modal modal--wide">
        <div className="modal__header">
          <h2>
            📝 {editingAdmission ? "Edit Registration" : "New Registration"}
          </h2>
          <button className="modal__close" onClick={onClose}>
            ✕
          </button>
        </div>
        <div className="modal__body">
          <div className="modal__section-title">Applicant</div>
          <div className="modal__grid">
            <div className="field">
              <label>
                First Name<span className="required">*</span>
              </label>
              <input
                value={form.firstName}
                onChange={(e) => field("firstName", e.target.value)}
              />
            </div>
            <div className="field">
              <label>Middle Name</label>
              <input
                value={form.middleName ?? ""}
                onChange={(e) => field("middleName", e.target.value)}
              />
            </div>
            <div className="field">
              <label>
                Last Name<span className="required">*</span>
              </label>
              <input
                value={form.lastName}
                onChange={(e) => field("lastName", e.target.value)}
              />
            </div>
            <div className="field">
              <label>
                Gender<span className="required">*</span>
              </label>
              <select
                value={form.gender}
                onChange={(e) => field("gender", e.target.value)}
              >
                <option value="Male">Male</option>
                <option value="Female">Female</option>
                <option value="Other">Other</option>
              </select>
            </div>
            <div className="field">
              <label>
                Date of Birth<span className="required">*</span>
              </label>
              <input
                type="date"
                value={form.dateOfBirth}
                onChange={(e) => field("dateOfBirth", e.target.value)}
              />
            </div>
          </div>

          <div className="modal__section-title">Applying For</div>
          <div className="modal__grid">
            <div className="field">
              <label>
                Class Section<span className="required">*</span>
              </label>
              <select
                value={form.appliedForClassSectionId || ""}
                onChange={(e) =>
                  setForm((f) => ({
                    ...f,
                    appliedForClassSectionId: Number(e.target.value),
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
              <label>Admission Type</label>
              <select
                value={form.admissionType}
                onChange={(e) => field("admissionType", e.target.value)}
              >
                <option value="New">New</option>
                <option value="Transfer">Transfer</option>
              </select>
            </div>
            <div className="field">
              <label>Previous School</label>
              <input
                value={form.previousSchool ?? ""}
                onChange={(e) => field("previousSchool", e.target.value)}
              />
            </div>
          </div>

          <div className="modal__section-title">Parents / Guardian</div>
          <div className="modal__grid">
            <div className="field">
              <label>Father's Name</label>
              <input
                value={form.fatherName ?? ""}
                onChange={(e) => field("fatherName", e.target.value)}
              />
            </div>
            <div className="field">
              <label>Father's Mobile</label>
              <input
                value={form.fatherMobile ?? ""}
                onChange={(e) => field("fatherMobile", e.target.value)}
              />
            </div>
            <div className="field">
              <label>Mother's Name</label>
              <input
                value={form.motherName ?? ""}
                onChange={(e) => field("motherName", e.target.value)}
              />
            </div>
            <div className="field">
              <label>Mother's Mobile</label>
              <input
                value={form.motherMobile ?? ""}
                onChange={(e) => field("motherMobile", e.target.value)}
              />
            </div>
            <div className="field">
              <label>Guardian's Name</label>
              <input
                value={form.guardianName ?? ""}
                onChange={(e) => field("guardianName", e.target.value)}
              />
            </div>
            <div className="field">
              <label>Guardian Relation</label>
              <input
                value={form.guardianRelation ?? ""}
                onChange={(e) => field("guardianRelation", e.target.value)}
              />
            </div>
            <div className="field">
              <label>Guardian's Mobile</label>
              <input
                value={form.guardianMobile ?? ""}
                onChange={(e) => field("guardianMobile", e.target.value)}
              />
            </div>
          </div>

          <div className="modal__section-title">Contact &amp; Address</div>
          <div className="modal__grid">
            <div className="field">
              <label>
                Phone<span className="required">*</span>
              </label>
              <input
                value={form.phone}
                onChange={(e) => field("phone", e.target.value)}
              />
            </div>
            <div className="field">
              <label>Email</label>
              <input
                type="email"
                value={form.email ?? ""}
                onChange={(e) => field("email", e.target.value)}
              />
            </div>
            <div className="field">
              <label>Address</label>
              <input
                value={form.addressLine ?? ""}
                onChange={(e) => field("addressLine", e.target.value)}
              />
            </div>
            <div className="field">
              <label>City</label>
              <input
                value={form.city ?? ""}
                onChange={(e) => field("city", e.target.value)}
              />
            </div>
            <div className="field">
              <label>State</label>
              <input
                value={form.state ?? ""}
                onChange={(e) => field("state", e.target.value)}
              />
            </div>
            <div className="field">
              <label>Pincode</label>
              <input
                value={form.pincode ?? ""}
                onChange={(e) => field("pincode", e.target.value)}
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
            {isSubmitting
              ? "Saving…"
              : editingAdmission
                ? "Save Changes"
                : "Save Registration"}
          </button>
          <button className="btn btn--secondary" onClick={onClose}>
            Cancel
          </button>
        </div>
      </div>
    </div>
  );
}
