import { useEffect } from "react";
import { Link, useNavigate, useParams } from "react-router-dom";
import { Lock } from "lucide-react";
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
import { FormPage } from "@/components/form/FormPage";
import { FormSection } from "@/components/form/FormSection";
import {
  CheckboxField,
  SelectField,
  TextAreaField,
  TextField,
  ToggleGroup,
} from "@/components/form/Inputs";
import { AddRowButton, RepeatRow } from "@/components/form/RepeatRow";
import { ContactCard, ContactList } from "@/components/detail/ContactCard";
import { EmptyState } from "@/components/EmptyState";
import { Notice } from "@/components/Notice";
import { Badge } from "@/components/Badge";
import { PageLoadError, PageLoading } from "@/components/PageState";
import { useAsyncData } from "@/hooks/useAsyncData";
import { useRecordForm } from "@/hooks/useRecordForm";
import {
  blankToNull,
  fieldBinder,
  numberOrNull,
  remapRowErrors,
} from "@/utils/form";
import {
  BLOOD_GROUPS,
  CATEGORIES,
  CURRICULA,
  CUSTODY_ARRANGEMENTS,
  DIETARY_OPTIONS,
  ENGLISH_LEVELS,
  GENDERS,
  HOUSES,
  RELIGIONS,
} from "@/types/lookups";
import { STUDENT_FORM_SECTIONS } from "./studentFormSections";

const MAX_PICKUP_PERSONS = 10;

/** The editable part of a student (status, health, pickup persons and identity are kept apart). */
type EditForm = Omit<
  UpdateStudentRequest,
  "rowVersion" | "health" | "pickupPersons" | "status"
>;

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

/** Placeholder until the student has loaded (never shown: the page shows "Loading…" until then). */
const EMPTY_STATE: FormState = {
  form: {} as EditForm,
  status: "Active",
  health: {} as StudentHealthDto,
  pickups: [],
  identity: {} as UpdateStudentIdentityRequest,
};

const isBlankPickup = (p: PickupPersonRequest) =>
  !p.name.trim() && !p.relation.trim() && !p.phone.trim() && !p.idNote?.trim();

/**
 * U2: editing a student on a full page (/students/:id/edit). Same fields and section order as the
 * old dialog. Two requests: the student (PUT /students/{id}) and, for staff with sensitive-data
 * access, the identity numbers (PUT /students/{id}/identity).
 *
 * There is no "new student" page: students are created by enrolling an admission.
 */
export function StudentEditPage() {
  const studentId = Number(useParams().id);
  const navigate = useNavigate();

  const loaded = useAsyncData(
    async () => {
      const [student, classSections] = await Promise.all([
        studentService.getById(studentId),
        classSectionService.getAll(),
      ]);
      return { student, classSections };
    },
    [studentId],
    "Could not load this student.",
  );
  const record = useRecordForm<FormState>({
    sections: STUDENT_FORM_SECTIONS,
    initial: EMPTY_STATE,
    noun: "student",
    onReload: () => void loaded.reload(),
  });
  const { reset } = record;

  const student =
    loaded.data && isFullStudent(loaded.data.student)
      ? loaded.data.student
      : null;
  useEffect(() => {
    if (student) reset(stateFrom(student));
  }, [student, reset]);

  if (loaded.error) {
    return (
      <PageLoadError
        title="This student could not be opened"
        message={loaded.error}
        backTo="/students"
        backLabel="Back to students"
      />
    );
  }
  if (loaded.data && !student) {
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
  if (!student || record.form === EMPTY_STATE) return <PageLoading />;

  const classSections = loaded.data?.classSections ?? [];
  const { form, health, pickups, identity, status } = record.form;
  const errors = record.fieldErrors;
  // Identity numbers and allergy text need Students.ViewSensitive. Without it the API keeps the
  // stored values whatever is sent, so they are shown as notes instead of empty boxes.
  const sensitiveHidden = student.identity.isMasked;

  // ---------------------------------------------------------------- setters
  const update = (changes: Partial<FormState>) => record.patch(changes);
  const setForm = (changes: Partial<EditForm>) =>
    record.setForm((s) => ({ ...s, form: { ...s.form, ...changes } }));
  const setHealth = (changes: Partial<StudentHealthDto>) =>
    record.setForm((s) => ({ ...s, health: { ...s.health, ...changes } }));
  const setIdentity = (changes: Partial<UpdateStudentIdentityRequest>) =>
    record.setForm((s) => ({ ...s, identity: { ...s.identity, ...changes } }));
  const setPickups = (
    change: (list: PickupPersonRequest[]) => PickupPersonRequest[],
  ) => record.setForm((s) => ({ ...s, pickups: change(s.pickups) }));

  const bind = fieldBinder(form, setForm, errors, "student");
  const bindHealth = fieldBinder(
    health,
    setHealth,
    errors,
    "student",
    "health.",
  );
  const bindIdentity = fieldBinder(
    identity,
    setIdentity,
    errors,
    "student",
    "identity.",
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

  // Two requests, so this page uses the hook's lower-level steps instead of record.save().
  const handleSave = async () => {
    const problems = validate();
    if (Object.keys(problems).length > 0) {
      record.showProblems(problems);
      return;
    }
    const sent = pickups
      .map((p, index) => ({ p, index }))
      .filter(({ p }) => !isBlankPickup(p));
    record.setIsSaving(true);

    let saved: StudentResponse;
    try {
      saved = await studentService.update(student.id, {
        ...blankToNull(form),
        status,
        health: blankToNull(health),
        pickupPersons: sent.map(({ p }) => blankToNull(p)),
        rowVersion: student.rowVersion,
      });
    } catch (err) {
      record.setIsSaving(false);
      record.showApiError(err, "Could not save this student.", {
        remap: (f) =>
          remapRowErrors(
            f,
            "pickupPersons",
            sent.map((r) => r.index),
          ),
      });
      return;
    }

    if (!sensitiveHidden) {
      try {
        await studentService.updateIdentity(student.id, blankToNull(identity));
      } catch (err) {
        record.setIsSaving(false);
        // The main record is saved: take its new version so the next save does not conflict,
        // and keep the identity numbers the user typed.
        loaded.setData({ student: saved, classSections });
        record.reset(stateFrom(saved));
        setIdentity(identity);
        record.showApiError(err, "The identity numbers could not be saved.", {
          prefix: "identity",
          leadMessages: [
            "The student's details were saved, but the identity numbers were not. Fix them and save again.",
          ],
        });
        return;
      }
    }

    record.setIsSaving(false);
    record.finishSaved(
      stateFrom(saved),
      `${saved.firstName} ${saved.lastName} was saved.`,
      `/students/${student.id}`,
    );
  };

  return (
    <>
      {record.guard}
      <FormPage
        {...record.pageProps}
        title={`Edit ${student.firstName} ${student.lastName}`}
        subtitle={`Adm # ${student.admNo} · ${student.classSectionName}`}
        back={{ to: `/students/${student.id}`, label: "Back to student" }}
        onSubmit={handleSave}
        onCancel={() => navigate(`/students/${student.id}`)}
      >
        {/* 1. Identity & academic */}
        <FormSection
          id="academic"
          title="Identity & academic"
          description="Admission number and date are fixed once the student is created."
        >
          <TextField
            id="student-admNo"
            label="Admission number"
            value={student.admNo}
            onChange={() => {}}
            disabled
          />
          <SelectField
            {...bind("classSectionId")}
            label="Class"
            required
            emptyLabel="Select class…"
            options={classSections
              .filter(
                (c) => c.status === "Active" || c.id === form.classSectionId,
              )
              .map((c) => ({ value: c.id, label: c.displayName }))}
            onChange={(v) => setForm({ classSectionId: Number(v) })}
          />
          <TextField {...bind("rollNumber")} label="Roll number" required />
          <TextField
            id="student-admissionDate"
            label="Admission date"
            type="date"
            value={student.admissionDate}
            onChange={() => {}}
            disabled
          />
          <SelectField
            id="student-status"
            label="Status"
            error={errors.status}
            value={status}
            options={["Active", "Inactive", "Left"]}
            onChange={(v) => update({ status: v as StudentStatus })}
          />
          <TextField
            {...bind("photoUrl")}
            label="Photo URL"
            type="url"
            placeholder="https://…"
          />
        </FormSection>

        {/* 2. Personal */}
        <FormSection id="personal" title="Personal">
          <TextField {...bind("firstName")} label="First name" required />
          <TextField {...bind("middleName")} label="Middle name" />
          <TextField {...bind("lastName")} label="Last name" required />
          <TextField {...bind("preferredName")} label="Preferred name" />
          <SelectField
            {...bind("gender")}
            label="Gender"
            required
            options={GENDERS}
          />
          <TextField
            {...bind("dateOfBirth")}
            label="Date of birth"
            type="date"
            required
          />
          <SelectField
            {...bindHealth("bloodGroup")}
            label="Blood group"
            emptyLabel="Unknown"
            options={BLOOD_GROUPS}
          />
        </FormSection>

        {/* 3. Contact & address */}
        <FormSection
          id="contact"
          title="Contact & address"
          description="Several addresses per student come with the address book (G1)."
        >
          <TextField {...bind("mobile")} label="Mobile" type="tel" />
          <TextField {...bind("email")} label="Email" type="email" />
          <TextField {...bind("addressLine")} label="Address" span={2} />
          <TextField {...bind("city")} label="City" />
          <TextField {...bind("state")} label="State" />
          <TextField {...bind("pincode")} label="Pincode" />
        </FormSection>

        {/* 4. Parents / guardians (read-only here) */}
        <FormSection id="guardians" title="Parents / guardians" grid={false}>
          {student.guardians.length === 0 ? (
            <p className="cell-muted" style={{ margin: "0 0 12px" }}>
              No parents linked yet.
            </p>
          ) : (
            <div style={{ marginBottom: 12 }}>
              <ContactList>
                {student.guardians.map((g) => (
                  <ContactCard
                    key={g.parentId}
                    name={g.parentName}
                    phone={g.parentMobile}
                    badges={
                      <>
                        <Badge tone="info">{g.relationType}</Badge>
                        {g.isPrimaryContact && (
                          <Badge tone="warn">Primary contact</Badge>
                        )}
                      </>
                    }
                  />
                ))}
              </ContactList>
            </div>
          )}
          <Notice>
            Parent details and links are managed in the{" "}
            <Link to="/parents">Parents</Link> module.
          </Notice>
        </FormSection>

        {/* 5. Other info */}
        <FormSection id="other" title="Other info">
          <SelectField
            {...bind("category")}
            label="Category"
            required
            options={CATEGORIES}
          />
          <SelectField
            {...bind("religion")}
            label="Religion"
            emptyLabel="Not specified"
            options={RELIGIONS}
          />
          <TextField {...bind("previousSchool")} label="Previous school" />
          <CheckboxField
            id="student-transportRequired"
            label="Transport required"
            checked={form.transportRequired}
            onChange={(checked) =>
              setForm(
                checked
                  ? { transportRequired: true }
                  : { transportRequired: false, transportRoute: null },
              )
            }
          />
          <TextField
            {...bind("transportRoute")}
            label="Transport route"
            disabled={!form.transportRequired}
          />
          <TextAreaField
            {...bindHealth("medicalNotes")}
            label="Medical notes"
            span="full"
          />
        </FormSection>

        {/* 6. Citizenship */}
        <FormSection id="citizenship" title="Citizenship">
          <TextField {...bind("nationality")} label="Nationality" />
          <TextField
            {...bind("secondNationality")}
            label="Second nationality"
          />
          <TextField {...bind("countryOfBirth")} label="Country of birth" />
        </FormSection>

        {/* 7. Languages & curriculum */}
        <FormSection id="languages" title="Languages & curriculum">
          <TextField {...bind("motherTongue")} label="Mother tongue" />
          <TextField {...bind("homeLanguage")} label="Home language" />
          <SelectField
            {...bind("englishProficiency")}
            label="English proficiency"
            emptyLabel="Not specified"
            options={ENGLISH_LEVELS}
          />
          <SelectField
            {...bind("curriculumTrack")}
            label="Curriculum track"
            emptyLabel="Not specified"
            options={CURRICULA}
          />
          <TextField {...bind("ealCode")} label="EAL code" />
          <SelectField
            {...bind("admissionType")}
            label="Admission type"
            options={["New", "Transfer"]}
          />
        </FormSection>

        {/* 8. Safeguarding & pickup */}
        <FormSection
          id="safeguarding"
          title="Safeguarding & pickup"
          grid={false}
        >
          <div className="form-grid" style={{ marginBottom: 18 }}>
            <SelectField
              {...bind("custodyArrangement")}
              label="Custody arrangement"
              emptyLabel="Not specified"
              options={CUSTODY_ARRANGEMENTS}
            />
          </div>

          <h3 className="form-subhead">Authorised pickup persons</h3>
          {errors.pickupPersons && (
            <p className="field__error" style={{ marginBottom: 12 }}>
              {errors.pickupPersons}
            </p>
          )}
          {pickups.length === 0 && (
            <p className="cell-muted" style={{ margin: "0 0 12px" }}>
              No pickup persons added.
            </p>
          )}
          {pickups.map((p, i) => {
            const setRow = (changes: Partial<PickupPersonRequest>) =>
              setPickups((list) =>
                list.map((row, idx) =>
                  idx === i ? { ...row, ...changes } : row,
                ),
              );
            const bindRow = fieldBinder(
              p,
              setRow,
              errors,
              `pickup-${i}`,
              `pickupPersons.${i}.`,
            );
            return (
              <RepeatRow
                key={i}
                title={`Pickup person ${i + 1}`}
                onRemove={() =>
                  setPickups((list) => list.filter((_, idx) => idx !== i))
                }
              >
                <TextField {...bindRow("name")} label="Name" required />
                <TextField
                  {...bindRow("relation")}
                  label="Relation"
                  required
                  placeholder="e.g. Grandfather, Driver"
                />
                <TextField
                  {...bindRow("phone")}
                  label="Phone"
                  type="tel"
                  required
                />
                <TextField
                  {...bindRow("idNote")}
                  label="ID note"
                  placeholder="Optional"
                />
              </RepeatRow>
            );
          })}
          <AddRowButton
            label="Add pickup person"
            onClick={() =>
              setPickups((list) => [
                ...list,
                { name: "", relation: "", phone: "", idNote: null },
              ])
            }
            count={pickups.length}
            max={MAX_PICKUP_PERSONS}
            pluralNoun="pickup persons"
          />

          <div style={{ marginTop: 18 }}>
            <CheckboxField
              id="student-mediaConsent"
              label="Media consent: photos and videos of the student may appear in the yearbook and on the website"
              checked={form.mediaConsent}
              onChange={(checked) => setForm({ mediaConsent: checked })}
            />
          </div>
        </FormSection>

        {/* 9. Health & safety */}
        <FormSection id="health" title="Health & safety" grid={false}>
          <div style={{ marginBottom: 18 }}>
            <ToggleGroup
              legend="Dietary requirements"
              options={DIETARY_OPTIONS}
              isSelected={(tag) => selectedDietary.includes(tag)}
              onToggle={toggleDietary}
              extra={
                <button
                  type="button"
                  aria-pressed={selectedDietary.length === 0}
                  className={`tag-btn tag-btn--restrictions ${selectedDietary.length === 0 ? "selected" : ""}`}
                  onClick={() => setHealth({ dietaryRequirements: null })}
                >
                  No restrictions
                </button>
              }
            />
          </div>
          <div className="form-grid">
            {sensitiveHidden ? (
              <div className="field field--full">
                <span className="field-label">Allergies</span>
                <Notice tone="warn" icon={Lock}>
                  {student.hasAllergies
                    ? "This student has allergies on record. Viewing or changing the details needs sensitive-data access."
                    : "No allergies recorded. Adding them needs sensitive-data access."}
                </Notice>
              </div>
            ) : (
              <TextAreaField
                {...bindHealth("allergies")}
                label="Allergies"
                placeholder="e.g. peanuts, dairy, penicillin"
                span="full"
              />
            )}
            <TextField
              {...bindHealth("insuranceProvider")}
              label="Insurance provider"
            />
            <TextField
              {...bindHealth("insurancePolicyExpiry")}
              label="Insurance policy expiry"
              type="date"
            />
          </div>
        </FormSection>

        {/* 10. House */}
        <FormSection id="house" title="House" grid={false}>
          <ToggleGroup
            legend="House"
            hideLegend
            options={HOUSES}
            isSelected={(h) => form.house === h}
            onToggle={(h) => setForm({ house: form.house === h ? null : h })}
          />
          {errors.house && <p className="field__error">{errors.house}</p>}
        </FormSection>

        {/* 11. Welfare / finance */}
        <FormSection id="welfare" title="Welfare / finance">
          <TextField
            {...bind("feeConcessionPercent")}
            label="Fee concession (%)"
            type="number"
            min={0}
            max={100}
            hint="Scholarship, RTE or sibling discount. Flows into fee billing."
            onChange={(v) => setForm({ feeConcessionPercent: numberOrNull(v) })}
          />
          <TextAreaField
            {...bindHealth("specialEducationalNeeds")}
            label="Special educational needs (SEN / IEP)"
            placeholder="e.g. IEP - Dyslexia, extra time on exams"
            hint="Visible to teaching staff for classroom accommodations."
            span={2}
          />
        </FormSection>

        {/* 12. Identity documents */}
        <FormSection id="identity" title="Identity documents" grid={false}>
          {sensitiveHidden ? (
            <Notice tone="warn" icon={Lock}>
              Identity numbers are hidden for your role. Only staff with
              sensitive-data access can view or change them.
            </Notice>
          ) : (
            <div className="form-grid">
              <TextField
                {...bindIdentity("aadhaarNumber")}
                label="Aadhaar number"
                placeholder="12 digits"
              />
              <TextField
                {...bindIdentity("passportNumber")}
                label="Passport number"
              />
              <TextField
                {...bindIdentity("passportExpiry")}
                label="Passport expiry"
                type="date"
              />
              <TextField {...bindIdentity("visaType")} label="Visa type" />
              <TextField
                {...bindIdentity("visaExpiry")}
                label="Visa expiry"
                type="date"
              />
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
