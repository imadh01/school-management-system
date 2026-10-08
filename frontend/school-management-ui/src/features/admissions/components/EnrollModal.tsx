import { useEffect, useMemo, useState } from "react";
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
import { ValidationModal } from "@/components/ValidationModal";
import { getApiErrors } from "@/utils/apiError";

interface Props {
  admission: AdmissionResponse | null;
  classSections: ClassSectionResponse[];
  onClose: () => void;
  onSubmit: (id: number, data: EnrollAdmissionRequest) => Promise<void>;
}

/** Staff's choice for one guardian: a parent id, "new", or not yet chosen. */
type Choice = number | "new" | null;

const emptyForm: EnrollAdmissionRequest = {
  rollNumber: "",
  admissionNumber: "",
  admissionDate: new Date().toISOString().slice(0, 10),
  entryPoint: "",
  transportRequired: false,
  allottedClassSectionId: 0,
  nationality: null,
  curriculumTrack: null,
  englishProficiency: null,
  ealCode: null,
  house: null,
  allergies: null,
  guardians: [],
};

export function EnrollModal({
  admission,
  classSections,
  onClose,
  onSubmit,
}: Props) {
  const [form, setForm] = useState<EnrollAdmissionRequest>(emptyForm);
  const [matches, setMatches] = useState<GuardianMatch[]>([]);
  const [choices, setChoices] = useState<Record<number, Choice>>({});
  const [isLoadingMatches, setIsLoadingMatches] = useState(false);
  const [isSubmitting, setIsSubmitting] = useState(false);
  const [errors, setErrors] = useState<string[] | null>(null);

  // "Search another parent" support — loaded only when staff ask for it.
  const [allParents, setAllParents] = useState<ParentResponse[] | null>(null);
  const [searchOpenFor, setSearchOpenFor] = useState<number | null>(null);
  const [searchText, setSearchText] = useState("");

  // Reset and load the server's guardian matches each time a different
  // admission is opened.
  useEffect(() => {
    setForm({
      ...emptyForm,
      admissionDate: new Date().toISOString().slice(0, 10),
      allottedClassSectionId: admission?.appliedForClassSectionId ?? 0,
      transportRequired: admission?.transportRequired ?? false,
    });
    setMatches([]);
    setChoices({});
    setSearchOpenFor(null);
    setSearchText("");
    setErrors(null);
    if (!admission) return;

    let cancelled = false;
    setIsLoadingMatches(true);
    admissionService
      .getGuardianMatches(admission.id)
      .then((res) => {
        if (cancelled) return;
        setMatches(res.guardians);
        // Pre-select only what the server is sure about. A contested or
        // multiple match stays empty so staff must choose deliberately.
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
  }, [admission]);

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

  const searchResults = useMemo(() => {
    const q = searchText.trim().toLowerCase();
    if (!allParents || q.length < 2) return [];
    return allParents
      .filter((p) => `${p.name} ${p.mobile}`.toLowerCase().includes(q))
      .slice(0, 8);
  }, [allParents, searchText]);

  // Parents picked through search must also appear as a selectable option.
  const [extraCandidates, setExtraCandidates] = useState<
    Record<number, ParentResponse[]>
  >({});

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

  if (!admission) return null;

  const field = (key: keyof EnrollAdmissionRequest, value: string) =>
    setForm((f) => ({ ...f, [key]: value === "" ? null : value }));

  const hasMissingMobile = matches.some(
    (g) => g.suggestedAction === "MissingMobile",
  );

  const handleSubmit = async () => {
    const problems: string[] = [];
    if (!form.rollNumber) problems.push("Roll number is required.");
    if (!form.admissionNumber) problems.push("Admission number is required.");
    if (!form.allottedClassSectionId)
      problems.push("Allotted class is required.");

    if (matches.length === 0)
      problems.push(
        "Add at least one guardian to this application before enrolling.",
      );
    matches
      .filter((g) => g.suggestedAction === "MissingMobile")
      .forEach((g) =>
        problems.push(
          `${g.name} has no mobile number. Edit the application and add one before enrolling.`,
        ),
      );

    const decisions: GuardianDecision[] = [];
    const usedParents = new Set<number>();
    matches
      .filter((g) => g.suggestedAction !== "MissingMobile")
      .forEach((g) => {
        const choice = choices[g.admissionGuardianId];
        if (choice === null || choice === undefined) {
          problems.push(
            `Choose an existing parent or "Create new parent" for ${g.name}.`,
          );
          return;
        }
        if (choice === "new") {
          decisions.push({
            admissionGuardianId: g.admissionGuardianId,
            action: "CreateNew",
            parentId: null,
          });
        } else {
          if (usedParents.has(choice))
            problems.push(
              "The same parent cannot be linked to two guardians of this student.",
            );
          usedParents.add(choice);
          decisions.push({
            admissionGuardianId: g.admissionGuardianId,
            action: "UseExisting",
            parentId: choice,
          });
        }
      });

    if (problems.length > 0) {
      setErrors(problems);
      return;
    }

    setIsSubmitting(true);
    try {
      await onSubmit(admission.id, { ...form, guardians: decisions });
      onClose();
    } catch (err) {
      setErrors(
        getApiErrors(
          err,
          "Could not enroll this student. The application may not be in Admitted status.",
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
          <h2>🎓 Enroll to Class</h2>
          <button className="modal__close" onClick={onClose}>
            ✕
          </button>
        </div>
        <div className="modal__body">
          <div className="modal__note" style={{ marginTop: 0 }}>
            {admission.firstName} {admission.lastName} · {admission.regNo}
          </div>

          <div className="modal__section-title">Class &amp; Identity</div>
          <div className="modal__grid">
            <div className="field">
              <label>
                Allotted Class Section<span className="required">*</span>
              </label>
              <select
                value={form.allottedClassSectionId || ""}
                onChange={(e) =>
                  setForm((f) => ({
                    ...f,
                    allottedClassSectionId: Number(e.target.value),
                  }))
                }
              >
                <option value="">Select a class...</option>
                {classSections
                  .filter((c) => c.status === "Active")
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
                Admission Number<span className="required">*</span>
              </label>
              <input
                value={form.admissionNumber}
                onChange={(e) =>
                  setForm((f) => ({ ...f, admissionNumber: e.target.value }))
                }
              />
            </div>
            <div className="field">
              <label>Admission Date</label>
              <input
                type="date"
                value={form.admissionDate}
                onChange={(e) =>
                  setForm((f) => ({ ...f, admissionDate: e.target.value }))
                }
              />
            </div>
            <div className="field">
              <label>Entry Point</label>
              <input
                value={form.entryPoint ?? ""}
                onChange={(e) =>
                  setForm((f) => ({ ...f, entryPoint: e.target.value }))
                }
              />
            </div>
          </div>

          <div className="modal__section-title">Transport</div>
          <div className="field">
            <label>
              <input
                type="checkbox"
                checked={form.transportRequired}
                onChange={(e) =>
                  setForm((f) => ({
                    ...f,
                    transportRequired: e.target.checked,
                  }))
                }
                style={{ marginRight: 8 }}
              />
              Transport required
            </label>
          </div>

          <div className="modal__section-title">Parents / Guardians</div>
          <div className="modal__note" style={{ marginTop: 0 }}>
            Parents are matched by mobile number. Confirm for each guardian
            whether this is a parent already in the system or a new one — the
            system never links parents without your confirmation.
          </div>
          {isLoadingMatches && <div className="empty-state">Checking…</div>}
          {!isLoadingMatches && matches.length === 0 && (
            <div
              className="modal__note"
              style={{ background: "var(--red-bg)", color: "var(--red)" }}
            >
              This application has no guardians. Edit the registration and add
              at least one guardian with a mobile number.
            </div>
          )}
          {matches.map((g) => {
            const id = g.admissionGuardianId;
            const extras = extraCandidates[id] ?? [];
            return (
              <div
                key={id}
                style={{
                  border: "1px solid var(--border, #e5e7eb)",
                  borderRadius: 8,
                  padding: 12,
                  marginBottom: 10,
                }}
              >
                <div style={{ fontWeight: 600, marginBottom: 6 }}>
                  {g.relationType}: {g.name}{" "}
                  <span style={{ fontWeight: 400, color: "var(--muted)" }}>
                    · {g.mobile ?? "no mobile"}
                  </span>
                </div>

                {g.suggestedAction === "MissingMobile" ? (
                  <div style={{ color: "var(--red)", fontSize: 13 }}>
                    A mobile number is required. Edit the application and add
                    one before enrolling.
                  </div>
                ) : (
                  <>
                    {g.suggestedAction === "MustChoose" && (
                      <div
                        style={{
                          fontSize: 12.5,
                          color: "var(--amber)",
                          marginBottom: 6,
                        }}
                      >
                        More than one possible match — please choose carefully.
                      </div>
                    )}
                    {g.candidates.length === 0 && extras.length === 0 && (
                      <div
                        style={{
                          fontSize: 12.5,
                          color: "var(--muted)",
                          marginBottom: 6,
                        }}
                      >
                        No existing parent has this mobile number.
                      </div>
                    )}
                    {g.candidates.map((c) => (
                      <label
                        key={c.parentId}
                        style={{ display: "block", marginBottom: 4 }}
                      >
                        <input
                          type="radio"
                          name={`guardian-${id}`}
                          checked={choices[id] === c.parentId}
                          onChange={() =>
                            setChoices((x) => ({ ...x, [id]: c.parentId }))
                          }
                          style={{ marginRight: 8 }}
                        />
                        Use existing: <b>{c.name}</b> · {c.mobile}
                        {c.linkedChildren.length > 0 && (
                          <span style={{ color: "var(--muted)" }}>
                            {" "}
                            · children: {c.linkedChildren.join(", ")}
                          </span>
                        )}
                      </label>
                    ))}
                    {extras.map((p) => (
                      <label
                        key={p.id}
                        style={{ display: "block", marginBottom: 4 }}
                      >
                        <input
                          type="radio"
                          name={`guardian-${id}`}
                          checked={choices[id] === p.id}
                          onChange={() =>
                            setChoices((x) => ({ ...x, [id]: p.id }))
                          }
                          style={{ marginRight: 8 }}
                        />
                        Use existing: <b>{p.name}</b> · {p.mobile}
                      </label>
                    ))}
                    <label style={{ display: "block", marginBottom: 4 }}>
                      <input
                        type="radio"
                        name={`guardian-${id}`}
                        checked={choices[id] === "new"}
                        onChange={() =>
                          setChoices((x) => ({ ...x, [id]: "new" }))
                        }
                        style={{ marginRight: 8 }}
                      />
                      Create new parent
                    </label>

                    {searchOpenFor === id ? (
                      <div style={{ marginTop: 6 }}>
                        <input
                          autoFocus
                          placeholder="Search by name or mobile (2+ characters)"
                          value={searchText}
                          onChange={(e) => setSearchText(e.target.value)}
                        />
                        {searchResults.map((p) => (
                          <div
                            key={p.id}
                            style={{ padding: "4px 0", fontSize: 13 }}
                          >
                            <button
                              type="button"
                              className="btn btn--secondary btn--sm"
                              onClick={() => pickFromSearch(id, p)}
                            >
                              Select
                            </button>{" "}
                            {p.name} · {p.mobile}
                          </div>
                        ))}
                      </div>
                    ) : (
                      <button
                        type="button"
                        className="btn btn--secondary btn--sm"
                        style={{ marginTop: 6 }}
                        onClick={() => openSearch(id)}
                      >
                        Search another parent…
                      </button>
                    )}
                  </>
                )}
              </div>
            );
          })}

          <div className="modal__section-title">Additional Student Profile</div>
          <div className="modal__note" style={{ marginTop: 0 }}>
            Enrolling creates the student record right away — these fields
            aren't captured during admission, so add them now if known.
          </div>
          <div className="modal__grid">
            <div className="field">
              <label>Nationality</label>
              <input
                value={form.nationality ?? ""}
                onChange={(e) => field("nationality", e.target.value)}
              />
            </div>
            <div className="field">
              <label>Curriculum Track</label>
              <input
                value={form.curriculumTrack ?? ""}
                onChange={(e) => field("curriculumTrack", e.target.value)}
              />
            </div>
            <div className="field">
              <label>English Proficiency</label>
              <input
                value={form.englishProficiency ?? ""}
                onChange={(e) => field("englishProficiency", e.target.value)}
              />
            </div>
            <div className="field">
              <label>EAL Code</label>
              <input
                value={form.ealCode ?? ""}
                onChange={(e) => field("ealCode", e.target.value)}
              />
            </div>
            <div className="field">
              <label>House</label>
              <input
                value={form.house ?? ""}
                onChange={(e) => field("house", e.target.value)}
              />
            </div>
            <div className="field">
              <label>Allergies</label>
              <input
                value={form.allergies ?? ""}
                onChange={(e) => field("allergies", e.target.value)}
              />
            </div>
          </div>

          <div className="modal__note">
            This enrolls the student record but does not create a portal login —
            that stays a separate, manual step.
          </div>
        </div>
        <div className="modal__footer">
          <button
            className="btn btn--primary"
            onClick={handleSubmit}
            disabled={isSubmitting || isLoadingMatches || hasMissingMobile}
          >
            {isSubmitting ? "Enrolling…" : "Enroll Student"}
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
