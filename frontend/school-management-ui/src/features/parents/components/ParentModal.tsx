import { useEffect, useState } from "react";
import type {
  CreateParentRequest,
  UpdateParentRequest,
  ParentResponse,
} from "../types/parent.types";

interface Props {
  isOpen: boolean;
  onClose: () => void;
  editingParent: ParentResponse | null;
  onCreate: (data: CreateParentRequest) => Promise<void>;
  onUpdate: (id: number, data: UpdateParentRequest) => Promise<void>;
}

type FormState = CreateParentRequest & { status: "Active" | "Inactive" };

const emptyForm: FormState = {
  name: "",
  email: "",
  mobile: "",
  status: "Active",
  occupation: "",
  nationality: "",
  countryOfResidence: "",
  timezone: "",
  preferredLanguage: "English",
  preferredContactMethod: "Email",
  whatsapp: "",
  emergencyOnly: false,
  notifyAttendance: true,
  notifyExams: true,
  notifyFees: true,
  notifyNotices: true,
  notifyDiscipline: true,
  employer: "",
  jobTitle: "",
  workEmail: "",
  workPhone: "",
  billingContact: false,
  addressLine: "",
  city: "",
  state: "",
  pincode: "",
};

const NOTIFY_KEYS = [
  { key: "notifyAttendance" as const, label: "Attendance" },
  { key: "notifyExams" as const, label: "Exams" },
  { key: "notifyFees" as const, label: "Fees" },
  { key: "notifyNotices" as const, label: "Notices" },
  { key: "notifyDiscipline" as const, label: "Discipline" },
];

export function ParentModal({
  isOpen,
  onClose,
  editingParent,
  onCreate,
  onUpdate,
}: Props) {
  const [form, setForm] = useState<FormState>(emptyForm);
  const [isSubmitting, setIsSubmitting] = useState(false);
  const [error, setError] = useState<string | null>(null);

  useEffect(() => {
    if (!isOpen) return;
    setError(null);
    if (editingParent) {
      setForm({
        name: editingParent.name,
        email: editingParent.email ?? "",
        mobile: editingParent.mobile,
        status: editingParent.status,
        occupation: editingParent.occupation ?? "",
        nationality: editingParent.nationality ?? "",
        countryOfResidence: editingParent.countryOfResidence ?? "",
        timezone: editingParent.timezone ?? "",
        preferredLanguage: editingParent.preferredLanguage ?? "English",
        preferredContactMethod: editingParent.preferredContactMethod ?? "Email",
        whatsapp: editingParent.whatsapp ?? "",
        emergencyOnly: editingParent.emergencyOnly,
        notifyAttendance: editingParent.notifyAttendance,
        notifyExams: editingParent.notifyExams,
        notifyFees: editingParent.notifyFees,
        notifyNotices: editingParent.notifyNotices,
        notifyDiscipline: editingParent.notifyDiscipline,
        employer: editingParent.employer ?? "",
        jobTitle: editingParent.jobTitle ?? "",
        workEmail: editingParent.workEmail ?? "",
        workPhone: editingParent.workPhone ?? "",
        billingContact: editingParent.billingContact,
        addressLine: editingParent.addressLine ?? "",
        city: editingParent.city ?? "",
        state: editingParent.state ?? "",
        pincode: editingParent.pincode ?? "",
      });
    } else {
      setForm(emptyForm);
    }
  }, [isOpen, editingParent]);

  if (!isOpen) return null;

  const field = (key: keyof FormState, value: string) =>
    setForm((f) => ({ ...f, [key]: value }));

  const toggleNotify = (key: (typeof NOTIFY_KEYS)[number]["key"]) =>
    setForm((f) => ({ ...f, [key]: !f[key] }));

  const handleSubmit = async () => {
    setError(null);
    if (!form.name || !form.mobile) {
      setError("Name and mobile are required.");
      return;
    }
    setIsSubmitting(true);
    try {
      // Sanitize empty strings to null for optional fields before sending.
      const sanitized = Object.fromEntries(
        Object.entries(form).map(([k, v]) => [k, v === "" ? null : v]),
      ) as unknown as FormState;

      if (editingParent) {
        await onUpdate(editingParent.id, sanitized);
      } else {
        const { status, ...createData } = sanitized;
        void status;
        await onCreate(createData);
      }
      onClose();
    } catch {
      setError("Could not save this parent.");
    } finally {
      setIsSubmitting(false);
    }
  };

  return (
    <div className="modal-backdrop open">
      <div className="modal modal--wide">
        <div className="modal__header">
          <h2>{editingParent ? "✏️ Edit Parent" : "👥 Add Parent"}</h2>
          <button className="modal__close" onClick={onClose}>
            ✕
          </button>
        </div>
        <div className="modal__body">
          <div className="modal__section-title">Basics</div>
          <div className="modal__grid">
            <div className="field">
              <label>
                Full Name<span className="required">*</span>
              </label>
              <input
                value={form.name}
                onChange={(e) => field("name", e.target.value)}
              />
            </div>
            {editingParent && (
              <div className="field">
                <label>Status</label>
                <select
                  value={form.status}
                  onChange={(e) => field("status", e.target.value)}
                >
                  <option>Active</option>
                  <option>Inactive</option>
                </select>
              </div>
            )}
            <div className="field">
              <label>Email (unique when set)</label>
              <input
                type="email"
                value={form.email ?? ""}
                onChange={(e) => field("email", e.target.value)}
              />
            </div>
            <div className="field">
              <label>
                Mobile<span className="required">*</span> (unique)
              </label>
              <input
                value={form.mobile}
                onChange={(e) => field("mobile", e.target.value)}
                placeholder="+971 50 0000000"
              />
            </div>
            <div className="field">
              <label>Occupation</label>
              <input
                value={form.occupation ?? ""}
                onChange={(e) => field("occupation", e.target.value)}
              />
            </div>
          </div>

          <div className="modal__section-title">Identity &amp; Residence</div>
          <div className="modal__grid">
            <div className="field">
              <label>Nationality</label>
              <input
                value={form.nationality ?? ""}
                onChange={(e) => field("nationality", e.target.value)}
              />
            </div>
            <div className="field">
              <label>Country of Residence</label>
              <input
                value={form.countryOfResidence ?? ""}
                onChange={(e) => field("countryOfResidence", e.target.value)}
              />
            </div>
            <div className="field" style={{ gridColumn: "span 2" }}>
              <label>Time Zone</label>
              <input
                value={form.timezone ?? ""}
                onChange={(e) => field("timezone", e.target.value)}
                placeholder="UTC"
              />
            </div>
          </div>

          <div className="modal__section-title">Communication</div>
          <div className="modal__grid">
            <div className="field">
              <label>Preferred Language</label>
              <select
                value={form.preferredLanguage ?? "English"}
                onChange={(e) => field("preferredLanguage", e.target.value)}
              >
                <option>English</option>
                <option>Hindi</option>
                <option>Urdu</option>
                <option>Arabic</option>
                <option>Korean</option>
              </select>
            </div>
            <div className="field">
              <label>Preferred Contact Method</label>
              <select
                value={form.preferredContactMethod ?? "Email"}
                onChange={(e) =>
                  field("preferredContactMethod", e.target.value)
                }
              >
                <option>Email</option>
                <option>Phone</option>
                <option>WhatsApp</option>
                <option>App</option>
              </select>
            </div>
            <div className="field">
              <label>WhatsApp Number</label>
              <input
                value={form.whatsapp ?? ""}
                onChange={(e) => field("whatsapp", e.target.value)}
              />
            </div>
            <div className="field" style={{ alignSelf: "end" }}>
              <div className="checkbox-row" style={{ marginTop: 0 }}>
                <input
                  type="checkbox"
                  id="p-emergency"
                  checked={form.emergencyOnly}
                  onChange={(e) =>
                    setForm((f) => ({ ...f, emergencyOnly: e.target.checked }))
                  }
                />
                <label htmlFor="p-emergency">Emergency Only</label>
              </div>
            </div>
          </div>

          <div className="field" style={{ marginTop: 12 }}>
            <label>Notify me about:</label>
            <div className="tag-group">
              {NOTIFY_KEYS.map(({ key, label }) => (
                <button
                  key={key}
                  type="button"
                  className={`tag-btn ${form[key] ? "selected" : ""}`}
                  onClick={() => toggleNotify(key)}
                >
                  {form[key] ? "✓ " : ""}
                  {label}
                </button>
              ))}
            </div>
          </div>

          <div className="modal__section-title">Work</div>
          <div className="modal__grid">
            <div className="field">
              <label>Employer</label>
              <input
                value={form.employer ?? ""}
                onChange={(e) => field("employer", e.target.value)}
              />
            </div>
            <div className="field">
              <label>Job Title</label>
              <input
                value={form.jobTitle ?? ""}
                onChange={(e) => field("jobTitle", e.target.value)}
              />
            </div>
            <div className="field">
              <label>Work Email</label>
              <input
                type="email"
                value={form.workEmail ?? ""}
                onChange={(e) => field("workEmail", e.target.value)}
              />
            </div>
            <div className="field">
              <label>Work Phone</label>
              <input
                value={form.workPhone ?? ""}
                onChange={(e) => field("workPhone", e.target.value)}
              />
            </div>
          </div>
          <div className="checkbox-row">
            <input
              type="checkbox"
              id="p-billing"
              checked={form.billingContact}
              onChange={(e) =>
                setForm((f) => ({ ...f, billingContact: e.target.checked }))
              }
            />
            <label htmlFor="p-billing">
              Preferred Billing Contact — send fee invoices and billing notices
              to this parent
            </label>
          </div>

          <div className="modal__section-title">Address</div>
          <div className="modal__grid">
            <div className="field" style={{ gridColumn: "span 2" }}>
              <label>Street Address</label>
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
            <div className="field" style={{ gridColumn: "span 2" }}>
              <label>PIN / ZIP Code</label>
              <input
                value={form.pincode ?? ""}
                onChange={(e) => field("pincode", e.target.value)}
              />
            </div>
          </div>

          <div className="modal__note">
            <strong>Note:</strong> Mobile number must be unique across all
            parents. Linking to children is done separately from the Linked
            Children action on each row.
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
            {isSubmitting ? "Saving…" : editingParent ? "Save Changes" : "Save"}
          </button>
          <button className="btn btn--secondary" onClick={onClose}>
            Cancel
          </button>
        </div>
      </div>
    </div>
  );
}
