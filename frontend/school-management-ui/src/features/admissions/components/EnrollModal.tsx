import { useState } from "react";
import { ArrowLeft, GraduationCap, TriangleAlert } from "lucide-react";
import type {
  AdmissionResponse,
  EnrollAdmissionRequest,
  GuardianDecision,
  GuardianMatch,
} from "../types/admission.types";
import type { ClassSectionResponse } from "@/features/class-sections/types/classSection.types";
import type { ParentContactResponse } from "@/features/parents/types/parent.types";
import { parentService } from "@/features/parents/services/parentService";
import { admissionService } from "../services/admissionService";
import { GuardianMatchCard, type GuardianChoice } from "./GuardianMatchCard";
import { Dialog } from "@/components/Dialog";
import { DialogFooter } from "@/components/DialogFooter";
import { Notice } from "@/components/Notice";
import { ReviewList } from "@/components/ReviewList";
import {
  CheckboxField,
  SelectField,
  TextField,
} from "@/components/form/Inputs";
import { useAsyncData } from "@/hooks/useAsyncData";
import { useDialogSubmit } from "@/hooks/useDialogSubmit";
import { CURRICULA, ENGLISH_LEVELS, HOUSES } from "@/types/lookups";
import { getApiErrors } from "@/utils/apiError";
import { formatDate } from "@/utils/format";
import { blankToNull, fieldBinder } from "@/utils/form";

interface Props {
  admission: AdmissionResponse;
  classSections: ClassSectionResponse[];
  onClose: () => void;
  onSubmit: (id: number, data: EnrollAdmissionRequest) => Promise<void>;
}

/** Pre-select only what the server is sure about; a contested or multiple match stays empty. */
function initialChoices(
  guardians: GuardianMatch[],
): Record<number, GuardianChoice> {
  const initial: Record<number, GuardianChoice> = {};
  for (const g of guardians) {
    initial[g.admissionGuardianId] =
      g.suggestedAction === "UseExisting" && g.candidates.length > 0
        ? g.candidates[0].parentId
        : g.suggestedAction === "CreateNew"
          ? "new"
          : null;
  }
  return initial;
}

/**
 * Admitted → Enrolled, in two steps:
 *   1. details: class, numbers, and for each guardian "link to an existing parent" or "new parent";
 *   2. review: a summary of exactly what will be created, then Enrol.
 * The API re-checks everything, and refuses a second enrolment of the same application (409).
 */
export function EnrollModal({
  admission,
  classSections,
  onClose,
  onSubmit,
}: Props) {
  const [step, setStep] = useState<"details" | "review">("details");
  const [form, setForm] = useState<EnrollAdmissionRequest>({
    rollNumber: "",
    admissionNumber: "",
    admissionDate: new Date().toISOString().slice(0, 10),
    entryPoint: null,
    transportRequired: admission.transportRequired,
    allottedClassSectionId: admission.appliedForClassSectionId,
    nationality: null,
    curriculumTrack: null,
    englishProficiency: null,
    ealCode: null,
    house: null,
    allergies: null,
    guardians: [],
  });
  const patch = (changes: Partial<EnrollAdmissionRequest>) =>
    setForm((f) => ({ ...f, ...changes }));
  const [fieldErrors, setFieldErrors] = useState<Record<string, string>>({});
  const bind = fieldBinder(form, patch, fieldErrors, "enrol");
  const { isSubmitting, errors, setErrors, submit } = useDialogSubmit(onClose);

  const [choices, setChoices] = useState<Record<number, GuardianChoice>>({});
  const matchesData = useAsyncData(
    async () => {
      const res = await admissionService.getGuardianMatches(admission.id);
      setChoices(initialChoices(res.guardians));
      return res.guardians;
    },
    [admission.id],
    "Could not load the parent matches.",
  );
  const matches = matchesData.data ?? [];

  // "Search another parent": all parents are loaded only when staff first open the search.
  const [allParents, setAllParents] = useState<ParentContactResponse[] | null>(
    null,
  );
  const [extraParents, setExtraParents] = useState<
    Record<number, ParentContactResponse[]>
  >({});
  const loadAllParents = () => {
    if (allParents !== null) return;
    parentService
      .getAll()
      .then(setAllParents)
      .catch((err) => setErrors(getApiErrors(err, "Could not load parents.")));
  };

  // These cannot be fixed here: the application itself has to be edited first.
  const isBlocked = (action: GuardianMatch["suggestedAction"]) =>
    action === "MissingMobile" || action === "ParentRemoved";
  const hasBlockedGuardian = matches.some((g) => isBlocked(g.suggestedAction));
  const allottedSection = classSections.find(
    (c) => c.id === form.allottedClassSectionId,
  );
  const name = `${admission.firstName} ${admission.lastName}`;

  /** Name of the parent a guardian will be linked to (for the summary). */
  const parentName = (guardianId: number, parentId: number) =>
    matches
      .find((m) => m.admissionGuardianId === guardianId)
      ?.candidates.find((c) => c.parentId === parentId)?.name ??
    extraParents[guardianId]?.find((p) => p.id === parentId)?.name ??
    `Parent #${parentId}`;

  /** Checks step 1 and builds the guardian decisions; null when something must be fixed first. */
  const buildDecisions = (): GuardianDecision[] | null => {
    const problems: string[] = [];
    const fields: Record<string, string> = {};
    if (!form.allottedClassSectionId)
      fields.allottedClassSectionId = "Choose a class.";
    if (!form.rollNumber.trim()) fields.rollNumber = "Roll number is required.";
    if (!form.admissionNumber.trim())
      fields.admissionNumber = "Admission number is required.";
    if (!form.admissionDate)
      fields.admissionDate = "Admission date is required.";

    if (matches.length === 0)
      problems.push(
        "This application has no guardians. Edit the application and add at least one with a mobile number.",
      );
    for (const g of matches.filter(
      (m) => m.suggestedAction === "MissingMobile",
    ))
      problems.push(
        `${g.name} has no mobile number. Edit the application and add one first.`,
      );
    for (const g of matches.filter(
      (m) => m.suggestedAction === "ParentRemoved",
    ))
      problems.push(
        `The parent chosen for ${g.name} has been deleted. Edit the application and choose “Use a different person” first.`,
      );

    const decisions: GuardianDecision[] = [];
    const used = new Set<number>();
    for (const g of matches.filter((m) => !isBlocked(m.suggestedAction))) {
      const choice = choices[g.admissionGuardianId];
      if (choice === null || choice === undefined) {
        problems.push(
          `Choose an existing parent or "New parent" for ${g.name}.`,
        );
      } else if (choice === "new") {
        decisions.push({
          admissionGuardianId: g.admissionGuardianId,
          action: "CreateNew",
          parentId: null,
        });
      } else {
        if (used.has(choice))
          problems.push(
            "The same parent cannot be linked as two guardians of this student.",
          );
        used.add(choice);
        decisions.push({
          admissionGuardianId: g.admissionGuardianId,
          action: "UseExisting",
          parentId: choice,
        });
      }
    }

    setFieldErrors(fields);
    setErrors(problems);
    return problems.length > 0 || Object.keys(fields).length > 0
      ? null
      : decisions;
  };

  const handleEnrol = () => {
    const decisions = buildDecisions();
    if (!decisions) {
      setStep("details");
      return;
    }
    void submit(
      () =>
        onSubmit(admission.id, {
          ...blankToNull(form),
          rollNumber: form.rollNumber.trim(),
          admissionNumber: form.admissionNumber.trim(),
          guardians: decisions,
        }),
      "Could not enrol this applicant.",
    );
  };

  const footer =
    step === "details" ? (
      <DialogFooter
        onCancel={onClose}
        onSubmit={() => buildDecisions() && setStep("review")}
        label="Review enrolment"
        busyLabel="Review enrolment"
        isSubmitting={false}
        disabled={matchesData.isLoading || hasBlockedGuardian}
      />
    ) : (
      <>
        <button
          type="button"
          className="btn btn--secondary"
          onClick={() => {
            setErrors([]);
            setStep("details");
          }}
          disabled={isSubmitting}
        >
          <ArrowLeft size={16} aria-hidden="true" />
          Back
        </button>
        <button
          type="button"
          className="btn btn--primary"
          onClick={handleEnrol}
          disabled={isSubmitting}
        >
          {isSubmitting ? "Enrolling…" : "Enrol student"}
        </button>
      </>
    );

  return (
    <Dialog
      title={step === "details" ? "Enrol to class" : "Review before enrolling"}
      icon={GraduationCap}
      subtitle={`${name} · ${admission.regNo} · step ${step === "details" ? 1 : 2} of 2`}
      wide
      errors={matchesData.error ? [matchesData.error, ...errors] : errors}
      onClose={onClose}
      closable={!isSubmitting}
      footer={footer}
    >
      {step === "details" ? (
        <>
          <h3 className="form-subhead">Class and numbers</h3>
          <div className="form-grid" style={{ marginBottom: 18 }}>
            <SelectField
              {...bind("allottedClassSectionId")}
              label="Class"
              required
              emptyLabel="Select a class…"
              options={classSections
                .filter((s) => s.status === "Active")
                .map((s) => ({ value: s.id, label: s.displayName }))}
              value={form.allottedClassSectionId || ""}
              onChange={(v) => patch({ allottedClassSectionId: Number(v) })}
            />
            <TextField {...bind("rollNumber")} label="Roll number" required />
            <TextField
              {...bind("admissionNumber")}
              label="Admission number"
              required
            />
            <TextField
              {...bind("admissionDate")}
              label="Admission date"
              type="date"
              required
            />
            <TextField {...bind("entryPoint")} label="Entry point" />
            <CheckboxField
              id="enrol-transport"
              label="Transport required"
              checked={form.transportRequired}
              onChange={(v) => patch({ transportRequired: v })}
            />
          </div>

          <h3 className="form-subhead">Parents / guardians</h3>
          <p className="field-note" style={{ margin: "-4px 0 12px" }}>
            Parents are matched by mobile number. Confirm for each guardian
            whether this is a parent already in the school or a new one: nothing
            is linked without your choice.
          </p>
          {matchesData.isLoading && (
            <p role="status">Checking for existing parents…</p>
          )}
          {matches.map((g) => (
            <GuardianMatchCard
              key={g.admissionGuardianId}
              guardian={g}
              choice={choices[g.admissionGuardianId] ?? null}
              onChoose={(c) =>
                setChoices((x) => ({ ...x, [g.admissionGuardianId]: c }))
              }
              extraParents={extraParents[g.admissionGuardianId] ?? []}
              onPickExtra={(p) => {
                setExtraParents((m) => ({
                  ...m,
                  [g.admissionGuardianId]: [
                    ...(m[g.admissionGuardianId] ?? []).filter(
                      (x) => x.id !== p.id,
                    ),
                    p,
                  ],
                }));
                setChoices((x) => ({ ...x, [g.admissionGuardianId]: p.id }));
              }}
              allParents={allParents}
              onSearchOpened={loadAllParents}
            />
          ))}

          <h3 className="form-subhead" style={{ marginTop: 18 }}>
            Student profile (optional)
          </h3>
          <p className="field-note" style={{ margin: "-4px 0 12px" }}>
            Not asked at registration. Add them now if known, or later on the
            student record.
          </p>
          <div className="form-grid">
            <TextField {...bind("nationality")} label="Nationality" />
            <SelectField
              {...bind("curriculumTrack")}
              label="Curriculum track"
              options={CURRICULA}
              emptyLabel="Not specified"
            />
            <SelectField
              {...bind("englishProficiency")}
              label="English proficiency"
              options={ENGLISH_LEVELS}
              emptyLabel="Not specified"
            />
            <TextField {...bind("ealCode")} label="EAL code" />
            <SelectField
              {...bind("house")}
              label="House"
              options={HOUSES}
              emptyLabel="Not assigned"
            />
            <TextField {...bind("allergies")} label="Allergies" span="full" />
          </div>
        </>
      ) : (
        <>
          <Notice tone="warn" icon={TriangleAlert} style={{ marginBottom: 16 }}>
            Enrolling creates the student record and the parent links below, and
            closes this application. It cannot be undone from here.
          </Notice>
          <ReviewList
            title="Student"
            items={[
              { label: "Name", value: name },
              { label: "Class", value: allottedSection?.displayName ?? "—" },
              { label: "Roll number", value: form.rollNumber },
              { label: "Admission number", value: form.admissionNumber },
              {
                label: "Admission date",
                value: formatDate(form.admissionDate),
              },
              {
                label: "Transport",
                value: form.transportRequired ? "Required" : "Not required",
              },
              { label: "House", value: form.house },
            ]}
          />
          <ReviewList
            title="Parents"
            items={matches
              .filter((g) => !isBlocked(g.suggestedAction))
              .map((g) => {
                const choice = choices[g.admissionGuardianId];
                return {
                  label: `${g.relationType}: ${g.name}`,
                  value:
                    choice === "new"
                      ? "New parent will be created"
                      : typeof choice === "number"
                        ? `Linked to existing parent ${parentName(g.admissionGuardianId, choice)}`
                        : "—",
                };
              })}
          />
          <p className="field-note">
            No portal login is created. That stays a separate step.
          </p>
        </>
      )}
    </Dialog>
  );
}
