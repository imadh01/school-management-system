import { useEffect, useState } from "react";
import type {
  CreateStudentRequest,
  PickupPersonRequest,
  StudentHealthDto,
  StudentResponse,
  StudentStatus,
  UpdateStudentIdentityRequest,
} from "../types/student.types";
import type { ClassSectionResponse } from "@/features/class-sections/types/classSection.types";
import { ValidationModal } from "@/components/ValidationModal";
import { getApiErrors } from "@/utils/apiError";

interface Props {
  isOpen: boolean;
  /** Full detail record (from GET /students/{id}); null when adding. */
  editingStudent: StudentResponse | null;
  classSections: ClassSectionResponse[];
  onClose: () => void;
  onSubmit: (
    data: CreateStudentRequest,
    status: StudentStatus,
    identity: UpdateStudentIdentityRequest | null,
  ) => Promise<void>;
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
const BLOOD_GROUPS = ["A+", "A-", "B+", "B-", "AB+", "AB-", "O+", "O-"];
const RELIGIONS = [
  "Hindu",
  "Muslim",
  "Christian",
  "Sikh",
  "Buddhist",
  "Jain",
  "Other",
];
const CATEGORIES = ["General", "OBC", "SC", "ST", "EWS"];
const MAX_PICKUP_PERSONS = 10;

const emptyHealth: StudentHealthDto = {
  bloodGroup: null,
  allergies: null,
  dietaryRequirements: null,
  medicalNotes: null,
  specialEducationalNeeds: null,
  insuranceProvider: null,
  insurancePolicyExpiry: null,
};

const emptyIdentity: UpdateStudentIdentityRequest = {
  aadhaarNumber: null,
  passportNumber: null,
  passportExpiry: null,
  visaType: null,
  visaExpiry: null,
};

const emptyForm: CreateStudentRequest = {
  admNo: "",
  rollNumber: "",
  classSectionId: 0,
  admissionDate: new Date().toISOString().slice(0, 10),
  photoUrl: null,
  firstName: "",
  middleName: null,
  lastName: "",
  gender: "Male",
  dateOfBirth: "",
  mobile: null,
  email: null,
  addressLine: null,
  city: null,
  state: null,
  pincode: null,
  category: "General",
  religion: null,
  previousSchool: null,
  transportRequired: false,
  transportRoute: null,
  nationality: null,
  secondNationality: null,
  countryOfBirth: null,
  preferredName: null,
  motherTongue: null,
  homeLanguage: null,
  englishProficiency: "Native",
  curriculumTrack: "British",
  admissionType: "Fresh Admission",
  custodyArrangement: "Joint",
  mediaConsent: true,
  house: null,
  ealCode: null,
  feeConcessionPercent: 0,
  health: null,
  pickupPersons: null,
};

/** Turns every blank string into null so optional API fields stay clean. */
function blankToNull<T extends object>(obj: T): T {
  return Object.fromEntries(
    Object.entries(obj).map(([k, v]) => [
      k,
      typeof v === "string" && v.trim() === "" ? null : v,
    ]),
  ) as T;
}

export function StudentModal({
  isOpen,
  editingStudent,
  classSections,
  onClose,
  onSubmit,
}: Props) {
  const [form, setForm] = useState<CreateStudentRequest>(emptyForm);
  const [health, setHealth] = useState<StudentHealthDto>(emptyHealth);
  const [pickups, setPickups] = useState<PickupPersonRequest[]>([]);
  const [identity, setIdentity] =
    useState<UpdateStudentIdentityRequest>(emptyIdentity);
  const [status, setStatus] = useState<StudentStatus>("Active");
  const [isSubmitting, setIsSubmitting] = useState(false);
  const [errors, setErrors] = useState<string[] | null>(null);

  const identityHidden = editingStudent?.identity.isMasked ?? true;

  useEffect(() => {
    if (editingStudent) {
      const s = editingStudent;
      setForm({
        admNo: s.admNo,
        rollNumber: s.rollNumber,
        classSectionId: s.classSectionId,
        admissionDate: s.admissionDate,
        photoUrl: s.photoUrl,
        firstName: s.firstName,
        middleName: s.middleName,
        lastName: s.lastName,
        gender: s.gender,
        dateOfBirth: s.dateOfBirth,
        mobile: s.mobile,
        email: s.email,
        addressLine: s.addressLine,
        city: s.city,
        state: s.state,
        pincode: s.pincode,
        category: s.category,
        religion: s.religion,
        previousSchool: s.previousSchool,
        transportRequired: s.transportRequired,
        transportRoute: s.transportRoute,
        nationality: s.nationality,
        secondNationality: s.secondNationality,
        countryOfBirth: s.countryOfBirth,
        preferredName: s.preferredName,
        motherTongue: s.motherTongue,
        homeLanguage: s.homeLanguage,
        englishProficiency: s.englishProficiency,
        curriculumTrack: s.curriculumTrack,
        admissionType: s.admissionType,
        custodyArrangement: s.custodyArrangement,
        mediaConsent: s.mediaConsent,
        house: s.house,
        ealCode: s.ealCode,
        feeConcessionPercent: s.feeConcessionPercent,
        health: null,
        pickupPersons: null,
      });
      setHealth(s.health);
      setPickups(
        s.pickupPersons.map((p) => ({
          name: p.name,
          relation: p.relation,
          phone: p.phone,
          idNote: p.idNote,
        })),
      );
      setIdentity({
        aadhaarNumber: s.identity.aadhaarNumber,
        passportNumber: s.identity.passportNumber,
        passportExpiry: s.identity.passportExpiry,
        visaType: s.identity.visaType,
        visaExpiry: s.identity.visaExpiry,
      });
      setStatus(s.status);
    } else {
      setForm({
        ...emptyForm,
        admissionDate: new Date().toISOString().slice(0, 10),
      });
      setHealth(emptyHealth);
      setPickups([]);
      setIdentity(emptyIdentity);
      setStatus("Active");
    }
    setErrors(null);
  }, [editingStudent, isOpen]);

  if (!isOpen) return null;

  const field = (key: keyof CreateStudentRequest, value: string) =>
    setForm((f) => ({ ...f, [key]: value === "" ? null : value }));
  const healthField = (key: keyof StudentHealthDto, value: string) =>
    setHealth((h) => ({ ...h, [key]: value === "" ? null : value }));
  const identityField = (
    key: keyof UpdateStudentIdentityRequest,
    value: string,
  ) => setIdentity((i) => ({ ...i, [key]: value === "" ? null : value }));

  const selectedDietary = (health.dietaryRequirements ?? "")
    .split(",")
    .map((s) => s.trim())
    .filter(Boolean);
  const toggleDietary = (tag: string) => {
    const next = selectedDietary.includes(tag)
      ? selectedDietary.filter((t) => t !== tag)
      : [...selectedDietary, tag];
    setHealth((h) => ({ ...h, dietaryRequirements: next.join(", ") || null }));
  };

  const updatePickup = (i: number, patch: Partial<PickupPersonRequest>) =>
    setPickups((list) =>
      list.map((p, idx) => (idx === i ? { ...p, ...patch } : p)),
    );

  const handleSubmit = async () => {
    const problems: string[] = [];
    if (!form.admNo) problems.push("Admission number is required.");
    if (!form.rollNumber) problems.push("Roll number is required.");
    if (!form.classSectionId) problems.push("Class is required.");
    if (!form.firstName) problems.push("First name is required.");
    if (!form.lastName) problems.push("Last name is required.");
    if (!form.dateOfBirth) problems.push("Date of birth is required.");

    // A completely blank pickup row is ignored; a partly filled one is an error.
    const pickupRows = pickups.filter(
      (p) => p.name.trim() || p.relation.trim() || p.phone.trim(),
    );
    pickupRows.forEach((p, i) => {
      if (!p.name.trim() || !p.relation.trim() || !p.phone.trim())
        problems.push(
          `Pickup person ${i + 1}: name, relation and phone are all required.`,
        );
    });
    if (problems.length > 0) {
      setErrors(problems);
      return;
    }

    setIsSubmitting(true);
    try {
      const payload: CreateStudentRequest = {
        ...blankToNull(form),
        health: blankToNull(health),
        // On edit this replaces the list; on create it seeds it.
        pickupPersons: pickupRows.map((p) => blankToNull(p)),
      };
      const identityPayload =
        editingStudent && !identityHidden ? blankToNull(identity) : null;

      await onSubmit(payload, status, identityPayload);
      onClose();
    } catch (err) {
      setErrors(
        getApiErrors(
          err,
          "Could not save this student. The Adm # or roll number may already be in use.",
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
                {classSections
                  .filter(
                    (c) =>
                      c.status === "Active" || c.id === form.classSectionId,
                  )
                  .map((c) => (
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
                onChange={(e) => setStatus(e.target.value as StudentStatus)}
              >
                <option value="Active">Active</option>
                <option value="Inactive">Inactive</option>
                <option value="Left">Left</option>
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
              <label>Preferred Name</label>
              <input
                value={form.preferredName ?? ""}
                onChange={(e) => field("preferredName", e.target.value)}
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
                value={health.bloodGroup ?? ""}
                onChange={(e) => healthField("bloodGroup", e.target.value)}
              >
                <option value="">Unknown</option>
                {BLOOD_GROUPS.map((b) => (
                  <option key={b} value={b}>
                    {b}
                  </option>
                ))}
              </select>
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

          <div className="modal__section-title">Parents / Guardians</div>
          {editingStudent ? (
            <>
              {editingStudent.guardians.length === 0 ? (
                <div className="empty-state" style={{ padding: 12 }}>
                  No parents linked yet.
                </div>
              ) : (
                editingStudent.guardians.map((g) => (
                  <div key={g.parentId} style={{ marginBottom: 6 }}>
                    <strong>{g.parentName}</strong>{" "}
                    <span className="badge badge--info">{g.relationType}</span>
                    {g.isPrimaryContact && (
                      <span className="badge badge--warn"> ★ Primary</span>
                    )}
                    <span style={{ color: "var(--muted)", fontSize: 12.5 }}>
                      {" "}
                      · {g.parentMobile}
                    </span>
                  </div>
                ))
              )}
              <div className="modal__note">
                Parent details are edited in the Parents module. Links are
                managed there too.
              </div>
            </>
          ) : (
            <div className="modal__note" style={{ marginTop: 0 }}>
              Parents are linked after the student is created, from the Parents
              module. Students admitted through Admissions get their parents
              linked automatically at enrolment.
            </div>
          )}

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
                {CATEGORIES.map((c) => (
                  <option key={c}>{c}</option>
                ))}
              </select>
            </div>
            <div className="field">
              <label>Religion</label>
              <select
                value={form.religion ?? ""}
                onChange={(e) => field("religion", e.target.value)}
              >
                <option value="">Not specified</option>
                {RELIGIONS.map((r) => (
                  <option key={r} value={r}>
                    {r}
                  </option>
                ))}
              </select>
            </div>
            <div className="field">
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
                        transportRoute: null,
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
              />
            </div>
            <div className="field">
              <label>Medical Notes</label>
              <textarea
                rows={2}
                value={health.medicalNotes ?? ""}
                onChange={(e) => healthField("medicalNotes", e.target.value)}
              />
            </div>
          </div>

          <div className="modal__section-title">Citizenship</div>
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
              />
            </div>
            <div className="field">
              <label>Country of Birth</label>
              <input
                value={form.countryOfBirth ?? ""}
                onChange={(e) => field("countryOfBirth", e.target.value)}
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
                value={form.englishProficiency ?? ""}
                onChange={(e) => field("englishProficiency", e.target.value)}
              >
                <option value="">Not specified</option>
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
                value={form.curriculumTrack ?? ""}
                onChange={(e) => field("curriculumTrack", e.target.value)}
              >
                <option value="">Not specified</option>
                <option>British</option>
                <option>American</option>
                <option>IB</option>
                <option>National</option>
              </select>
            </div>
            <div className="field">
              <label>EAL Code</label>
              <input
                value={form.ealCode ?? ""}
                onChange={(e) => field("ealCode", e.target.value)}
              />
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
                value={form.custodyArrangement ?? ""}
                onChange={(e) => field("custodyArrangement", e.target.value)}
              >
                <option value="">Not specified</option>
                <option>Joint</option>
                <option>Mother</option>
                <option>Father</option>
                <option>Guardian</option>
              </select>
            </div>
          </div>
          <div className="field" style={{ marginTop: 10 }}>
            <label>Authorized Pickup Persons</label>
          </div>
          {pickups.length === 0 && (
            <div className="empty-state" style={{ padding: 10 }}>
              No pickup persons added.
            </div>
          )}
          {pickups.map((p, i) => (
            <div
              key={i}
              className="modal__grid"
              style={{ alignItems: "end", marginBottom: 8 }}
            >
              <div className="field">
                <label>Name</label>
                <input
                  value={p.name}
                  onChange={(e) => updatePickup(i, { name: e.target.value })}
                />
              </div>
              <div className="field">
                <label>Relation</label>
                <input
                  value={p.relation}
                  onChange={(e) =>
                    updatePickup(i, { relation: e.target.value })
                  }
                  placeholder="e.g. Grandfather, Driver"
                />
              </div>
              <div className="field">
                <label>Phone</label>
                <input
                  value={p.phone}
                  onChange={(e) => updatePickup(i, { phone: e.target.value })}
                />
              </div>
              <div className="field">
                <label>ID note</label>
                <input
                  value={p.idNote ?? ""}
                  onChange={(e) =>
                    updatePickup(i, { idNote: e.target.value || null })
                  }
                  placeholder="Optional"
                />
              </div>
              <div className="field">
                <button
                  type="button"
                  className="btn btn--secondary btn--sm"
                  onClick={() =>
                    setPickups((list) => list.filter((_, idx) => idx !== i))
                  }
                >
                  Remove
                </button>
              </div>
            </div>
          ))}
          <button
            type="button"
            className="btn btn--secondary btn--sm"
            disabled={pickups.length >= MAX_PICKUP_PERSONS}
            onClick={() =>
              setPickups((list) => [
                ...list,
                { name: "", relation: "", phone: "", idNote: null },
              ])
            }
          >
            + Add pickup person
          </button>
          <div
            className="checkbox-row"
            style={{
              display: "flex",
              alignItems: "center",
              gap: 8,
              marginTop: 12,
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
                  setHealth((h) => ({ ...h, dietaryRequirements: null }))
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
                value={health.allergies ?? ""}
                onChange={(e) => healthField("allergies", e.target.value)}
                placeholder="Critical: peanut, treenut, dairy, penicillin, etc. — staff sees this everywhere"
              />
            </div>
            <div className="field">
              <label>Insurance Provider</label>
              <input
                value={health.insuranceProvider ?? ""}
                onChange={(e) =>
                  healthField("insuranceProvider", e.target.value)
                }
              />
            </div>
            <div className="field">
              <label>Insurance Policy Expiry</label>
              <input
                type="date"
                value={health.insurancePolicyExpiry ?? ""}
                onChange={(e) =>
                  healthField("insurancePolicyExpiry", e.target.value)
                }
              />
            </div>
          </div>

          <div className="modal__section-title">House</div>
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
                value={health.specialEducationalNeeds ?? ""}
                onChange={(e) =>
                  healthField("specialEducationalNeeds", e.target.value)
                }
                placeholder="e.g. IEP - Dyslexia, extra time on exams"
              />
              <div className="field-note">
                Visible to teaching staff for classroom accommodations
              </div>
            </div>
          </div>

          <div className="modal__section-title">
            Identity Documents (Aadhaar / Passport / Visa)
          </div>
          {!editingStudent ? (
            <div className="modal__note" style={{ marginTop: 0 }}>
              Identity numbers can be added by authorised staff after the
              student is created (Edit student).
            </div>
          ) : identityHidden ? (
            <>
              <div className="modal__note" style={{ marginTop: 0 }}>
                Numbers are hidden for your role. Only staff with sensitive-data
                permission can view or change them.
              </div>
              <div className="modal__grid">
                <div className="field">
                  <label>Aadhaar</label>
                  <input value={identity.aadhaarNumber ?? "—"} disabled />
                </div>
                <div className="field">
                  <label>Passport Number</label>
                  <input value={identity.passportNumber ?? "—"} disabled />
                </div>
              </div>
            </>
          ) : (
            <div className="modal__grid">
              <div className="field">
                <label>Aadhaar Number</label>
                <input
                  value={identity.aadhaarNumber ?? ""}
                  onChange={(e) =>
                    identityField("aadhaarNumber", e.target.value)
                  }
                  placeholder="12 digits"
                />
              </div>
              <div className="field">
                <label>Passport Number</label>
                <input
                  value={identity.passportNumber ?? ""}
                  onChange={(e) =>
                    identityField("passportNumber", e.target.value)
                  }
                />
              </div>
              <div className="field">
                <label>Passport Expiry</label>
                <input
                  type="date"
                  value={identity.passportExpiry ?? ""}
                  onChange={(e) =>
                    identityField("passportExpiry", e.target.value)
                  }
                />
              </div>
              <div className="field">
                <label>Visa Type</label>
                <input
                  value={identity.visaType ?? ""}
                  onChange={(e) => identityField("visaType", e.target.value)}
                />
              </div>
              <div className="field">
                <label>Visa Expiry</label>
                <input
                  type="date"
                  value={identity.visaExpiry ?? ""}
                  onChange={(e) => identityField("visaExpiry", e.target.value)}
                />
              </div>
            </div>
          )}

          <div className="modal__note">
            <strong>Note:</strong> Portal login credentials aren't set here —
            student portal access is provisioned as a separate, deliberate step,
            not part of adding a student record.
          </div>
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
      <ValidationModal errors={errors} onClose={() => setErrors(null)} />
    </div>
  );
}
