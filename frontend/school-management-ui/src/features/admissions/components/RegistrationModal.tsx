import { useEffect, useState } from "react";
import type {
  AdmissionGuardianRequest,
  AdmissionResponse,
  CreateAdmissionRequest,
  GuardianRelation,
} from "../types/admission.types";
import type { ClassSectionResponse } from "@/features/class-sections/types/classSection.types";
import { ValidationModal } from "@/components/ValidationModal";
import { getApiErrors } from "@/utils/apiError";

interface Props {
  isOpen: boolean;
  editingAdmission: AdmissionResponse | null;
  classSections: ClassSectionResponse[];
  onClose: () => void;
  onSubmit: (data: CreateAdmissionRequest) => Promise<void>;
}

const MAX_GUARDIANS = 5;

const emptyGuardian = (
  relationType: GuardianRelation,
): AdmissionGuardianRequest => ({
  relationType,
  name: "",
  mobile: "",
  email: "",
  isPrimaryContact: false,
});

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
  addressLine: "",
  city: "",
  state: "",
  pincode: "",
  remarks: "",
  guardians: [],
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
  const [errors, setErrors] = useState<string[] | null>(null);

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
        addressLine: editingAdmission.addressLine,
        city: editingAdmission.city,
        state: editingAdmission.state,
        pincode: editingAdmission.pincode,
        remarks: editingAdmission.remarks,
        guardians: editingAdmission.guardians.map((g) => ({
          relationType: g.relationType,
          name: g.name,
          mobile: g.mobile,
          email: g.email,
          isPrimaryContact: g.isPrimaryContact,
        })),
      });
    } else {
      setForm(emptyForm);
    }
    setErrors(null);
  }, [editingAdmission, isOpen]);

  if (!isOpen) return null;

  const field = (key: keyof CreateAdmissionRequest, value: string) =>
    setForm((f) => ({ ...f, [key]: value === "" ? null : value }));

  const selectedClassSection = classSections.find(
    (c) => c.id === form.appliedForClassSectionId,
  );

  // ----- guardian rows -----
  const updateGuardian = (
    index: number,
    patch: Partial<AdmissionGuardianRequest>,
  ) =>
    setForm((f) => ({
      ...f,
      guardians: f.guardians.map((g, i) =>
        i === index ? { ...g, ...patch } : g,
      ),
    }));

  const setPrimary = (index: number) =>
    setForm((f) => ({
      ...f,
      guardians: f.guardians.map((g, i) => ({
        ...g,
        isPrimaryContact: i === index,
      })),
    }));

  const addGuardian = () => {
    if (form.guardians.length >= MAX_GUARDIANS) return;
    // Suggest the next missing relation so the common case is one click.
    const has = (r: GuardianRelation) =>
      form.guardians.some((g) => g.relationType === r);
    const next: GuardianRelation = !has("Father")
      ? "Father"
      : !has("Mother")
        ? "Mother"
        : "Guardian";
    setForm((f) => ({
      ...f,
      guardians: [...f.guardians, emptyGuardian(next)],
    }));
  };

  const removeGuardian = (index: number) =>
    setForm((f) => ({
      ...f,
      guardians: f.guardians.filter((_, i) => i !== index),
    }));

  const handleSubmit = async () => {
    const problems: string[] = [];
    if (!form.firstName) problems.push("First name is required.");
    if (!form.lastName) problems.push("Last name is required.");
    if (!form.dateOfBirth) problems.push("Date of birth is required.");
    if (!form.phone) problems.push("Phone is required.");
    if (!form.appliedForClassSectionId)
      problems.push("Applied class is required.");

    // A row left completely blank is simply ignored; a half-filled one is an error.
    const guardians = form.guardians.filter((g) => g.name.trim() || g.mobile);
    guardians.forEach((g, i) => {
      if (!g.name.trim()) problems.push(`Guardian ${i + 1}: name is required.`);
    });
    if (guardians.filter((g) => g.relationType === "Father").length > 1)
      problems.push("Only one father can be added.");
    if (guardians.filter((g) => g.relationType === "Mother").length > 1)
      problems.push("Only one mother can be added.");

    if (problems.length > 0) {
      setErrors(problems);
      return;
    }

    // If staff did not tick a primary contact, the first guardian is primary.
    const normalizedGuardians = guardians.map((g, i) => ({
      ...g,
      name: g.name.trim(),
      mobile: g.mobile?.trim() || null,
      email: g.email?.trim() || null,
      isPrimaryContact: guardians.some((x) => x.isPrimaryContact)
        ? g.isPrimaryContact
        : i === 0,
    }));

    setIsSubmitting(true);
    try {
      const sanitized = {
        ...Object.fromEntries(
          Object.entries(form).map(([key, value]) => [
            key,
            value === "" ? null : value,
          ]),
        ),
        guardians: normalizedGuardians,
      } as CreateAdmissionRequest;

      await onSubmit(sanitized);
      onClose();
    } catch (err) {
      setErrors(
        getApiErrors(
          err,
          "Could not save this registration. Check the details and try again.",
        ),
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
                {classSections
                  .filter((c) => c.status === "Active")
                  .map((c) => (
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
              <label>Academic Year</label>
              <input
                value={selectedClassSection?.academicYearName ?? "—"}
                disabled
              />
              <span style={{ fontSize: 11, color: "var(--muted)" }}>
                Determined by the selected class
              </span>
            </div>
            <div className="field" style={{ gridColumn: "span 2" }}>
              <label>Previous School</label>
              <input
                value={form.previousSchool ?? ""}
                onChange={(e) => field("previousSchool", e.target.value)}
              />
            </div>
          </div>

          <div className="modal__section-title">Parents / Guardians</div>
          <div className="modal__note" style={{ marginTop: 0 }}>
            Optional while the application is a draft, but at least one guardian
            with a mobile number is needed before enrolment. The mobile number
            is used to find parents already in the system.
          </div>
          {form.guardians.length === 0 && (
            <div className="empty-state" style={{ padding: 12 }}>
              No guardians added yet.
            </div>
          )}
          {form.guardians.map((g, i) => (
            <div
              key={i}
              className="modal__grid"
              style={{
                borderBottom: "1px solid var(--border, #e5e7eb)",
                paddingBottom: 12,
                marginBottom: 12,
                alignItems: "end",
              }}
            >
              <div className="field">
                <label>Relation</label>
                <select
                  value={g.relationType}
                  onChange={(e) =>
                    updateGuardian(i, {
                      relationType: e.target.value as GuardianRelation,
                    })
                  }
                >
                  <option value="Father">Father</option>
                  <option value="Mother">Mother</option>
                  <option value="Guardian">Guardian</option>
                </select>
              </div>
              <div className="field">
                <label>
                  Name<span className="required">*</span>
                </label>
                <input
                  value={g.name}
                  onChange={(e) => updateGuardian(i, { name: e.target.value })}
                />
              </div>
              <div className="field">
                <label>Mobile</label>
                <input
                  value={g.mobile ?? ""}
                  onChange={(e) =>
                    updateGuardian(i, { mobile: e.target.value })
                  }
                />
              </div>
              <div className="field">
                <label>Email</label>
                <input
                  type="email"
                  value={g.email ?? ""}
                  onChange={(e) => updateGuardian(i, { email: e.target.value })}
                />
              </div>
              <div className="field">
                <label
                  style={{ display: "flex", alignItems: "center", gap: 6 }}
                >
                  <input
                    type="radio"
                    name="primaryGuardian"
                    checked={g.isPrimaryContact}
                    onChange={() => setPrimary(i)}
                  />
                  Primary contact
                </label>
              </div>
              <div className="field">
                <button
                  type="button"
                  className="btn btn--secondary btn--sm"
                  onClick={() => removeGuardian(i)}
                >
                  Remove
                </button>
              </div>
            </div>
          ))}
          <button
            type="button"
            className="btn btn--secondary btn--sm"
            onClick={addGuardian}
            disabled={form.guardians.length >= MAX_GUARDIANS}
          >
            + Add guardian
          </button>

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

          <div className="modal__section-title">Remarks</div>
          <div className="field">
            <textarea
              rows={3}
              value={form.remarks ?? ""}
              onChange={(e) => field("remarks", e.target.value)}
              placeholder="Optional notes about this application"
            />
          </div>
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
      <ValidationModal errors={errors} onClose={() => setErrors(null)} />
    </div>
  );
}
