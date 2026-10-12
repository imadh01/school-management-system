import { useCallback, useEffect, useMemo, useState } from "react";
import { Link, useNavigate, useParams } from "react-router-dom";
import { Info, Lock, Plus, Trash, UserX } from "lucide-react";
import { studentService } from "../services/studentService";
import { classSectionService } from "@/features/class-sections/services/classSectionService";
import {
  isFullStudent,
  type PickupPersonRequest,
  type StudentHealthDto,
  type StudentResponse,
  type StudentStatus,
  type UpdateStudentIdentityRequest,
  type UpdateStudentRequest,
} from "../types/student.types";
import type { ClassSectionResponse } from "@/features/class-sections/types/classSection.types";
import { FormPage } from "@/components/form/FormPage";
import { FormSection } from "@/components/form/FormSection";
import { Field } from "@/components/form/Field";
import { UnsavedChangesGuard } from "@/components/form/UnsavedChangesGuard";
import { EmptyState } from "@/components/EmptyState";
import { Badge } from "@/components/Badge";
import { useToast } from "@/components/Toast";
import {
  getReadErrors,
  getSaveErrors,
  isConcurrencyConflict,
} from "@/utils/apiError";
import { STUDENT_FORM_SECTIONS, sectionOfField } from "./studentFormSections";

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
const ENGLISH_LEVELS = [
  "Native",
  "C2 Proficient",
  "C1 Advanced",
  "B2 Upper Intermediate",
  "B1 Intermediate",
  "A2 Elementary",
];
const CURRICULA = ["British", "American", "IB", "National"];
const CUSTODY = ["Joint", "Mother", "Father", "Guardian"];
const MAX_PICKUP_PERSONS = 10;

/** The editable part of a student (status, health, pickup persons and identity are kept apart). */
type EditForm = Omit<
  UpdateStudentRequest,
  "rowVersion" | "health" | "pickupPersons" | "status"
>;
type TextKey = {
  [K in keyof EditForm]: EditForm[K] extends string | null ? K : never;
}[keyof EditForm];
type HealthKey = keyof StudentHealthDto;
type IdentityKey = keyof UpdateStudentIdentityRequest;

interface FormState {
  form: EditForm;
  status: StudentStatus;
  health: StudentHealthDto;
  pickups: PickupPersonRequest[];
  identity: UpdateStudentIdentityRequest;
}

function stateFrom(s: StudentResponse): FormState {
  return {
    form: {
      rollNumber: s.rollNumber,
      classSectionId: s.classSectionId,
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
    },
    status: s.status,
    health: { ...s.health },
    pickups: s.pickupPersons.map((p) => ({
      name: p.name,
      relation: p.relation,
      phone: p.phone,
      idNote: p.idNote,
    })),
    identity: {
      aadhaarNumber: s.identity.aadhaarNumber,
      passportNumber: s.identity.passportNumber,
      passportExpiry: s.identity.passportExpiry,
      visaType: s.identity.visaType,
      visaExpiry: s.identity.visaExpiry,
    },
  };
}

/** Blank strings become null, so optional API fields stay clean. */
function blankToNull<T extends object>(obj: T): T {
  return Object.fromEntries(
    Object.entries(obj).map(([k, v]) => [
      k,
      typeof v === "string" && v.trim() === "" ? null : v,
    ]),
  ) as T;
}

const isBlankPickup = (p: PickupPersonRequest) =>
  !p.name.trim() && !p.relation.trim() && !p.phone.trim() && !p.idNote?.trim();

/**
 * U2: editing a student on a full page (/students/:id/edit), replacing the old dialog.
 * Same fields and the same section order; errors appear under each field and the section list
 * counts them; leaving with unsaved changes asks first.
 *
 * There is no "new student" page: students are created by enrolling an admission.
 */
export function StudentEditPage() {
  const { id } = useParams();
  const studentId = Number(id);
  const navigate = useNavigate();
  const toast = useToast();

  const [student, setStudent] = useState<StudentResponse | null>(null);
  const [readOnly, setReadOnly] = useState(false);
  const [loadError, setLoadError] = useState<string | null>(null);
  const [classSections, setClassSections] = useState<ClassSectionResponse[]>(
    [],
  );

  const [state, setState] = useState<FormState | null>(null);
  const [initial, setInitial] = useState<string>("");
  const [fieldErrors, setFieldErrors] = useState<Record<string, string>>({});
  const [formErrors, setFormErrors] = useState<string[]>([]);
  const [conflict, setConflict] = useState(false);
  const [isSaving, setIsSaving] = useState(false);
  const [leaveTo, setLeaveTo] = useState<string | null>(null);

  const load = useCallback(async () => {
    try {
      const [s, sections] = await Promise.all([
        studentService.getById(studentId),
        classSectionService.getAll(),
      ]);
      setClassSections(sections);
      if (!isFullStudent(s)) {
        setReadOnly(true);
        return;
      }
      const next = stateFrom(s);
      setStudent(s);
      setState(next);
      setInitial(JSON.stringify(next));
      setFieldErrors({});
      setFormErrors([]);
      setConflict(false);
    } catch (err) {
      setLoadError(getReadErrors(err, "Could not load this student.")[0]);
    }
  }, [studentId]);

  useEffect(() => {
    void load();
  }, [load]);

  // Navigate only after the saved state has rendered, so the unsaved-changes guard lets us go.
  useEffect(() => {
    if (leaveTo) navigate(leaveTo);
  }, [leaveTo, navigate]);

  const isDirty = state !== null && JSON.stringify(state) !== initial;

  const sectionLinks = useMemo(() => {
    const counts: Record<string, number> = {};
    for (const key of Object.keys(fieldErrors)) {
      const section = sectionOfField(key);
      if (section) counts[section] = (counts[section] ?? 0) + 1;
    }
    return STUDENT_FORM_SECTIONS.map((s) => ({
      id: s.id,
      title: s.title,
      errorCount: counts[s.id] ?? 0,
    }));
  }, [fieldErrors]);

  if (loadError) {
    return (
      <div className="card">
        <EmptyState
          icon={UserX}
          title="This student could not be opened"
          message={loadError}
          action={
            <Link className="btn btn--secondary" to="/students">
              Back to students
            </Link>
          }
        />
      </div>
    );
  }
  if (readOnly) {
    return (
      <div className="card">
        <EmptyState
          icon={Lock}
          title="You can view this student but not edit them"
          action={
            <Link className="btn btn--primary" to={`/students/${studentId}`}>
              Open the student
            </Link>
          }
        />
      </div>
    );
  }
  if (!student || !state) {
    return (
      <div className="card">
        <div className="empty-state" role="status">
          Loading…
        </div>
      </div>
    );
  }

  const { form, health, pickups, identity, status } = state;
  const identityHidden = student.identity.isMasked;
  // Allergy text needs Students.ViewSensitive, like the identity numbers. Without it the API keeps
  // the stored text whatever is sent, so the field is shown as a note instead of an empty box.
  const allergyHidden = student.identity.isMasked;

  // ---------------------------------------------------------------- setters
  const update = (patch: Partial<FormState>) =>
    setState((s) => (s ? { ...s, ...patch } : s));
  const setForm = (patch: Partial<EditForm>) =>
    setState((s) => (s ? { ...s, form: { ...s.form, ...patch } } : s));
  const setHealth = (patch: Partial<StudentHealthDto>) =>
    setState((s) => (s ? { ...s, health: { ...s.health, ...patch } } : s));
  const setIdentity = (patch: Partial<UpdateStudentIdentityRequest>) =>
    setState((s) => (s ? { ...s, identity: { ...s.identity, ...patch } } : s));
  const setPickups = (
    change: (list: PickupPersonRequest[]) => PickupPersonRequest[],
  ) => setState((s) => (s ? { ...s, pickups: change(s.pickups) } : s));

  // ---------------------------------------------------------------- field helpers
  const textField = (
    key: TextKey,
    label: string,
    opts: {
      required?: boolean;
      type?: string;
      hint?: string;
      placeholder?: string;
      span?: 1 | 2 | "full";
    } = {},
  ) => (
    <Field
      id={`student-${key}`}
      label={label}
      required={opts.required}
      error={fieldErrors[key]}
      hint={opts.hint}
      span={opts.span}
    >
      {(c) => (
        <input
          {...c}
          type={opts.type ?? "text"}
          value={form[key] ?? ""}
          placeholder={opts.placeholder}
          onChange={(e) =>
            setForm({ [key]: e.target.value } as Partial<EditForm>)
          }
        />
      )}
    </Field>
  );

  const selectField = (
    key: TextKey,
    label: string,
    options: string[],
    opts: { required?: boolean; emptyLabel?: string } = {},
  ) => (
    <Field
      id={`student-${key}`}
      label={label}
      required={opts.required}
      error={fieldErrors[key]}
    >
      {(c) => (
        <select
          {...c}
          value={form[key] ?? ""}
          onChange={(e) =>
            setForm({ [key]: e.target.value } as Partial<EditForm>)
          }
        >
          {opts.emptyLabel !== undefined && (
            <option value="">{opts.emptyLabel}</option>
          )}
          {options.map((o) => (
            <option key={o} value={o}>
              {o}
            </option>
          ))}
        </select>
      )}
    </Field>
  );

  const healthField = (
    key: HealthKey,
    label: string,
    opts: {
      type?: "text" | "date" | "textarea";
      hint?: string;
      placeholder?: string;
      span?: 1 | 2 | "full";
    } = {},
  ) => (
    <Field
      id={`student-health-${key}`}
      label={label}
      error={fieldErrors[`health.${key}`]}
      hint={opts.hint}
      span={opts.span}
    >
      {(c) =>
        opts.type === "textarea" ? (
          <textarea
            {...c}
            rows={3}
            value={health[key] ?? ""}
            placeholder={opts.placeholder}
            onChange={(e) => setHealth({ [key]: e.target.value })}
          />
        ) : (
          <input
            {...c}
            type={opts.type ?? "text"}
            value={health[key] ?? ""}
            placeholder={opts.placeholder}
            onChange={(e) => setHealth({ [key]: e.target.value })}
          />
        )
      }
    </Field>
  );

  const identityField = (
    key: IdentityKey,
    label: string,
    opts: { type?: string; placeholder?: string } = {},
  ) => (
    <Field
      id={`student-identity-${key}`}
      label={label}
      error={fieldErrors[`identity.${key}`]}
    >
      {(c) => (
        <input
          {...c}
          type={opts.type ?? "text"}
          value={identity[key] ?? ""}
          placeholder={opts.placeholder}
          onChange={(e) => setIdentity({ [key]: e.target.value })}
        />
      )}
    </Field>
  );

  const selectedDietary = (health.dietaryRequirements ?? "")
    .split(",")
    .map((s) => s.trim())
    .filter(Boolean);
  const toggleDietary = (tag: string) => {
    const next = selectedDietary.includes(tag)
      ? selectedDietary.filter((t) => t !== tag)
      : [...selectedDietary, tag];
    setHealth({ dietaryRequirements: next.join(", ") || null });
  };

  // ---------------------------------------------------------------- saving
  /** The same required-field checks the API makes, so most mistakes show before a round trip. */
  const validate = (): Record<string, string> => {
    const e: Record<string, string> = {};
    if (!form.rollNumber.trim()) e.rollNumber = "Roll number is required.";
    if (!form.classSectionId) e.classSectionId = "Choose a class.";
    if (!form.firstName.trim()) e.firstName = "First name is required.";
    if (!form.lastName.trim()) e.lastName = "Last name is required.";
    if (!form.dateOfBirth) e.dateOfBirth = "Date of birth is required.";
    pickups.forEach((p, i) => {
      if (isBlankPickup(p)) return; // an empty row is ignored
      if (!p.name.trim()) e[`pickupPersons.${i}.name`] = "Name is required.";
      if (!p.relation.trim())
        e[`pickupPersons.${i}.relation`] = "Relation is required.";
      if (!p.phone.trim()) e[`pickupPersons.${i}.phone`] = "Phone is required.";
    });
    return e;
  };

  /** Server errors split into fields with a section, and the rest (shown in the banner). */
  const showServerErrors = (fields: Record<string, string>, form: string[]) => {
    const known: Record<string, string> = {};
    const rest = [...form];
    for (const [key, message] of Object.entries(fields)) {
      if (sectionOfField(key)) known[key] = message;
      else rest.push(message);
    }
    setFieldErrors(known);
    setFormErrors(rest);
  };

  const handleSave = async () => {
    const problems = validate();
    setConflict(false);
    if (Object.keys(problems).length > 0) {
      setFieldErrors(problems);
      setFormErrors([]);
      return;
    }
    setFieldErrors({});
    setFormErrors([]);
    setIsSaving(true);

    // Blank pickup rows are dropped; remember where each sent row came from, so an API error on
    // "PickupPersons[1].Phone" lands under the right row on screen.
    const sentRows = pickups
      .map((p, index) => ({ p, index }))
      .filter(({ p }) => !isBlankPickup(p));
    const remapPickupKeys = (fields: Record<string, string>) =>
      Object.fromEntries(
        Object.entries(fields).map(([key, message]) => {
          const m = /^pickupPersons\.(\d+)(.*)$/.exec(key);
          if (!m) return [key, message];
          const original = sentRows[Number(m[1])]?.index ?? Number(m[1]);
          return [`pickupPersons.${original}${m[2]}`, message];
        }),
      );

    let saved: StudentResponse;
    try {
      const request: UpdateStudentRequest = {
        ...blankToNull(form),
        status,
        health: blankToNull(health),
        pickupPersons: sentRows.map(({ p }) => blankToNull(p)),
        rowVersion: student.rowVersion,
      };
      saved = await studentService.update(student.id, request);
    } catch (err) {
      setIsSaving(false);
      if (isConcurrencyConflict(err)) {
        setConflict(true);
        setFormErrors([
          "Someone else saved this student while you were editing. Reload to see their changes; your edits on this page will be lost.",
        ]);
        return;
      }
      const errors = getSaveErrors(err, "Could not save this student.");
      showServerErrors(remapPickupKeys(errors.fields), errors.form);
      return;
    }

    // Identity numbers have their own endpoint and permission (Students.ViewSensitive).
    if (!identityHidden) {
      try {
        await studentService.updateIdentity(student.id, blankToNull(identity));
      } catch (err) {
        setIsSaving(false);
        // The main record is saved: take its new version so the next save does not conflict.
        const savedState = stateFrom(saved);
        const keep = { ...savedState, identity };
        setStudent(saved);
        setState(keep);
        setInitial(JSON.stringify(savedState));
        const errors = getSaveErrors(
          err,
          "The identity numbers could not be saved.",
          "identity",
        );
        showServerErrors(errors.fields, [
          "The student's details were saved, but the identity numbers were not. Fix them and save again.",
          ...errors.form,
        ]);
        return;
      }
    }

    setIsSaving(false);
    const savedState = stateFrom(saved);
    setInitial(JSON.stringify(savedState));
    setState(savedState);
    toast.success(`${saved.firstName} ${saved.lastName} was saved.`);
    setLeaveTo(`/students/${student.id}`);
  };

  const fullName = `${student.firstName} ${student.lastName}`;

  return (
    <>
      <UnsavedChangesGuard when={isDirty && !isSaving && !leaveTo} />
      <FormPage
        title={`Edit ${fullName}`}
        subtitle={`Adm # ${student.admNo} · ${student.classSectionName}`}
        back={{ to: `/students/${student.id}`, label: "Back to student" }}
        sections={sectionLinks}
        formErrors={formErrors}
        formErrorAction={
          conflict ? (
            <button
              type="button"
              className="btn btn--secondary btn--sm"
              style={{ marginTop: 10 }}
              onClick={() => void load()}
            >
              Reload latest version
            </button>
          ) : undefined
        }
        onSubmit={handleSave}
        isSaving={isSaving}
        isDirty={isDirty}
        onCancel={() => navigate(`/students/${student.id}`)}
      >
        {/* 1. Identity & academic */}
        <FormSection
          id="academic"
          title="Identity & academic"
          description="Admission number and date are fixed once the student is created."
        >
          <Field id="student-admNo" label="Admission number">
            {(c) => <input {...c} value={student.admNo} disabled />}
          </Field>
          <Field
            id="student-classSectionId"
            label="Class"
            required
            error={fieldErrors.classSectionId}
          >
            {(c) => (
              <select
                {...c}
                value={form.classSectionId || ""}
                onChange={(e) =>
                  setForm({ classSectionId: Number(e.target.value) })
                }
              >
                <option value="">Select class…</option>
                {classSections
                  .filter(
                    (s) =>
                      s.status === "Active" || s.id === form.classSectionId,
                  )
                  .map((s) => (
                    <option key={s.id} value={s.id}>
                      {s.displayName}
                    </option>
                  ))}
              </select>
            )}
          </Field>
          {textField("rollNumber", "Roll number", { required: true })}
          <Field id="student-admissionDate" label="Admission date">
            {(c) => (
              <input
                {...c}
                type="date"
                value={student.admissionDate}
                disabled
              />
            )}
          </Field>
          <Field id="student-status" label="Status" error={fieldErrors.status}>
            {(c) => (
              <select
                {...c}
                value={status}
                onChange={(e) =>
                  update({ status: e.target.value as StudentStatus })
                }
              >
                <option value="Active">Active</option>
                <option value="Inactive">Inactive</option>
                <option value="Left">Left</option>
              </select>
            )}
          </Field>
          {textField("photoUrl", "Photo URL", { placeholder: "https://…" })}
        </FormSection>

        {/* 2. Personal */}
        <FormSection id="personal" title="Personal">
          {textField("firstName", "First name", { required: true })}
          {textField("middleName", "Middle name")}
          {textField("lastName", "Last name", { required: true })}
          {textField("preferredName", "Preferred name")}
          {selectField("gender", "Gender", ["Male", "Female", "Other"], {
            required: true,
          })}
          {textField("dateOfBirth", "Date of birth", {
            required: true,
            type: "date",
          })}
          <Field
            id="student-health-bloodGroup"
            label="Blood group"
            error={fieldErrors["health.bloodGroup"]}
          >
            {(c) => (
              <select
                {...c}
                value={health.bloodGroup ?? ""}
                onChange={(e) => setHealth({ bloodGroup: e.target.value })}
              >
                <option value="">Unknown</option>
                {BLOOD_GROUPS.map((b) => (
                  <option key={b} value={b}>
                    {b}
                  </option>
                ))}
              </select>
            )}
          </Field>
        </FormSection>

        {/* 3. Contact & address */}
        <FormSection
          id="contact"
          title="Contact & address"
          description="Several addresses per student come with the address book (G1)."
        >
          {textField("mobile", "Mobile", { type: "tel" })}
          {textField("email", "Email", { type: "email" })}
          {textField("addressLine", "Address", { span: 2 })}
          {textField("city", "City")}
          {textField("state", "State")}
          {textField("pincode", "Pincode")}
        </FormSection>

        {/* 4. Parents / guardians (read-only here) */}
        <FormSection id="guardians" title="Parents / guardians" grid={false}>
          {student.guardians.length === 0 ? (
            <p className="cell-muted" style={{ margin: "0 0 12px" }}>
              No parents linked yet.
            </p>
          ) : (
            <div className="contact-list" style={{ marginBottom: 12 }}>
              {student.guardians.map((g) => (
                <div key={g.parentId} className="contact-card">
                  <div className="contact-card__name">{g.parentName}</div>
                  <div className="contact-card__badges">
                    <Badge tone="info">{g.relationType}</Badge>
                    {g.isPrimaryContact && (
                      <Badge tone="warn">Primary contact</Badge>
                    )}
                  </div>
                  <div className="contact-card__line">{g.parentMobile}</div>
                </div>
              ))}
            </div>
          )}
          <div className="notice">
            <Info size={16} aria-hidden="true" />
            <span>
              Parent details and links are managed in the{" "}
              <Link to="/parents">Parents</Link> module.
            </span>
          </div>
        </FormSection>

        {/* 5. Other info */}
        <FormSection id="other" title="Other info">
          {selectField("category", "Category", CATEGORIES, { required: true })}
          {selectField("religion", "Religion", RELIGIONS, {
            emptyLabel: "Not specified",
          })}
          {textField("previousSchool", "Previous school")}
          <fieldset className="field field--plain">
            <legend>Transport required</legend>
            <div className="radio-row">
              <label>
                <input
                  type="radio"
                  name="transportRequired"
                  checked={form.transportRequired}
                  onChange={() => setForm({ transportRequired: true })}
                />
                Yes
              </label>
              <label>
                <input
                  type="radio"
                  name="transportRequired"
                  checked={!form.transportRequired}
                  onChange={() =>
                    setForm({ transportRequired: false, transportRoute: null })
                  }
                />
                No
              </label>
            </div>
          </fieldset>
          <Field
            id="student-transportRoute"
            label="Transport route"
            error={fieldErrors.transportRoute}
          >
            {(c) => (
              <input
                {...c}
                value={form.transportRoute ?? ""}
                disabled={!form.transportRequired}
                onChange={(e) => setForm({ transportRoute: e.target.value })}
              />
            )}
          </Field>
          {healthField("medicalNotes", "Medical notes", {
            type: "textarea",
            span: "full",
          })}
        </FormSection>

        {/* 6. Citizenship */}
        <FormSection id="citizenship" title="Citizenship">
          {textField("nationality", "Nationality")}
          {textField("secondNationality", "Second nationality")}
          {textField("countryOfBirth", "Country of birth")}
        </FormSection>

        {/* 7. Languages & curriculum */}
        <FormSection id="languages" title="Languages & curriculum">
          {textField("motherTongue", "Mother tongue")}
          {textField("homeLanguage", "Home language")}
          {selectField(
            "englishProficiency",
            "English proficiency",
            ENGLISH_LEVELS,
            {
              emptyLabel: "Not specified",
            },
          )}
          {selectField("curriculumTrack", "Curriculum track", CURRICULA, {
            emptyLabel: "Not specified",
          })}
          {textField("ealCode", "EAL code")}
          {selectField("admissionType", "Admission type", ["New", "Transfer"])}
        </FormSection>

        {/* 8. Safeguarding & pickup */}
        <FormSection
          id="safeguarding"
          title="Safeguarding & pickup"
          grid={false}
        >
          <div className="form-grid" style={{ marginBottom: 18 }}>
            {selectField("custodyArrangement", "Custody arrangement", CUSTODY, {
              emptyLabel: "Not specified",
            })}
          </div>

          <h3 className="form-subhead">Authorised pickup persons</h3>
          {pickups.length === 0 && (
            <p className="cell-muted" style={{ margin: "0 0 12px" }}>
              No pickup persons added.
            </p>
          )}
          {pickups.map((p, i) => {
            const k = (f: string) => `pickupPersons.${i}.${f}`;
            const setRow = (patch: Partial<PickupPersonRequest>) =>
              setPickups((list) =>
                list.map((row, idx) =>
                  idx === i ? { ...row, ...patch } : row,
                ),
              );
            return (
              <div key={i} className="repeat-row">
                <div className="repeat-row__title">Pickup person {i + 1}</div>
                <Field
                  id={`pickup-${i}-name`}
                  label="Name"
                  required
                  error={fieldErrors[k("name")]}
                >
                  {(c) => (
                    <input
                      {...c}
                      value={p.name}
                      onChange={(e) => setRow({ name: e.target.value })}
                    />
                  )}
                </Field>
                <Field
                  id={`pickup-${i}-relation`}
                  label="Relation"
                  required
                  error={fieldErrors[k("relation")]}
                >
                  {(c) => (
                    <input
                      {...c}
                      value={p.relation}
                      placeholder="e.g. Grandfather, Driver"
                      onChange={(e) => setRow({ relation: e.target.value })}
                    />
                  )}
                </Field>
                <Field
                  id={`pickup-${i}-phone`}
                  label="Phone"
                  required
                  error={fieldErrors[k("phone")]}
                >
                  {(c) => (
                    <input
                      {...c}
                      type="tel"
                      value={p.phone}
                      onChange={(e) => setRow({ phone: e.target.value })}
                    />
                  )}
                </Field>
                <Field
                  id={`pickup-${i}-idNote`}
                  label="ID note"
                  error={fieldErrors[k("idNote")]}
                >
                  {(c) => (
                    <input
                      {...c}
                      value={p.idNote ?? ""}
                      placeholder="Optional"
                      onChange={(e) =>
                        setRow({ idNote: e.target.value || null })
                      }
                    />
                  )}
                </Field>
                <button
                  type="button"
                  className="btn btn--secondary"
                  aria-label={`Remove pickup person ${i + 1}`}
                  onClick={() =>
                    setPickups((list) => list.filter((_, idx) => idx !== i))
                  }
                >
                  <Trash size={16} aria-hidden="true" />
                  Remove
                </button>
              </div>
            );
          })}
          <button
            type="button"
            className="btn btn--secondary"
            disabled={pickups.length >= MAX_PICKUP_PERSONS}
            onClick={() =>
              setPickups((list) => [
                ...list,
                { name: "", relation: "", phone: "", idNote: null },
              ])
            }
          >
            <Plus size={16} aria-hidden="true" />
            Add pickup person
          </button>
          {pickups.length >= MAX_PICKUP_PERSONS && (
            <p className="field-note">
              Up to {MAX_PICKUP_PERSONS} pickup persons.
            </p>
          )}

          <label className="checkbox-row" style={{ marginTop: 18 }}>
            <input
              type="checkbox"
              checked={form.mediaConsent}
              onChange={(e) => setForm({ mediaConsent: e.target.checked })}
            />
            Media consent: photos and videos of the student may appear in the
            yearbook and on the website
          </label>
        </FormSection>

        {/* 9. Health & safety */}
        <FormSection id="health" title="Health & safety" grid={false}>
          <fieldset className="field field--plain" style={{ marginBottom: 18 }}>
            <legend>Dietary requirements</legend>
            <div className="tag-group">
              {DIETARY_OPTIONS.map((tag) => (
                <button
                  key={tag}
                  type="button"
                  aria-pressed={selectedDietary.includes(tag)}
                  className={`tag-btn ${selectedDietary.includes(tag) ? "selected" : ""}`}
                  onClick={() => toggleDietary(tag)}
                >
                  {tag}
                </button>
              ))}
              <button
                type="button"
                aria-pressed={selectedDietary.length === 0}
                className={`tag-btn tag-btn--restrictions ${selectedDietary.length === 0 ? "selected" : ""}`}
                onClick={() => setHealth({ dietaryRequirements: null })}
              >
                No restrictions
              </button>
            </div>
          </fieldset>
          <div className="form-grid">
            {allergyHidden ? (
              <div className="field field--full">
                <span className="field-label">Allergies</span>
                <div className="notice notice--warn">
                  <Lock size={16} aria-hidden="true" />
                  <span>
                    {student.hasAllergies
                      ? "This student has allergies on record. Viewing or changing the details needs sensitive-data access."
                      : "No allergies recorded. Adding them needs sensitive-data access."}
                  </span>
                </div>
              </div>
            ) : (
              healthField("allergies", "Allergies", {
                type: "textarea",
                placeholder: "e.g. peanuts, dairy, penicillin",
                span: "full",
              })
            )}
            {healthField("insuranceProvider", "Insurance provider")}
            {healthField("insurancePolicyExpiry", "Insurance policy expiry", {
              type: "date",
            })}
          </div>
        </FormSection>

        {/* 10. House */}
        <FormSection id="house" title="House" grid={false}>
          <fieldset className="field field--plain">
            <legend className="visually-hidden">House</legend>
            <div className="tag-group">
              {HOUSE_OPTIONS.map((h) => (
                <button
                  key={h}
                  type="button"
                  aria-pressed={form.house === h}
                  className={`tag-btn house-${h} ${form.house === h ? "selected" : ""}`}
                  onClick={() =>
                    setForm({ house: form.house === h ? null : h })
                  }
                >
                  {h}
                </button>
              ))}
            </div>
            {fieldErrors.house && (
              <p className="field__error">{fieldErrors.house}</p>
            )}
          </fieldset>
        </FormSection>

        {/* 11. Welfare / finance */}
        <FormSection id="welfare" title="Welfare / finance">
          <Field
            id="student-feeConcessionPercent"
            label="Fee concession (%)"
            error={fieldErrors.feeConcessionPercent}
            hint="Scholarship, RTE or sibling discount. Flows into fee billing."
          >
            {(c) => (
              <input
                {...c}
                type="number"
                min={0}
                max={100}
                value={form.feeConcessionPercent ?? ""}
                onChange={(e) =>
                  setForm({
                    feeConcessionPercent:
                      e.target.value === "" ? null : Number(e.target.value),
                  })
                }
              />
            )}
          </Field>
          {healthField(
            "specialEducationalNeeds",
            "Special educational needs (SEN / IEP)",
            {
              type: "textarea",
              placeholder: "e.g. IEP - Dyslexia, extra time on exams",
              hint: "Visible to teaching staff for classroom accommodations.",
              span: 2,
            },
          )}
        </FormSection>

        {/* 12. Identity documents */}
        <FormSection id="identity" title="Identity documents" grid={false}>
          {identityHidden ? (
            <div className="notice notice--warn">
              <Lock size={16} aria-hidden="true" />
              <span>
                Identity numbers are hidden for your role. Only staff with
                sensitive-data access can view or change them.
              </span>
            </div>
          ) : (
            <div className="form-grid">
              {identityField("aadhaarNumber", "Aadhaar number", {
                placeholder: "12 digits",
              })}
              {identityField("passportNumber", "Passport number")}
              {identityField("passportExpiry", "Passport expiry", {
                type: "date",
              })}
              {identityField("visaType", "Visa type")}
              {identityField("visaExpiry", "Visa expiry", { type: "date" })}
            </div>
          )}
          <p className="field-note" style={{ marginTop: 14 }}>
            Scanned copies of documents come with the documents module (G1).
            Portal logins are set up separately, not here.
          </p>
        </FormSection>
      </FormPage>
    </>
  );
}
