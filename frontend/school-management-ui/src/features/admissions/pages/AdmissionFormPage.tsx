import { useCallback, useEffect, useMemo, useState } from "react";
import { Link, useNavigate, useParams } from "react-router-dom";
import { FileUp, Info, Lock, Plus, Trash, UserX } from "lucide-react";
import { admissionService } from "../services/admissionService";
import { classSectionService } from "@/features/class-sections/services/classSectionService";
import {
  isAdmissionEditable,
  type AdmissionGuardianRequest,
  type AdmissionResponse,
  type CreateAdmissionRequest,
  type GuardianRelation,
} from "../types/admission.types";
import type { ClassSectionResponse } from "@/features/class-sections/types/classSection.types";
import { FormPage } from "@/components/form/FormPage";
import { FormSection } from "@/components/form/FormSection";
import { Field } from "@/components/form/Field";
import { UnsavedChangesGuard } from "@/components/form/UnsavedChangesGuard";
import { EmptyState } from "@/components/EmptyState";
import { useToast } from "@/components/Toast";
import {
  getReadErrors,
  getSaveErrors,
  isConcurrencyConflict,
} from "@/utils/apiError";

const MAX_GUARDIANS = 5;

/** The sections in the same order as the old registration dialog, plus Documents (off until G1). */
const SECTIONS = [
  {
    id: "applicant",
    title: "Applicant",
    fields: ["firstName", "middleName", "lastName", "gender", "dateOfBirth"],
  },
  {
    id: "applying",
    title: "Applying for",
    fields: ["appliedForClassSectionId", "admissionType", "previousSchool"],
  },
  { id: "guardians", title: "Parents / guardians", fields: ["guardians"] },
  {
    id: "contact",
    title: "Contact & address",
    fields: ["phone", "email", "addressLine", "city", "state", "pincode"],
  },
  { id: "remarks", title: "Remarks", fields: ["remarks"] },
  { id: "documents", title: "Documents", fields: [] as string[] },
];

function sectionOf(key: string): string | undefined {
  return SECTIONS.find((s) =>
    s.fields.some((f) => key === f || key.startsWith(f + ".")),
  )?.id;
}

type TextKey = Exclude<
  keyof CreateAdmissionRequest,
  "guardians" | "appliedForClassSectionId"
>;

const emptyForm = (): CreateAdmissionRequest => ({
  firstName: "",
  middleName: null,
  lastName: "",
  gender: "Male",
  dateOfBirth: "",
  appliedForClassSectionId: 0,
  admissionType: "New",
  previousSchool: null,
  phone: "",
  email: null,
  addressLine: null,
  city: null,
  state: null,
  pincode: null,
  remarks: null,
  guardians: [],
});

function formFrom(a: AdmissionResponse): CreateAdmissionRequest {
  return {
    firstName: a.firstName,
    middleName: a.middleName,
    lastName: a.lastName,
    gender: a.gender,
    dateOfBirth: a.dateOfBirth,
    appliedForClassSectionId: a.appliedForClassSectionId,
    admissionType: a.admissionType,
    previousSchool: a.previousSchool,
    phone: a.phone,
    email: a.email,
    addressLine: a.addressLine,
    city: a.city,
    state: a.state,
    pincode: a.pincode,
    remarks: a.remarks,
    guardians: a.guardians.map((g) => ({
      relationType: g.relationType,
      name: g.name,
      mobile: g.mobile,
      email: g.email,
      isPrimaryContact: g.isPrimaryContact,
    })),
  };
}

const isBlankGuardian = (g: AdmissionGuardianRequest) =>
  !g.name.trim() && !g.mobile?.trim() && !g.email?.trim();

/**
 * U2: registering an application (/admissions/new) and editing it (/admissions/:id/edit) on a full
 * page, replacing the registration dialog. Same fields, same section order. Applications that are
 * already enrolled or rejected cannot be edited (the API refuses it as well).
 */
export function AdmissionFormPage() {
  const { id } = useParams();
  const isNew = id === undefined;
  const admissionId = Number(id);
  const navigate = useNavigate();
  const toast = useToast();

  const [admission, setAdmission] = useState<AdmissionResponse | null>(null);
  const [classSections, setClassSections] = useState<ClassSectionResponse[]>(
    [],
  );
  const [loadError, setLoadError] = useState<string | null>(null);
  const [isLoaded, setIsLoaded] = useState(false);

  const [form, setForm] = useState<CreateAdmissionRequest>(emptyForm);
  const [initial, setInitial] = useState(JSON.stringify(emptyForm()));
  const [fieldErrors, setFieldErrors] = useState<Record<string, string>>({});
  const [formErrors, setFormErrors] = useState<string[]>([]);
  const [conflict, setConflict] = useState(false);
  const [isSaving, setIsSaving] = useState(false);
  const [leaveTo, setLeaveTo] = useState<string | null>(null);

  const load = useCallback(async () => {
    try {
      const [sections, existing] = await Promise.all([
        classSectionService.getAll(),
        isNew ? Promise.resolve(null) : admissionService.getById(admissionId),
      ]);
      setClassSections(sections);
      if (existing) {
        const next = formFrom(existing);
        setAdmission(existing);
        setForm(next);
        setInitial(JSON.stringify(next));
      }
      setFieldErrors({});
      setFormErrors([]);
      setConflict(false);
      setIsLoaded(true);
    } catch (err) {
      setLoadError(getReadErrors(err, "Could not load this application.")[0]);
    }
  }, [isNew, admissionId]);

  useEffect(() => {
    void load();
  }, [load]);

  useEffect(() => {
    if (leaveTo) navigate(leaveTo);
  }, [leaveTo, navigate]);

  const isDirty = JSON.stringify(form) !== initial;

  const sectionLinks = useMemo(() => {
    const counts: Record<string, number> = {};
    for (const key of Object.keys(fieldErrors)) {
      const s = sectionOf(key);
      if (s) counts[s] = (counts[s] ?? 0) + 1;
    }
    return SECTIONS.map((s) => ({
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
          title="This application could not be opened"
          message={loadError}
          action={
            <Link className="btn btn--secondary" to="/admissions">
              Back to admissions
            </Link>
          }
        />
      </div>
    );
  }
  if (!isLoaded) {
    return (
      <div className="card">
        <div className="empty-state" role="status">
          Loading…
        </div>
      </div>
    );
  }

  if (admission && !isAdmissionEditable(admission.status)) {
    const enrolled = admission.status === "Enrolled";
    return (
      <div className="card" style={{ maxWidth: 620, margin: "32px auto" }}>
        <EmptyState
          icon={Lock}
          title={`This application is ${admission.status.toLowerCase()}`}
          message={
            enrolled
              ? "Enrolled applications can no longer be edited: the details now live on the student record. Edit the student instead."
              : "Rejected applications are closed and can no longer be edited."
          }
          action={
            <div
              style={{
                display: "flex",
                gap: 10,
                flexWrap: "wrap",
                justifyContent: "center",
              }}
            >
              <Link
                className="btn btn--secondary"
                to={`/admissions/${admission.id}`}
              >
                Open the application
              </Link>
              {enrolled && admission.studentId && (
                <Link
                  className="btn btn--primary"
                  to={`/students/${admission.studentId}`}
                >
                  Open the student record
                </Link>
              )}
            </div>
          }
        />
      </div>
    );
  }

  // ---------------------------------------------------------------- setters
  const set = (patch: Partial<CreateAdmissionRequest>) =>
    setForm((f) => ({ ...f, ...patch }));
  const setGuardian = (
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
    const relationType: GuardianRelation = !has("Father")
      ? "Father"
      : !has("Mother")
        ? "Mother"
        : "Guardian";
    set({
      guardians: [
        ...form.guardians,
        {
          relationType,
          name: "",
          mobile: "",
          email: "",
          isPrimaryContact: false,
        },
      ],
    });
  };
  const removeGuardian = (index: number) =>
    set({ guardians: form.guardians.filter((_, i) => i !== index) });

  const textField = (
    key: TextKey,
    label: string,
    opts: { required?: boolean; type?: string; span?: 1 | 2 | "full" } = {},
  ) => (
    <Field
      id={`admission-${key}`}
      label={label}
      required={opts.required}
      error={fieldErrors[key]}
      span={opts.span}
    >
      {(c) => (
        <input
          {...c}
          type={opts.type ?? "text"}
          value={form[key] ?? ""}
          onChange={(e) =>
            set({ [key]: e.target.value } as Partial<CreateAdmissionRequest>)
          }
        />
      )}
    </Field>
  );

  const selectedSection = classSections.find(
    (c) => c.id === form.appliedForClassSectionId,
  );

  // ---------------------------------------------------------------- saving
  const validate = (): Record<string, string> => {
    const e: Record<string, string> = {};
    if (!form.firstName.trim()) e.firstName = "First name is required.";
    if (!form.lastName.trim()) e.lastName = "Last name is required.";
    if (!form.dateOfBirth) e.dateOfBirth = "Date of birth is required.";
    if (!form.phone.trim()) e.phone = "Phone is required.";
    if (!form.appliedForClassSectionId)
      e.appliedForClassSectionId = "Choose a class.";
    const counted: Record<string, number> = {};
    form.guardians.forEach((g, i) => {
      if (isBlankGuardian(g)) return; // an empty row is ignored
      if (!g.name.trim()) e[`guardians.${i}.name`] = "Name is required.";
      if (g.relationType !== "Guardian") {
        counted[g.relationType] = (counted[g.relationType] ?? 0) + 1;
        if (counted[g.relationType] > 1)
          e[`guardians.${i}.relationType`] =
            `Only one ${g.relationType.toLowerCase()} can be added.`;
      }
    });
    return e;
  };

  const showServerErrors = (
    fields: Record<string, string>,
    messages: string[],
  ) => {
    const known: Record<string, string> = {};
    const rest = [...messages];
    for (const [key, message] of Object.entries(fields)) {
      if (sectionOf(key)) known[key] = message;
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

    // Blank guardian rows are dropped; remember where each sent row came from, so an API error on
    // "Guardians[1].Mobile" lands under the right row on screen.
    const sentRows = form.guardians
      .map((g, index) => ({ g, index }))
      .filter(({ g }) => !isBlankGuardian(g));
    const anyPrimary = sentRows.some(({ g }) => g.isPrimaryContact);
    const guardians = sentRows.map(({ g }, i) => ({
      relationType: g.relationType,
      name: g.name.trim(),
      mobile: g.mobile?.trim() || null,
      email: g.email?.trim() || null,
      // If nobody was marked as primary contact, the first guardian is.
      isPrimaryContact: anyPrimary ? g.isPrimaryContact : i === 0,
    }));
    const remap = (fields: Record<string, string>) =>
      Object.fromEntries(
        Object.entries(fields).map(([key, message]) => {
          const m = /^guardians\.(\d+)(.*)$/.exec(key);
          if (!m) return [key, message];
          return [
            `guardians.${sentRows[Number(m[1])]?.index ?? m[1]}${m[2]}`,
            message,
          ];
        }),
      );

    const request: CreateAdmissionRequest = {
      ...(Object.fromEntries(
        Object.entries(form).map(([k, v]) => [
          k,
          typeof v === "string" && v.trim() === "" ? null : v,
        ]),
      ) as CreateAdmissionRequest),
      firstName: form.firstName.trim(),
      lastName: form.lastName.trim(),
      phone: form.phone.trim(),
      guardians,
    };

    setIsSaving(true);
    try {
      const saved = admission
        ? await admissionService.update(admission.id, {
            ...request,
            rowVersion: admission.rowVersion,
          })
        : await admissionService.create(request);
      const savedForm = formFrom(saved);
      setForm(savedForm);
      setInitial(JSON.stringify(savedForm));
      toast.success(
        admission
          ? `${saved.firstName} ${saved.lastName}'s application was saved.`
          : `Application ${saved.regNo} was registered.`,
      );
      setLeaveTo(`/admissions/${saved.id}`);
    } catch (err) {
      if (isConcurrencyConflict(err)) {
        setConflict(true);
        setFormErrors([
          "Someone else saved this application while you were editing. Reload to see their changes; your edits on this page will be lost.",
        ]);
      } else {
        const errors = getSaveErrors(err, "Could not save this application.");
        showServerErrors(remap(errors.fields), errors.form);
      }
    } finally {
      setIsSaving(false);
    }
  };

  const title = admission
    ? `Edit application ${admission.regNo}`
    : "New registration";
  const backTo = admission ? `/admissions/${admission.id}` : "/admissions";

  return (
    <>
      <UnsavedChangesGuard when={isDirty && !isSaving && !leaveTo} />
      <FormPage
        title={title}
        subtitle={
          admission
            ? `${admission.firstName} ${admission.lastName} · ${admission.status}`
            : "Register an applicant. Confirmation and enrolment happen later, from the application's page."
        }
        back={{
          to: backTo,
          label: admission ? "Back to application" : "Back to admissions",
        }}
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
        saveLabel={admission ? "Save changes" : "Save registration"}
        onCancel={() => navigate(backTo)}
      >
        {/* 1. Applicant */}
        <FormSection id="applicant" title="Applicant">
          {textField("firstName", "First name", { required: true })}
          {textField("middleName", "Middle name")}
          {textField("lastName", "Last name", { required: true })}
          <Field
            id="admission-gender"
            label="Gender"
            required
            error={fieldErrors.gender}
          >
            {(c) => (
              <select
                {...c}
                value={form.gender}
                onChange={(e) => set({ gender: e.target.value })}
              >
                <option value="Male">Male</option>
                <option value="Female">Female</option>
                <option value="Other">Other</option>
              </select>
            )}
          </Field>
          {textField("dateOfBirth", "Date of birth", {
            required: true,
            type: "date",
          })}
        </FormSection>

        {/* 2. Applying for */}
        <FormSection id="applying" title="Applying for">
          <Field
            id="admission-appliedForClassSectionId"
            label="Class"
            required
            error={fieldErrors.appliedForClassSectionId}
          >
            {(c) => (
              <select
                {...c}
                value={form.appliedForClassSectionId || ""}
                onChange={(e) =>
                  set({ appliedForClassSectionId: Number(e.target.value) })
                }
              >
                <option value="">Select a class…</option>
                {classSections
                  .filter(
                    (s) =>
                      s.status === "Active" ||
                      s.id === form.appliedForClassSectionId,
                  )
                  .map((s) => (
                    <option key={s.id} value={s.id}>
                      {s.displayName}
                    </option>
                  ))}
              </select>
            )}
          </Field>
          <Field
            id="admission-admissionType"
            label="Admission type"
            error={fieldErrors.admissionType}
          >
            {(c) => (
              <select
                {...c}
                value={form.admissionType}
                onChange={(e) => set({ admissionType: e.target.value })}
              >
                <option value="New">New</option>
                <option value="Transfer">Transfer</option>
              </select>
            )}
          </Field>
          <Field
            id="admission-academicYear"
            label="Academic year"
            hint="Follows the selected class."
          >
            {(c) => (
              <input
                {...c}
                value={selectedSection?.academicYearName ?? "—"}
                disabled
              />
            )}
          </Field>
          {textField("previousSchool", "Previous school", { span: 2 })}
        </FormSection>

        {/* 3. Parents / guardians (exactly as before: typed in, matched at enrolment) */}
        <FormSection
          id="guardians"
          title="Parents / guardians"
          grid={false}
          description="Optional while the application is a draft. Enrolment needs at least one guardian with a mobile number: the number is used to find parents already in the school."
        >
          {fieldErrors.guardians && (
            <p className="field__error" style={{ marginBottom: 12 }}>
              {fieldErrors.guardians}
            </p>
          )}
          {form.guardians.length === 0 && (
            <p className="cell-muted" style={{ margin: "0 0 12px" }}>
              No guardians added yet.
            </p>
          )}
          {form.guardians.map((g, i) => {
            const k = (f: string) => `guardians.${i}.${f}`;
            return (
              <div key={i} className="repeat-row">
                <div className="repeat-row__title">Guardian {i + 1}</div>
                <Field
                  id={`guardian-${i}-relation`}
                  label="Relation"
                  error={fieldErrors[k("relationType")]}
                >
                  {(c) => (
                    <select
                      {...c}
                      value={g.relationType}
                      onChange={(e) =>
                        setGuardian(i, {
                          relationType: e.target.value as GuardianRelation,
                        })
                      }
                    >
                      <option value="Father">Father</option>
                      <option value="Mother">Mother</option>
                      <option value="Guardian">Guardian</option>
                    </select>
                  )}
                </Field>
                <Field
                  id={`guardian-${i}-name`}
                  label="Name"
                  required
                  error={fieldErrors[k("name")]}
                >
                  {(c) => (
                    <input
                      {...c}
                      value={g.name}
                      onChange={(e) => setGuardian(i, { name: e.target.value })}
                    />
                  )}
                </Field>
                <Field
                  id={`guardian-${i}-mobile`}
                  label="Mobile"
                  error={fieldErrors[k("mobile")]}
                >
                  {(c) => (
                    <input
                      {...c}
                      type="tel"
                      value={g.mobile ?? ""}
                      onChange={(e) =>
                        setGuardian(i, { mobile: e.target.value })
                      }
                    />
                  )}
                </Field>
                <Field
                  id={`guardian-${i}-email`}
                  label="Email"
                  error={fieldErrors[k("email")]}
                >
                  {(c) => (
                    <input
                      {...c}
                      type="email"
                      value={g.email ?? ""}
                      onChange={(e) =>
                        setGuardian(i, { email: e.target.value })
                      }
                    />
                  )}
                </Field>
                <label
                  className="checkbox-row"
                  style={{ marginTop: 0, minHeight: 44 }}
                >
                  <input
                    type="radio"
                    name="primaryGuardian"
                    checked={g.isPrimaryContact}
                    onChange={() => setPrimary(i)}
                  />
                  Primary contact
                </label>
                <button
                  type="button"
                  className="btn btn--secondary"
                  aria-label={`Remove guardian ${i + 1}`}
                  onClick={() => removeGuardian(i)}
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
            onClick={addGuardian}
            disabled={form.guardians.length >= MAX_GUARDIANS}
          >
            <Plus size={16} aria-hidden="true" />
            Add guardian
          </button>
          {form.guardians.length >= MAX_GUARDIANS && (
            <p className="field-note">Up to {MAX_GUARDIANS} guardians.</p>
          )}
        </FormSection>

        {/* 4. Contact & address */}
        <FormSection id="contact" title="Contact & address">
          {textField("phone", "Phone", { required: true, type: "tel" })}
          {textField("email", "Email", { type: "email" })}
          {textField("addressLine", "Address", { span: 2 })}
          {textField("city", "City")}
          {textField("state", "State")}
          {textField("pincode", "Pincode")}
        </FormSection>

        {/* 5. Remarks */}
        <FormSection id="remarks" title="Remarks">
          <Field
            id="admission-remarks"
            label="Remarks"
            span="full"
            error={fieldErrors.remarks}
          >
            {(c) => (
              <textarea
                {...c}
                rows={3}
                value={form.remarks ?? ""}
                placeholder="Optional notes about this application"
                onChange={(e) => set({ remarks: e.target.value })}
              />
            )}
          </Field>
        </FormSection>

        {/* 6. Documents: in the layout now, switched on with G1 */}
        <FormSection
          id="documents"
          title="Documents"
          grid={false}
          description="Birth certificate, previous school records, photos. PDF, JPG or PNG, up to 5 MB each."
        >
          <div className="dropzone dropzone--disabled" aria-disabled="true">
            <FileUp size={28} aria-hidden="true" />
            <strong>Document upload is not switched on yet</strong>
            <span>
              It arrives with the documents module (G1). Files picked here will
              be uploaded right after the application is saved.
            </span>
          </div>
          <div className="notice" style={{ marginTop: 12 }}>
            <Info size={16} aria-hidden="true" />
            <span>Until then, keep paper copies with the application.</span>
          </div>
        </FormSection>
      </FormPage>
    </>
  );
}
