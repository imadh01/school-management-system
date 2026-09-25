import { useEffect, useState } from "react";
import type {
  StudentResponse,
  CreateStudentRequest,
} from "../types/student.types";
import type { ClassSectionResponse } from "@/features/class-sections/types/classSection.types";

interface Props {
  isOpen: boolean;
  editingStudent: StudentResponse | null;
  classSections: ClassSectionResponse[];
  onClose: () => void;
  onSubmit: (data: CreateStudentRequest, status?: string) => Promise<void>;
}

const DIETARY_OPTIONS = [
  "Halal",
  "Kosher",
  "Vegetarian",
  "Vegan",
  "Pescatarian",
  "Dairy-Free",
  "Gluten-Free",
  "Nut-Free",
];
const HOUSE_OPTIONS = ["Red", "Blue", "Green", "Yellow"];

const emptyForm: CreateStudentRequest = {
  admNo: "",
  rollNumber: "",
  classSectionId: 0,
  admissionDate: new Date().toISOString().slice(0, 10),
  photoUrl: "",
  firstName: "",
  middleName: "",
  lastName: "",
  gender: "Male",
  dateOfBirth: "",
  bloodGroup: "unknown",
  aadhaarNumber: "",
  mobile: "",
  email: "",
  addressLine: "",
  city: "",
  state: "",
  pincode: "",
  fatherName: "",
  fatherOccupation: "",
  fatherMobile: "",
  motherName: "",
  motherOccupation: "",
  motherMobile: "",
  guardianName: "",
  guardianRelation: "",
  guardianMobile: "",
  category: "General",
  religion: "",
  previousSchool: "",
  transportRequired: false,
  transportRoute: "",
  medicalNotes: "",
  nationality: "",
  secondNationality: "",
  countryOfBirth: "",
  preferredName: "",
  passportNumber: "",
  passportExpiry: null,
  visaType: "",
  visaExpiry: "",
  motherTongue: "",
  homeLanguage: "",
  englishProficiency: "Native",
  curriculumTrack: "British",
  admissionType: "Fresh Admission",
  custodyArrangement: "Joint",
  primaryContactParent: "Both Parents",
  authorizedPickupPersons: "",
  mediaConsent: true,
  dietaryRequirements: "",
  allergies: "",
  insuranceProvider: "",
  insurancePolicyExpiry: null,
  house: null,
  ealCode: "",
  feeConcessionPercent: 0,
  specialEducationalNeeds: "",
};

export function StudentModal({
  isOpen,
  editingStudent,
  classSections,
  onClose,
  onSubmit,
}: Props) {
  const [form, setForm] = useState<CreateStudentRequest>(emptyForm);
  const [status, setStatus] = useState("Active");
  const [isSubmitting, setIsSubmitting] = useState(false);
  const [error, setError] = useState<string | null>(null);

  useEffect(() => {
    if (editingStudent) {
      // StudentResponse only carries the class's display name, not its id —
      // resolve it against the loaded classSections list so the dropdown
      // shows the student's actual current class instead of resetting blank.
      const matchedClassId =
        classSections.find(
          (c) => c.displayName === editingStudent.classSectionName,
        )?.id ?? 0;
      setForm({
        admNo: editingStudent.admNo,
        rollNumber: editingStudent.rollNumber,
        classSectionId: matchedClassId,
        admissionDate: editingStudent.admissionDate,
        photoUrl: editingStudent.photoUrl,
        firstName: editingStudent.firstName,
        middleName: editingStudent.middleName,
        lastName: editingStudent.lastName,
        gender: editingStudent.gender,
        dateOfBirth: editingStudent.dateOfBirth,
        bloodGroup: editingStudent.bloodGroup,
        aadhaarNumber: editingStudent.aadhaarNumber,
        mobile: editingStudent.mobile,
        email: editingStudent.email,
        addressLine: editingStudent.addressLine,
        city: editingStudent.city,
        state: editingStudent.state,
        pincode: editingStudent.pincode,
        fatherName: editingStudent.fatherName,
        fatherOccupation: editingStudent.fatherOccupation,
        fatherMobile: editingStudent.fatherMobile,
        motherName: editingStudent.motherName,
        motherOccupation: editingStudent.motherOccupation,
        motherMobile: editingStudent.motherMobile,
        guardianName: editingStudent.guardianName,
        guardianRelation: editingStudent.guardianRelation,
        guardianMobile: editingStudent.guardianMobile,
        category: editingStudent.category,
        religion: editingStudent.religion,
        previousSchool: editingStudent.previousSchool,
        transportRequired: editingStudent.transportRequired,
        transportRoute: editingStudent.transportRoute,
        medicalNotes: editingStudent.medicalNotes,
        nationality: editingStudent.nationality,
        secondNationality: editingStudent.secondNationality,
        countryOfBirth: editingStudent.countryOfBirth,
        preferredName: editingStudent.preferredName,
        passportNumber: editingStudent.passportNumber,
        passportExpiry: editingStudent.passportExpiry,
        visaType: editingStudent.visaType,
        visaExpiry: editingStudent.visaExpiry,
        motherTongue: editingStudent.motherTongue,
        homeLanguage: editingStudent.homeLanguage,
        englishProficiency: editingStudent.englishProficiency,
        curriculumTrack: editingStudent.curriculumTrack,
        admissionType: editingStudent.admissionType,
        custodyArrangement: editingStudent.custodyArrangement,
        primaryContactParent: editingStudent.primaryContactParent,
        authorizedPickupPersons: editingStudent.authorizedPickupPersons,
        mediaConsent: editingStudent.mediaConsent,
        dietaryRequirements: editingStudent.dietaryRequirements,
        allergies: editingStudent.allergies,
        insuranceProvider: editingStudent.insuranceProvider,
        insurancePolicyExpiry: editingStudent.insurancePolicyExpiry,
        house: editingStudent.house,
        ealCode: editingStudent.ealCode,
        feeConcessionPercent: editingStudent.feeConcessionPercent,
        specialEducationalNeeds: editingStudent.specialEducationalNeeds,
      });
      setStatus(editingStudent.status);
    } else {
      setForm(emptyForm);
      setStatus("Active");
    }
    setError(null);
  }, [editingStudent, isOpen, classSections]);

  if (!isOpen) return null;

  const field = (key: keyof CreateStudentRequest, value: string) =>
    setForm((f) => ({ ...f, [key]: value === "" ? null : value }));

  const selectedDietary = (form.dietaryRequirements ?? "")
    .split(",")
    .map((s) => s.trim())
    .filter(Boolean);
  const toggleDietary = (tag: string) => {
    const next = selectedDietary.includes(tag)
      ? selectedDietary.filter((t) => t !== tag)
      : [...selectedDietary, tag];
    setForm((f) => ({ ...f, dietaryRequirements: next.join(", ") || null }));
  };

  const handleSubmit = async () => {
    setError(null);
    if (
      !form.admNo ||
      !form.rollNumber ||
      !form.classSectionId ||
      !form.firstName ||
      !form.lastName ||
      !form.dateOfBirth ||
      !form.addressLine ||
      !form.city ||
      !form.state ||
      !form.pincode
    ) {
      setError(
        "Adm #, class, roll number, first/last name, DOB, and full address are required.",
      );
      return;
    }
    setIsSubmitting(true);
    try {
      // Guard against any empty-string value reaching a nullable field —
      // rather than trust every individual input's state management,
      // normalize the whole payload once, right before it's sent.
      const sanitized = Object.fromEntries(
        Object.entries(form).map(([key, value]) => [
          key,
          value === "" ? null : value,
        ]),
      ) as CreateStudentRequest;

      await onSubmit(sanitized, status);
      onClose();
    } catch {
      setError(
        "Could not save this student. The Adm # or roll number may already be in use.",
      );
    } finally {
      setIsSubmitting(false);
    }
  };

  return (
    <div className="modal-backdrop">
      <div className="modal modal--wide">
        <div className="modal__header">
          <h2>🧑‍🎓 {editingStudent ? "Edit Student" : "Add Student"}</h2>
          <button className="modal__close" onClick={onClose}>
            ✕
          </button>
        </div>
        <div className="modal__body">
          <div className="modal__section-title">Identity &amp; Academic</div>
          <div className="modal__grid">
            <div className="field">
              <label>
                Admission Number<span className="required">*</span>
              </label>
              <input
                value={form.admNo}
                disabled={!!editingStudent}
                onChange={(e) =>
                  setForm((f) => ({ ...f, admNo: e.target.value }))
                }
                placeholder="2024-00000001"
              />
            </div>
            <div className="field">
              <label>
                Class<span className="required">*</span>
              </label>
              <select
                value={form.classSectionId || ""}
                onChange={(e) =>
                  setForm((f) => ({
                    ...f,
                    classSectionId: Number(e.target.value),
                  }))
                }
              >
                <option value="">Select class...</option>
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
                Admission Date<span className="required">*</span>
              </label>
              <input
                type="date"
                value={form.admissionDate}
                disabled={!!editingStudent}
                onChange={(e) =>
                  setForm((f) => ({ ...f, admissionDate: e.target.value }))
                }
              />
            </div>
            <div className="field">
              <label>Status</label>
              <select
                value={status}
                onChange={(e) => setStatus(e.target.value)}
              >
                <option>Active</option>
                <option>Inactive</option>
                <option>Transferred</option>
                <option>Passed Out</option>
                <option>Suspended</option>
              </select>
            </div>
            <div className="field">
              <label>Photo URL</label>
              <input
                value={form.photoUrl ?? ""}
                onChange={(e) => field("photoUrl", e.target.value)}
                placeholder="https://..."
              />
            </div>
          </div>

          <div className="modal__section-title">Personal</div>
          <div className="modal__grid">
            <div className="field">
              <label>
                First Name<span className="required">*</span>
              </label>
              <input
                value={form.firstName}
                onChange={(e) =>
                  setForm((f) => ({ ...f, firstName: e.target.value }))
                }
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
                onChange={(e) =>
                  setForm((f) => ({ ...f, lastName: e.target.value }))
                }
              />
            </div>
            <div className="field">
              <label>
                Gender<span className="required">*</span>
              </label>
              <select
                value={form.gender}
                onChange={(e) =>
                  setForm((f) => ({ ...f, gender: e.target.value }))
                }
              >
                <option>Male</option>
                <option>Female</option>
                <option>Other</option>
              </select>
            </div>
            <div className="field">
              <label>
                Date of Birth<span className="required">*</span>
              </label>
              <input
                type="date"
                value={form.dateOfBirth}
                onChange={(e) =>
                  setForm((f) => ({ ...f, dateOfBirth: e.target.value }))
                }
              />
            </div>
            <div className="field">
              <label>Blood Group</label>
              <select
                value={form.bloodGroup ?? "unknown"}
                onChange={(e) => field("bloodGroup", e.target.value)}
              >
                <option>unknown</option>
                <option>A+</option>
                <option>A-</option>
                <option>B+</option>
                <option>B-</option>
                <option>O+</option>
                <option>O-</option>
                <option>AB+</option>
                <option>AB-</option>
              </select>
            </div>
            <div className="field" style={{ gridColumn: "span 2" }}>
              <label>Aadhaar Number</label>
              <input
                value={form.aadhaarNumber ?? ""}
                onChange={(e) => field("aadhaarNumber", e.target.value)}
                placeholder="12 digits"
              />
            </div>
          </div>

          <div className="modal__section-title">Contact &amp; Address</div>
          <div className="modal__grid">
            <div className="field">
              <label>Mobile</label>
              <input
                value={form.mobile ?? ""}
                onChange={(e) => field("mobile", e.target.value)}
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
            <div className="field" style={{ gridColumn: "span 2" }}>
              <label>
                Address Line<span className="required">*</span>
              </label>
              <input
                value={form.addressLine ?? ""}
                onChange={(e) => field("addressLine", e.target.value)}
              />
            </div>
            <div className="field">
              <label>
                City<span className="required">*</span>
              </label>
              <input
                value={form.city ?? ""}
                onChange={(e) => field("city", e.target.value)}
              />
            </div>
            <div className="field">
              <label>
                State<span className="required">*</span>
              </label>
              <input
                value={form.state ?? ""}
                onChange={(e) => field("state", e.target.value)}
              />
            </div>
            <div className="field" style={{ gridColumn: "span 2" }}>
              <label>
                PIN Code<span className="required">*</span>
              </label>
              <input
                value={form.pincode ?? ""}
                onChange={(e) => field("pincode", e.target.value)}
              />
            </div>
          </div>

          <div className="modal__section-title">Family</div>
          <div className="modal__grid">
            <div className="field">
              <label>Father Name</label>
              <input
                value={form.fatherName ?? ""}
                onChange={(e) => field("fatherName", e.target.value)}
              />
            </div>
            <div className="field">
              <label>Father Occupation</label>
              <input
                value={form.fatherOccupation ?? ""}
                onChange={(e) => field("fatherOccupation", e.target.value)}
              />
            </div>
            <div className="field">
              <label>Father Mobile</label>
              <input
                value={form.fatherMobile ?? ""}
                onChange={(e) => field("fatherMobile", e.target.value)}
              />
            </div>
            <div className="field">
              <label>Mother Name</label>
              <input
                value={form.motherName ?? ""}
                onChange={(e) => field("motherName", e.target.value)}
              />
            </div>
            <div className="field">
              <label>Mother Occupation</label>
              <input
                value={form.motherOccupation ?? ""}
                onChange={(e) => field("motherOccupation", e.target.value)}
              />
            </div>
            <div className="field">
              <label>Mother Mobile</label>
              <input
                value={form.motherMobile ?? ""}
                onChange={(e) => field("motherMobile", e.target.value)}
              />
            </div>
            <div className="field">
              <label>Guardian Name</label>
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
                placeholder="e.g. Uncle"
              />
            </div>
            <div className="field">
              <label>Guardian Mobile</label>
              <input
                value={form.guardianMobile ?? ""}
                onChange={(e) => field("guardianMobile", e.target.value)}
              />
            </div>
          </div>

          <div className="modal__section-title">Other Info</div>
          <div className="modal__grid">
            <div className="field">
              <label>
                Category<span className="required">*</span>
              </label>
              <select
                value={form.category}
                onChange={(e) =>
                  setForm((f) => ({ ...f, category: e.target.value }))
                }
              >
                <option>General</option>
                <option>OBC</option>
                <option>SC</option>
                <option>ST</option>
              </select>
            </div>
            <div className="field">
              <label>Religion</label>
              <input
                value={form.religion ?? ""}
                onChange={(e) => field("religion", e.target.value)}
              />
            </div>
            <div className="field" style={{ gridColumn: "span 2" }}>
              <label>Previous School</label>
              <input
                value={form.previousSchool ?? ""}
                onChange={(e) => field("previousSchool", e.target.value)}
              />
            </div>
            <div className="field">
              <label>Transport Required</label>
              <div className="radio-row">
                <label>
                  <input
                    type="radio"
                    checked={form.transportRequired}
                    onChange={() =>
                      setForm((f) => ({ ...f, transportRequired: true }))
                    }
                  />{" "}
                  Yes
                </label>
                <label>
                  <input
                    type="radio"
                    checked={!form.transportRequired}
                    onChange={() =>
                      setForm((f) => ({
                        ...f,
                        transportRequired: false,
                        transportRoute: "",
                      }))
                    }
                  />{" "}
                  No
                </label>
              </div>
            </div>
            <div className="field">
              <label>Transport Route</label>
              <input
                value={form.transportRoute ?? ""}
                disabled={!form.transportRequired}
                onChange={(e) => field("transportRoute", e.target.value)}
                placeholder="(transport off)"
              />
            </div>
            <div className="field" style={{ gridColumn: "span 2" }}>
              <label>Medical Notes</label>
              <textarea
                rows={2}
                value={form.medicalNotes ?? ""}
                onChange={(e) => field("medicalNotes", e.target.value)}
                placeholder="Allergies, conditions, etc."
              />
            </div>
          </div>

          <div className="modal__section-title">Identity &amp; Citizenship</div>
          <div className="modal__grid">
            <div className="field">
              <label>Nationality</label>
              <input
                value={form.nationality ?? ""}
                onChange={(e) => field("nationality", e.target.value)}
              />
            </div>
            <div className="field">
              <label>Second Nationality</label>
              <input
                value={form.secondNationality ?? ""}
                onChange={(e) => field("secondNationality", e.target.value)}
                placeholder="Optional"
              />
            </div>
            <div className="field">
              <label>Country of Birth</label>
              <input
                value={form.countryOfBirth ?? ""}
                onChange={(e) => field("countryOfBirth", e.target.value)}
              />
            </div>
            <div className="field">
              <label>Preferred Name</label>
              <input
                value={form.preferredName ?? ""}
                onChange={(e) => field("preferredName", e.target.value)}
                placeholder="What students like to be called"
              />
            </div>
            <div className="field">
              <label>Passport Number</label>
              <input
                value={form.passportNumber ?? ""}
                onChange={(e) => field("passportNumber", e.target.value)}
              />
            </div>
            <div className="field">
              <label>Passport Expiry</label>
              <input
                type="date"
                value={form.passportExpiry ?? ""}
                onChange={(e) => field("passportExpiry", e.target.value)}
              />
            </div>
            <div className="field">
              <label>Visa Type</label>
              <input
                value={form.visaType ?? ""}
                onChange={(e) => field("visaType", e.target.value)}
                placeholder="e.g. Dependent, Student, Tourist"
              />
            </div>
            <div className="field">
              <label>Visa Expiry</label>
              <input
                type="date"
                value={form.visaExpiry ?? ""}
                onChange={(e) => field("visaExpiry", e.target.value)}
              />
            </div>
          </div>

          <div className="modal__section-title">Languages &amp; Curriculum</div>
          <div className="modal__grid">
            <div className="field">
              <label>Mother Tongue</label>
              <input
                value={form.motherTongue ?? ""}
                onChange={(e) => field("motherTongue", e.target.value)}
                placeholder="e.g. English, Mandarin, Hindi"
              />
            </div>
            <div className="field">
              <label>Home Language</label>
              <input
                value={form.homeLanguage ?? ""}
                onChange={(e) => field("homeLanguage", e.target.value)}
              />
            </div>
            <div className="field">
              <label>English Proficiency</label>
              <select
                value={form.englishProficiency ?? "Native"}
                onChange={(e) => field("englishProficiency", e.target.value)}
              >
                <option>Native</option>
                <option>C2 Proficient</option>
                <option>C1 Advanced</option>
                <option>B2 Upper Intermediate</option>
                <option>B1 Intermediate</option>
                <option>A2 Elementary</option>
              </select>
            </div>
            <div className="field">
              <label>Curriculum Track</label>
              <select
                value={form.curriculumTrack ?? "British"}
                onChange={(e) => field("curriculumTrack", e.target.value)}
              >
                <option>British</option>
                <option>American</option>
                <option>IB</option>
                <option>National</option>
              </select>
            </div>
            <div className="field">
              <label>Admission Type</label>
              <select
                value={form.admissionType}
                onChange={(e) =>
                  setForm((f) => ({ ...f, admissionType: e.target.value }))
                }
              >
                <option>Fresh Admission</option>
                <option>Transfer</option>
              </select>
            </div>
          </div>

          <div className="modal__section-title">Safeguarding &amp; Pickup</div>
          <div className="modal__grid">
            <div className="field">
              <label>Custody Arrangement</label>
              <select
                value={form.custodyArrangement ?? "Joint"}
                onChange={(e) => field("custodyArrangement", e.target.value)}
              >
                <option>Joint</option>
                <option>Mother</option>
                <option>Father</option>
                <option>Guardian</option>
              </select>
            </div>
            <div className="field">
              <label>Primary Contact Parent</label>
              <select
                value={form.primaryContactParent ?? "Both Parents"}
                onChange={(e) => field("primaryContactParent", e.target.value)}
              >
                <option>Both Parents</option>
                <option>Father</option>
                <option>Mother</option>
                <option>Guardian</option>
              </select>
            </div>
            <div className="field" style={{ gridColumn: "span 2" }}>
              <label>Authorized Pickup Persons</label>
              <textarea
                rows={2}
                value={form.authorizedPickupPersons ?? ""}
                onChange={(e) =>
                  field("authorizedPickupPersons", e.target.value)
                }
                placeholder="Comma-list of authorized adults — grandparents, drivers, etc."
              />
            </div>
          </div>
          <div
            className="checkbox-row"
            style={{
              display: "flex",
              alignItems: "center",
              gap: 8,
              marginTop: 8,
            }}
          >
            <input
              type="checkbox"
              id="media-consent"
              checked={form.mediaConsent}
              onChange={(e) =>
                setForm((f) => ({ ...f, mediaConsent: e.target.checked }))
              }
            />
            <label htmlFor="media-consent">
              Media Consent — allow photos/videos of student in school yearbook
              + website
            </label>
          </div>

          <div className="modal__section-title">Health &amp; Safety</div>
          <div className="field" style={{ marginBottom: 14 }}>
            <label>Dietary Requirements</label>
            <div className="tag-group">
              {DIETARY_OPTIONS.map((tag) => (
                <button
                  key={tag}
                  type="button"
                  className={`tag-btn ${selectedDietary.includes(tag) ? "selected" : ""}`}
                  onClick={() => toggleDietary(tag)}
                >
                  {tag}
                </button>
              ))}
            </div>
            <div style={{ marginTop: 8 }}>
              <button
                type="button"
                className={`tag-btn tag-btn--restrictions ${selectedDietary.length === 0 ? "selected" : ""}`}
                onClick={() =>
                  setForm((f) => ({ ...f, dietaryRequirements: null }))
                }
              >
                ✓ No Restrictions
              </button>
            </div>
          </div>
          <div className="modal__grid">
            <div className="field" style={{ gridColumn: "span 2" }}>
              <label>Allergies</label>
              <textarea
                rows={2}
                value={form.allergies ?? ""}
                onChange={(e) => field("allergies", e.target.value)}
                placeholder="Critical: peanut, treenut, dairy, penicillin, etc. — staff sees this everywhere"
              />
            </div>
            <div className="field">
              <label>Insurance Provider</label>
              <input
                value={form.insuranceProvider ?? ""}
                onChange={(e) => field("insuranceProvider", e.target.value)}
                placeholder="e.g. AXA International"
              />
            </div>
            <div className="field">
              <label>Insurance Policy Expiry</label>
              <input
                type="date"
                value={form.insurancePolicyExpiry ?? ""}
                onChange={(e) => field("insurancePolicyExpiry", e.target.value)}
              />
            </div>
          </div>

          <div className="modal__section-title">House &amp; Identity</div>
          <div className="tag-group" style={{ marginBottom: 16 }}>
            {HOUSE_OPTIONS.map((h) => (
              <button
                key={h}
                type="button"
                className={`tag-btn house-${h} ${form.house === h ? "selected" : ""}`}
                onClick={() =>
                  setForm((f) => ({ ...f, house: f.house === h ? null : h }))
                }
              >
                🏠 {h}
              </button>
            ))}
          </div>

          <div className="modal__section-title">Welfare / Finance</div>
          <div className="modal__grid">
            <div className="field">
              <label>Fee Concession (%)</label>
              <input
                type="number"
                value={form.feeConcessionPercent ?? 0}
                onChange={(e) =>
                  setForm((f) => ({
                    ...f,
                    feeConcessionPercent:
                      e.target.value === "" ? null : Number(e.target.value),
                  }))
                }
              />
              <div className="field-note">
                Scholarship / RTE / sibling discount — flows into fee billing
              </div>
            </div>
            <div className="field">
              <label>Special Educational Needs (SEN / IEP)</label>
              <textarea
                rows={2}
                value={form.specialEducationalNeeds ?? ""}
                onChange={(e) =>
                  field("specialEducationalNeeds", e.target.value)
                }
                placeholder="e.g. IEP - Dyslexia, extra time on exams"
              />
              <div className="field-note">
                Visible to teaching staff for classroom accommodations
              </div>
            </div>
          </div>

          <div className="modal__note">
            <strong>Note:</strong> Portal login credentials aren't set here —
            student portal access is provisioned as a separate, deliberate step,
            not part of adding a student record.
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
            {isSubmitting ? "Saving…" : "Save"}
          </button>
          <button className="btn btn--secondary" onClick={onClose}>
            Cancel
          </button>
        </div>
      </div>
    </div>
  );
}
