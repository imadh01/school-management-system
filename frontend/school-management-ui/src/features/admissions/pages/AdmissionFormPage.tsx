import { useEffect } from "react";
import { Link, useNavigate, useParams } from "react-router-dom";
import { FileUp, Lock } from "lucide-react";
import { admissionService } from "../services/admissionService";
import { classSectionService } from "@/features/class-sections/services/classSectionService";
import {
  isAdmissionEditable,
  type AdmissionGuardianRequest,
  type AdmissionResponse,
  type CreateAdmissionRequest,
  type GuardianRelation,
} from "../types/admission.types";
import { FormPage } from "@/components/form/FormPage";
import { FormSection } from "@/components/form/FormSection";
import {
  SelectField,
  TextAreaField,
  TextField,
} from "@/components/form/Inputs";
import { AddRowButton, RepeatRow } from "@/components/form/RepeatRow";
import { AddressListEditor } from "@/components/form/AddressListEditor";
import { EmptyState } from "@/components/EmptyState";
import { Notice } from "@/components/Notice";
import { PageLoadError, PageLoading } from "@/components/PageState";
import { useAsyncData } from "@/hooks/useAsyncData";
import { useCountries } from "@/hooks/useCountries";
import { useRecordForm } from "@/hooks/useRecordForm";
import type { AddressFormRow } from "@/types/address";
import {
  toAddressRequests,
  toAddressRows,
  validateAddresses,
} from "@/utils/address";
import { GENDERS, GUARDIAN_RELATIONS } from "@/types/lookups";
import {
  blankToNull,
  fieldBinder,
  remapRowErrors,
  type FormSectionDef,
} from "@/utils/form";

const MAX_GUARDIANS = 5;

/** The sections in the same order as the old registration dialog, plus Documents (off until G1). */
const SECTIONS: readonly FormSectionDef[] = [
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
    fields: ["phone", "email", "addresses"],
  },
  { id: "remarks", title: "Remarks", fields: ["remarks"] },
  { id: "documents", title: "Documents", fields: [] },
];

/** The form holds addresses as editable rows; everything else is the request itself. */
type FormState = Omit<CreateAdmissionRequest, "addresses"> & {
  addresses: AddressFormRow[];
};

const EMPTY_FORM: FormState = {
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
  addresses: [],
  remarks: null,
  guardians: [],
};

function formFrom(a: AdmissionResponse): FormState {
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
    addresses: toAddressRows(a.addresses),
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

/** Shown instead of the form for an application that is already enrolled or rejected (the API refuses edits too). */
function LockedApplication({ admission }: { admission: AdmissionResponse }) {
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

/**
 * U2: registering an application (/admissions/new) and editing it (/admissions/:id/edit) on a full
 * page. Applications that are already enrolled or rejected cannot be edited (the API refuses it too).
 */
export function AdmissionFormPage() {
  const { id } = useParams();
  const isNew = id === undefined;
  const admissionId = Number(id);
  const navigate = useNavigate();

  const loaded = useAsyncData(
    async () => {
      const [classSections, admission] = await Promise.all([
        classSectionService.getAll(),
        isNew ? Promise.resolve(null) : admissionService.getById(admissionId),
      ]);
      return { classSections, admission };
    },
    [isNew, admissionId],
    "Could not load this application.",
  );
  const countries = useCountries();
  const record = useRecordForm<FormState>({
    sections: SECTIONS,
    initial: EMPTY_FORM,
    noun: "application",
    onReload: () => void loaded.reload(),
  });
  const { reset } = record;

  const admission = loaded.data?.admission ?? null;
  useEffect(() => {
    if (admission) reset(formFrom(admission));
  }, [admission, reset]);

  if (loaded.error) {
    return (
      <PageLoadError
        title="This application could not be opened"
        message={loaded.error}
        backTo="/admissions"
        backLabel="Back to admissions"
      />
    );
  }
  if (!loaded.data || (admission && record.form === EMPTY_FORM))
    return <PageLoading />;
  if (admission && !isAdmissionEditable(admission.status))
    return <LockedApplication admission={admission} />;

  const classSections = loaded.data.classSections;
  const form = record.form;
  const errors = record.fieldErrors;
  const bind = fieldBinder(form, record.patch, errors, "admission");
  const selectedSection = classSections.find(
    (c) => c.id === form.appliedForClassSectionId,
  );

  // ---------------------------------------------------------------- guardians
  const setGuardians = (
    change: (list: AdmissionGuardianRequest[]) => AdmissionGuardianRequest[],
  ) => record.setForm((f) => ({ ...f, guardians: change(f.guardians) }));
  const setGuardian = (
    index: number,
    changes: Partial<AdmissionGuardianRequest>,
  ) =>
    setGuardians((list) =>
      list.map((g, i) => (i === index ? { ...g, ...changes } : g)),
    );
  const addGuardian = () => {
    // Suggest the next missing relation so the common case is one click.
    const has = (r: GuardianRelation) =>
      form.guardians.some((g) => g.relationType === r);
    const relationType: GuardianRelation = !has("Father")
      ? "Father"
      : !has("Mother")
        ? "Mother"
        : "Guardian";
    setGuardians((list) => [
      ...list,
      {
        relationType,
        name: "",
        mobile: "",
        email: "",
        isPrimaryContact: false,
      },
    ]);
  };

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
    return { ...e, ...validateAddresses(form.addresses, countries.data ?? []) };
  };

  const handleSave = () => {
    // Blank guardian rows are dropped; remember where each sent row came from, so an API error on
    // "Guardians[1].Mobile" lands under the right row on screen.
    const sent = form.guardians
      .map((g, index) => ({ g, index }))
      .filter(({ g }) => !isBlankGuardian(g));
    const anyPrimary = sent.some(({ g }) => g.isPrimaryContact);
    const request: CreateAdmissionRequest = {
      ...blankToNull(form),
      firstName: form.firstName.trim(),
      lastName: form.lastName.trim(),
      phone: form.phone.trim(),
      addresses: toAddressRequests(form.addresses),
      guardians: sent.map(({ g }, i) => ({
        relationType: g.relationType,
        name: g.name.trim(),
        mobile: g.mobile?.trim() || null,
        email: g.email?.trim() || null,
        // If nobody was marked as primary contact, the first guardian is.
        isPrimaryContact: anyPrimary ? g.isPrimaryContact : i === 0,
      })),
    };

    void record.save({
      validate,
      submit: () =>
        admission
          ? admissionService.update(admission.id, {
              ...request,
              rowVersion: admission.rowVersion,
            })
          : admissionService.create(request),
      fallback: "Could not save this application.",
      remap: (f) =>
        remapRowErrors(
          f,
          "guardians",
          sent.map((r) => r.index),
        ),
      onSaved: (saved) => ({
        form: formFrom(saved),
        message: admission
          ? `${saved.firstName} ${saved.lastName}'s application was saved.`
          : `Application ${saved.regNo} was registered.`,
        goTo: `/admissions/${saved.id}`,
      }),
    });
  };

  const backTo = admission ? `/admissions/${admission.id}` : "/admissions";

  return (
    <>
      {record.guard}
      <FormPage
        {...record.pageProps}
        title={
          admission ? `Edit application ${admission.regNo}` : "New registration"
        }
        subtitle={
          admission
            ? `${admission.firstName} ${admission.lastName} · ${admission.status}`
            : "Register an applicant. Confirmation and enrolment happen later, from the application's page."
        }
        back={{
          to: backTo,
          label: admission ? "Back to application" : "Back to admissions",
        }}
        onSubmit={handleSave}
        saveLabel={admission ? "Save changes" : "Save registration"}
        onCancel={() => navigate(backTo)}
      >
        {/* 1. Applicant */}
        <FormSection id="applicant" title="Applicant">
          <TextField {...bind("firstName")} label="First name" required />
          <TextField {...bind("middleName")} label="Middle name" />
          <TextField {...bind("lastName")} label="Last name" required />
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
        </FormSection>

        {/* 2. Applying for */}
        <FormSection id="applying" title="Applying for">
          <SelectField
            {...bind("appliedForClassSectionId")}
            label="Class"
            required
            emptyLabel="Select a class…"
            options={classSections
              .filter(
                (s) =>
                  s.status === "Active" ||
                  s.id === form.appliedForClassSectionId,
              )
              .map((s) => ({ value: s.id, label: s.displayName }))}
            value={form.appliedForClassSectionId || ""}
            onChange={(v) =>
              record.patch({ appliedForClassSectionId: Number(v) })
            }
          />
          <SelectField
            {...bind("admissionType")}
            label="Admission type"
            options={["New", "Transfer"]}
          />
          <TextField
            id="admission-academicYear"
            label="Academic year"
            hint="Follows the selected class."
            value={selectedSection?.academicYearName ?? "—"}
            onChange={() => {}}
            disabled
          />
          <TextField
            {...bind("previousSchool")}
            label="Previous school"
            span={2}
          />
        </FormSection>

        {/* 3. Parents / guardians (typed in here, matched to existing parents at enrolment) */}
        <FormSection
          id="guardians"
          title="Parents / guardians"
          grid={false}
          description="Optional while the application is a draft. Enrolment needs at least one guardian with a mobile number: the number is used to find parents already in the school."
        >
          {errors.guardians && (
            <p className="field__error" style={{ marginBottom: 12 }}>
              {errors.guardians}
            </p>
          )}
          {form.guardians.length === 0 && (
            <p className="cell-muted" style={{ margin: "0 0 12px" }}>
              No guardians added yet.
            </p>
          )}
          {form.guardians.map((g, i) => {
            const bindRow = fieldBinder(
              g,
              (c) => setGuardian(i, c),
              errors,
              "admission",
              `guardians.${i}.`,
            );
            return (
              <RepeatRow
                key={i}
                title={`Guardian ${i + 1}`}
                onRemove={() =>
                  setGuardians((list) => list.filter((_, x) => x !== i))
                }
              >
                <SelectField
                  {...bindRow("relationType")}
                  label="Relation"
                  options={GUARDIAN_RELATIONS}
                />
                <TextField {...bindRow("name")} label="Name" required />
                <TextField {...bindRow("mobile")} label="Mobile" type="tel" />
                <TextField {...bindRow("email")} label="Email" type="email" />
                <label
                  className="checkbox-row"
                  style={{ marginTop: 0, minHeight: 44 }}
                >
                  <input
                    type="radio"
                    name="primaryGuardian"
                    checked={g.isPrimaryContact}
                    onChange={() =>
                      setGuardians((list) =>
                        list.map((x, j) => ({
                          ...x,
                          isPrimaryContact: j === i,
                        })),
                      )
                    }
                  />
                  Primary contact
                </label>
              </RepeatRow>
            );
          })}
          <AddRowButton
            label="Add guardian"
            onClick={addGuardian}
            count={form.guardians.length}
            max={MAX_GUARDIANS}
            pluralNoun="guardians"
          />
        </FormSection>

        {/* 4. Contact & address */}
        <FormSection id="contact" title="Contact & address" grid={false}>
          <div className="form-grid" style={{ marginBottom: 18 }}>
            <TextField {...bind("phone")} label="Phone" type="tel" required />
            <TextField {...bind("email")} label="Email" type="email" />
          </div>
          <AddressListEditor
            idPrefix="admission"
            rows={form.addresses}
            onChange={(addresses) => record.patch({ addresses })}
            countries={countries.data ?? []}
            countriesLoading={countries.isLoading}
            errors={errors}
          />
        </FormSection>

        {/* 5. Remarks */}
        <FormSection id="remarks" title="Remarks">
          <TextAreaField
            {...bind("remarks")}
            label="Remarks"
            span="full"
            placeholder="Optional notes about this application"
          />
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
          <Notice style={{ marginTop: 12 }}>
            Until then, keep paper copies with the application.
          </Notice>
        </FormSection>
      </FormPage>
    </>
  );
}
