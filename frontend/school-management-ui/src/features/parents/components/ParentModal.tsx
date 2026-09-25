import { useState } from "react";
import type { CreateParentRequest } from "../types/parent.types";

interface Props {
  isOpen: boolean;
  onClose: () => void;
  onSubmit: (data: CreateParentRequest) => Promise<void>;
}

const emptyForm: CreateParentRequest = {
  name: "",
  email: "",
  mobile: "",
  occupation: "",
  nationality: "",
  countryOfResidence: "",
  timezone: "",
  preferredLanguage: "",
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

export function ParentModal({ isOpen, onClose, onSubmit }: Props) {
  const [form, setForm] = useState<CreateParentRequest>(emptyForm);
  const [isSubmitting, setIsSubmitting] = useState(false);
  const [error, setError] = useState<string | null>(null);

  if (!isOpen) return null;

  const field = (key: keyof CreateParentRequest, value: string) =>
    setForm((f) => ({ ...f, [key]: value === "" ? null : value }));

  const handleSubmit = async () => {
    setError(null);
    if (!form.name || !form.mobile) {
      setError("Name and mobile are required.");
      return;
    }
    setIsSubmitting(true);
    try {
      await onSubmit(form);
      setForm(emptyForm);
      onClose();
    } catch {
      setError("Could not save this parent.");
    } finally {
      setIsSubmitting(false);
    }
  };

  return (
    <div className="modal-backdrop">
      <div className="modal modal--wide">
        <div className="modal__header">
          <h2>👪 Add Parent</h2>
          <button className="modal__close" onClick={onClose}>
            ✕
          </button>
        </div>
        <div className="modal__body">
          <div className="modal__section-title">Identity &amp; Contact</div>
          <div className="modal__grid">
            <div className="field">
              <label>
                Name<span className="required">*</span>
              </label>
              <input
                value={form.name}
                onChange={(e) =>
                  setForm((f) => ({ ...f, name: e.target.value }))
                }
              />
            </div>
            <div className="field">
              <label>
                Mobile<span className="required">*</span>
              </label>
              <input
                value={form.mobile}
                onChange={(e) =>
                  setForm((f) => ({ ...f, mobile: e.target.value }))
                }
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
              <label>WhatsApp</label>
              <input
                value={form.whatsapp ?? ""}
                onChange={(e) => field("whatsapp", e.target.value)}
              />
            </div>
            <div className="field">
              <label>Preferred Contact Method</label>
              <select
                value={form.preferredContactMethod ?? ""}
                onChange={(e) =>
                  field("preferredContactMethod", e.target.value)
                }
              >
                <option value="Email">Email</option>
                <option value="Mobile">Mobile</option>
                <option value="WhatsApp">WhatsApp</option>
              </select>
            </div>
          </div>

          <div className="modal__section-title">Background</div>
          <div className="modal__grid">
            <div className="field">
              <label>Occupation</label>
              <input
                value={form.occupation ?? ""}
                onChange={(e) => field("occupation", e.target.value)}
              />
            </div>
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
            <div className="field">
              <label>Preferred Language</label>
              <input
                value={form.preferredLanguage ?? ""}
                onChange={(e) => field("preferredLanguage", e.target.value)}
              />
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

          <div className="modal__section-title">Address</div>
          <div className="modal__grid">
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

          <div className="modal__section-title">Preferences</div>
          <div className="modal__grid">
            <div className="field">
              <label>
                <input
                  type="checkbox"
                  checked={form.billingContact}
                  onChange={(e) =>
                    setForm((f) => ({ ...f, billingContact: e.target.checked }))
                  }
                  style={{ marginRight: 8 }}
                />
                Billing contact
              </label>
            </div>
            <div className="field">
              <label>
                <input
                  type="checkbox"
                  checked={form.emergencyOnly}
                  onChange={(e) =>
                    setForm((f) => ({ ...f, emergencyOnly: e.target.checked }))
                  }
                  style={{ marginRight: 8 }}
                />
                Emergency contact only
              </label>
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
            {isSubmitting ? "Saving…" : "Save Parent"}
          </button>
          <button className="btn btn--secondary" onClick={onClose}>
            Cancel
          </button>
        </div>
      </div>
    </div>
  );
}
