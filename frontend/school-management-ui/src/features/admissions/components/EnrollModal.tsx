import { useEffect, useMemo, useState } from "react";
import { ArrowLeft, GraduationCap, Search, TriangleAlert } from "lucide-react";
import type {
  AdmissionResponse,
  EnrollAdmissionRequest,
  GuardianDecision,
  GuardianMatch,
} from "../types/admission.types";
import type { ClassSectionResponse } from "@/features/class-sections/types/classSection.types";
import type { ParentResponse } from "@/features/parents/types/parent.types";
import { parentService } from "@/features/parents/services/parentService";
import { admissionService } from "../services/admissionService";
import { Dialog } from "@/components/Dialog";
import { Field } from "@/components/form/Field";
import { getApiErrors } from "@/utils/apiError";

interface Props {
  admission: AdmissionResponse;
  classSections: ClassSectionResponse[];
  onClose: () => void;
  onSubmit: (id: number, data: EnrollAdmissionRequest) => Promise<void>;
}

/** Staff's choice for one guardian: a parent id, "new", or not yet chosen. */
type Choice = number | "new" | null;

const HOUSES = ["Red", "Blue", "Green", "Yellow"];
const CURRICULA = ["British", "American", "IB", "National"];
const ENGLISH_LEVELS = [
  "Native",
  "C2 Proficient",
  "C1 Advanced",
  "B2 Upper Intermediate",
  "B1 Intermediate",
  "A2 Elementary",
];

function formatDate(iso: string): string {
  const d = new Date(`${iso}T00:00:00`);
  return Number.isNaN(d.getTime())
    ? iso
    : d.toLocaleDateString("en-GB", {
        day: "numeric",
        month: "short",
        year: "numeric",
      });
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
  const [matches, setMatches] = useState<GuardianMatch[]>([]);
  const [choices, setChoices] = useState<Record<number, Choice>>({});
  const [isLoadingMatches, setIsLoadingMatches] = useState(true);
  const [isSubmitting, setIsSubmitting] = useState(false);
  const [errors, setErrors] = useState<string[]>([]);
  const [fieldErrors, setFieldErrors] = useState<Record<string, string>>({});

  // "Search another parent": all parents are loaded only when staff ask for it.
  const [allParents, setAllParents] = useState<ParentResponse[] | null>(null);
  const [searchOpenFor, setSearchOpenFor] = useState<number | null>(null);
  const [searchText, setSearchText] = useState("");
  const [extraCandidates, setExtraCandidates] = useState<
    Record<number, ParentResponse[]>
  >({});

  useEffect(() => {
    let cancelled = false;
    admissionService
      .getGuardianMatches(admission.id)
      .then((res) => {
        if (cancelled) return;
        setMatches(res.guardians);
        // Pre-select only what the server is sure about. A contested or multiple match stays
        // empty so staff must choose deliberately.
        const initial: Record<number, Choice> = {};
        res.guardians.forEach((g) => {
          if (g.suggestedAction === "UseExisting" && g.candidates.length > 0)
            initial[g.admissionGuardianId] = g.candidates[0].parentId;
          else if (g.suggestedAction === "CreateNew")
            initial[g.admissionGuardianId] = "new";
          else initial[g.admissionGuardianId] = null;
        });
        setChoices(initial);
      })
      .catch((err) => {
        if (!cancelled)
          setErrors(getApiErrors(err, "Could not load the parent matches."));
      })
      .finally(() => {
        if (!cancelled) setIsLoadingMatches(false);
      });
    return () => {
      cancelled = true;
    };
  }, [admission.id]);

  const searchResults = useMemo(() => {
    const q = searchText.trim().toLowerCase();
    if (!allParents || q.length < 2) return [];
    return allParents
      .filter((p) => `${p.name} ${p.mobile}`.toLowerCase().includes(q))
      .slice(0, 8);
  }, [allParents, searchText]);

  const openSearch = async (guardianId: number) => {
    setSearchOpenFor(guardianId);
    setSearchText("");
    if (allParents === null) {
      try {
        setAllParents(await parentService.getAll());
      } catch (err) {
        setErrors(getApiErrors(err, "Could not load parents."));
      }
    }
  };

  const pickFromSearch = (guardianId: number, parent: ParentResponse) => {
    setExtraCandidates((m) => ({
      ...m,
      [guardianId]: [
        ...(m[guardianId] ?? []).filter((p) => p.id !== parent.id),
        parent,
      ],
    }));
    setChoices((c) => ({ ...c, [guardianId]: parent.id }));
    setSearchOpenFor(null);
  };

  const set = (patch: Partial<EnrollAdmissionRequest>) =>
    setForm((f) => ({ ...f, ...patch }));
  const text = (v: string) => (v.trim() === "" ? null : v);

  const hasMissingMobile = matches.some(
    (g) => g.suggestedAction === "MissingMobile",
  );
  const allottedSection = classSections.find(
    (c) => c.id === form.allottedClassSectionId,
  );

  /** Name of the parent a guardian will be linked to (for the summary). */
  const parentName = (guardianId: number, parentId: number) => {
    const g = matches.find((m) => m.admissionGuardianId === guardianId);
    return (
      g?.candidates.find((c) => c.parentId === parentId)?.name ??
      extraCandidates[guardianId]?.find((p) => p.id === parentId)?.name ??
      `Parent #${parentId}`
    );
  };

  /** Checks step 1 and builds the guardian decisions; null when something must be fixed first. */
  const buildDecisions = (): GuardianDecision[] | null => {
    const problems: string[] = [];
    const fields: Record<string, string> = {};
    if (!form.allottedClassSectionId) fields.class = "Choose a class.";
    if (!form.rollNumber.trim()) fields.roll = "Roll number is required.";
    if (!form.admissionNumber.trim())
      fields.admNo = "Admission number is required.";
    if (!form.admissionDate) fields.date = "Admission date is required.";

    if (matches.length === 0)
      problems.push(
        "This application has no guardians. Edit the application and add at least one with a mobile number.",
      );
    matches
      .filter((g) => g.suggestedAction === "MissingMobile")
      .forEach((g) =>
        problems.push(
          `${g.name} has no mobile number. Edit the application and add one first.`,
        ),
      );

    const decisions: GuardianDecision[] = [];
    const used = new Set<number>();
    matches
      .filter((g) => g.suggestedAction !== "MissingMobile")
      .forEach((g) => {
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
      });

    setFieldErrors(fields);
    setErrors(problems);
    return problems.length > 0 || Object.keys(fields).length > 0
      ? null
      : decisions;
  };

  const goToReview = () => {
    if (buildDecisions()) setStep("review");
  };

  const handleEnrol = async () => {
    const decisions = buildDecisions();
    if (!decisions) {
      setStep("details");
      return;
    }
    setIsSubmitting(true);
    try {
      await onSubmit(admission.id, {
        ...form,
        rollNumber: form.rollNumber.trim(),
        admissionNumber: form.admissionNumber.trim(),
        guardians: decisions,
      });
      onClose();
    } catch (err) {
      setErrors(getApiErrors(err, "Could not enrol this applicant."));
      setIsSubmitting(false);
    }
  };

  const name = `${admission.firstName} ${admission.lastName}`;

  const footer =
    step === "details" ? (
      <>
        <button type="button" className="btn btn--secondary" onClick={onClose}>
          Cancel
        </button>
        <button
          type="button"
          className="btn btn--primary"
          onClick={goToReview}
          disabled={isLoadingMatches || hasMissingMobile}
        >
          Review enrolment
        </button>
      </>
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
      subtitle={`${name} · ${admission.regNo}${step === "details" ? " · step 1 of 2" : " · step 2 of 2"}`}
      wide
      errors={errors}
      onClose={onClose}
      closable={!isSubmitting}
      footer={footer}
    >
      {step === "details" ? (
        <>
          <h3 className="form-subhead">Class and numbers</h3>
          <div className="form-grid" style={{ marginBottom: 18 }}>
            <Field
              id="enrol-class"
              label="Class"
              required
              error={fieldErrors.class}
            >
              {(c) => (
                <select
                  {...c}
                  value={form.allottedClassSectionId || ""}
                  onChange={(e) =>
                    set({ allottedClassSectionId: Number(e.target.value) })
                  }
                >
                  <option value="">Select a class…</option>
                  {classSections
                    .filter((s) => s.status === "Active")
                    .map((s) => (
                      <option key={s.id} value={s.id}>
                        {s.displayName}
                      </option>
                    ))}
                </select>
              )}
            </Field>
            <Field
              id="enrol-roll"
              label="Roll number"
              required
              error={fieldErrors.roll}
            >
              {(c) => (
                <input
                  {...c}
                  value={form.rollNumber}
                  onChange={(e) => set({ rollNumber: e.target.value })}
                />
              )}
            </Field>
            <Field
              id="enrol-admno"
              label="Admission number"
              required
              error={fieldErrors.admNo}
            >
              {(c) => (
                <input
                  {...c}
                  value={form.admissionNumber}
                  onChange={(e) => set({ admissionNumber: e.target.value })}
                />
              )}
            </Field>
            <Field
              id="enrol-date"
              label="Admission date"
              required
              error={fieldErrors.date}
            >
              {(c) => (
                <input
                  {...c}
                  type="date"
                  value={form.admissionDate}
                  onChange={(e) => set({ admissionDate: e.target.value })}
                />
              )}
            </Field>
            <Field id="enrol-entry" label="Entry point">
              {(c) => (
                <input
                  {...c}
                  value={form.entryPoint ?? ""}
                  onChange={(e) => set({ entryPoint: text(e.target.value) })}
                />
              )}
            </Field>
            <label
              className="checkbox-row"
              style={{ alignSelf: "end", minHeight: 44 }}
            >
              <input
                type="checkbox"
                checked={form.transportRequired}
                onChange={(e) => set({ transportRequired: e.target.checked })}
              />
              Transport required
            </label>
          </div>

          <h3 className="form-subhead">Parents / guardians</h3>
          <p className="field-note" style={{ margin: "-4px 0 12px" }}>
            Parents are matched by mobile number. Confirm for each guardian
            whether this is a parent already in the school or a new one: nothing
            is linked without your choice.
          </p>
          {isLoadingMatches && (
            <p role="status">Checking for existing parents…</p>
          )}
          {matches.map((g) => {
            const id = g.admissionGuardianId;
            const extras = extraCandidates[id] ?? [];
            const groupName = `enrol-guardian-${id}`;
            return (
              <fieldset key={id} className="choice-card">
                <legend className="choice-card__title">
                  {g.relationType}: {g.name}{" "}
                  <span>· {g.mobile ?? "no mobile"}</span>
                </legend>
                {g.suggestedAction === "MissingMobile" ? (
                  <p className="field__error">
                    A mobile number is required. Edit the application and add
                    one before enrolling.
                  </p>
                ) : (
                  <>
                    {g.suggestedAction === "MustChoose" && (
                      <p
                        className="notice notice--warn"
                        style={{ margin: "0 0 8px" }}
                      >
                        <TriangleAlert size={16} aria-hidden="true" />
                        <span>
                          More than one parent has this number. Choose
                          carefully.
                        </span>
                      </p>
                    )}
                    {g.candidates.length === 0 && extras.length === 0 && (
                      <p className="field-note" style={{ margin: "0 0 6px" }}>
                        No parent in the school has this mobile number.
                      </p>
                    )}
                    {g.candidates.map((c) => (
                      <label key={c.parentId} className="choice-option">
                        <input
                          type="radio"
                          name={groupName}
                          checked={choices[id] === c.parentId}
                          onChange={() =>
                            setChoices((x) => ({ ...x, [id]: c.parentId }))
                          }
                        />
                        <span>
                          Link to existing parent <b>{c.name}</b> · {c.mobile}
                          {c.linkedChildren.length > 0 && (
                            <small>
                              Children: {c.linkedChildren.join(", ")}
                            </small>
                          )}
                        </span>
                      </label>
                    ))}
                    {extras.map((p) => (
                      <label key={p.id} className="choice-option">
                        <input
                          type="radio"
                          name={groupName}
                          checked={choices[id] === p.id}
                          onChange={() =>
                            setChoices((x) => ({ ...x, [id]: p.id }))
                          }
                        />
                        <span>
                          Link to existing parent <b>{p.name}</b> · {p.mobile}
                        </span>
                      </label>
                    ))}
                    <label className="choice-option">
                      <input
                        type="radio"
                        name={groupName}
                        checked={choices[id] === "new"}
                        onChange={() =>
                          setChoices((x) => ({ ...x, [id]: "new" }))
                        }
                      />
                      <span>New parent (create from this guardian)</span>
                    </label>

                    {searchOpenFor === id ? (
                      <div style={{ marginTop: 8 }}>
                        <Field
                          id={`enrol-search-${id}`}
                          label="Search parents by name or mobile"
                        >
                          {(c) => (
                            <input
                              {...c}
                              autoFocus
                              placeholder="At least 2 characters"
                              value={searchText}
                              onChange={(e) => setSearchText(e.target.value)}
                            />
                          )}
                        </Field>
                        {searchResults.map((p) => (
                          <div key={p.id} className="choice-option">
                            <button
                              type="button"
                              className="btn btn--secondary btn--sm"
                              onClick={() => pickFromSearch(id, p)}
                            >
                              Select
                            </button>
                            <span>
                              {p.name} · {p.mobile}
                            </span>
                          </div>
                        ))}
                      </div>
                    ) : (
                      <button
                        type="button"
                        className="btn btn--secondary btn--sm"
                        style={{ marginTop: 6 }}
                        onClick={() => void openSearch(id)}
                      >
                        <Search size={14} aria-hidden="true" />
                        Search another parent…
                      </button>
                    )}
                  </>
                )}
              </fieldset>
            );
          })}

          <h3 className="form-subhead" style={{ marginTop: 18 }}>
            Student profile (optional)
          </h3>
          <p className="field-note" style={{ margin: "-4px 0 12px" }}>
            Not asked at registration. Add them now if known, or later on the
            student record.
          </p>
          <div className="form-grid">
            <Field id="enrol-nationality" label="Nationality">
              {(c) => (
                <input
                  {...c}
                  value={form.nationality ?? ""}
                  onChange={(e) => set({ nationality: text(e.target.value) })}
                />
              )}
            </Field>
            <Field id="enrol-curriculum" label="Curriculum track">
              {(c) => (
                <select
                  {...c}
                  value={form.curriculumTrack ?? ""}
                  onChange={(e) =>
                    set({ curriculumTrack: text(e.target.value) })
                  }
                >
                  <option value="">Not specified</option>
                  {CURRICULA.map((x) => (
                    <option key={x}>{x}</option>
                  ))}
                </select>
              )}
            </Field>
            <Field id="enrol-english" label="English proficiency">
              {(c) => (
                <select
                  {...c}
                  value={form.englishProficiency ?? ""}
                  onChange={(e) =>
                    set({ englishProficiency: text(e.target.value) })
                  }
                >
                  <option value="">Not specified</option>
                  {ENGLISH_LEVELS.map((x) => (
                    <option key={x}>{x}</option>
                  ))}
                </select>
              )}
            </Field>
            <Field id="enrol-eal" label="EAL code">
              {(c) => (
                <input
                  {...c}
                  value={form.ealCode ?? ""}
                  onChange={(e) => set({ ealCode: text(e.target.value) })}
                />
              )}
            </Field>
            <Field id="enrol-house" label="House">
              {(c) => (
                <select
                  {...c}
                  value={form.house ?? ""}
                  onChange={(e) => set({ house: text(e.target.value) })}
                >
                  <option value="">Not assigned</option>
                  {HOUSES.map((x) => (
                    <option key={x}>{x}</option>
                  ))}
                </select>
              )}
            </Field>
            <Field id="enrol-allergies" label="Allergies" span="full">
              {(c) => (
                <input
                  {...c}
                  value={form.allergies ?? ""}
                  onChange={(e) => set({ allergies: text(e.target.value) })}
                />
              )}
            </Field>
          </div>
        </>
      ) : (
        <>
          <div className="notice notice--warn" style={{ marginBottom: 16 }}>
            <TriangleAlert size={16} aria-hidden="true" />
            <span>
              Enrolling creates the student record and the parent links below,
              and closes this application. It cannot be undone from here.
            </span>
          </div>

          <h3 className="review-heading">Student</h3>
          <dl className="review-list">
            <dt>Name</dt>
            <dd>{name}</dd>
            <dt>Class</dt>
            <dd>{allottedSection?.displayName ?? "—"}</dd>
            <dt>Roll number</dt>
            <dd>{form.rollNumber}</dd>
            <dt>Admission number</dt>
            <dd>{form.admissionNumber}</dd>
            <dt>Admission date</dt>
            <dd>{formatDate(form.admissionDate)}</dd>
            <dt>Transport</dt>
            <dd>{form.transportRequired ? "Required" : "Not required"}</dd>
            {form.house && (
              <>
                <dt>House</dt>
                <dd>{form.house}</dd>
              </>
            )}
          </dl>

          <h3 className="review-heading">Parents</h3>
          <dl className="review-list">
            {matches
              .filter((g) => g.suggestedAction !== "MissingMobile")
              .map((g) => {
                const choice = choices[g.admissionGuardianId];
                return (
                  <div
                    key={g.admissionGuardianId}
                    style={{ display: "contents" }}
                  >
                    <dt>
                      {g.relationType}: {g.name}
                    </dt>
                    <dd>
                      {choice === "new"
                        ? "New parent will be created"
                        : typeof choice === "number"
                          ? `Linked to existing parent ${parentName(g.admissionGuardianId, choice)}`
                          : "—"}
                    </dd>
                  </div>
                );
              })}
          </dl>
          <p className="field-note">
            No portal login is created. That stays a separate step.
          </p>
        </>
      )}
    </Dialog>
  );
}
