import { useEffect, useState } from "react";
import { parentService } from "../services/parentService";
import { admissionService } from "@/features/admissions/services/admissionService";
import type { ParentResponse, RelationType } from "../types/parent.types";
import type { StudentResponse } from "@/features/students/types/student.types";
import type { AdmissionResponse } from "@/features/admissions/types/admission.types";

interface Props {
  student: StudentResponse | null;
  onClose: () => void;
  onLinked: () => void;
}

type Mode = "existing" | "new";

const emptyNewParent = { name: "", mobile: "", email: "" };

export function GuardianLinkModal({ student, onClose, onLinked }: Props) {
  const [mode, setMode] = useState<Mode>("existing");
  const [allParents, setAllParents] = useState<ParentResponse[]>([]);
  const [selectedParentId, setSelectedParentId] = useState(0);
  const [newParent, setNewParent] = useState(emptyNewParent);
  const [relationType, setRelationType] = useState<RelationType>("Father");
  const [isPrimaryContact, setIsPrimaryContact] = useState(false);
  const [admission, setAdmission] = useState<AdmissionResponse | null>(null);
  const [isSubmitting, setIsSubmitting] = useState(false);
  const [error, setError] = useState<string | null>(null);

  useEffect(() => {
    if (!student) return;
    parentService.getAll().then(setAllParents);
    setMode("existing");
    setSelectedParentId(0);
    setNewParent(emptyNewParent);
    setRelationType("Father");
    setIsPrimaryContact(false);
    setError(null);

    if (student.admissionId) {
      admissionService.getById(student.admissionId).then(setAdmission);
    } else {
      setAdmission(null);
    }
  }, [student]);

  if (!student) return null;

  const prefillFrom = (which: "father" | "mother" | "guardian") => {
    if (!admission) return;
    if (which === "father" && admission.fatherName) {
      setNewParent({
        name: admission.fatherName,
        mobile: admission.fatherMobile ?? "",
        email: "",
      });
      setRelationType("Father");
    } else if (which === "mother" && admission.motherName) {
      setNewParent({
        name: admission.motherName,
        mobile: admission.motherMobile ?? "",
        email: "",
      });
      setRelationType("Mother");
    } else if (which === "guardian" && admission.guardianName) {
      setNewParent({
        name: admission.guardianName,
        mobile: admission.guardianMobile ?? "",
        email: "",
      });
      setRelationType("Guardian");
    }
    setMode("new");
  };

  const handleSubmit = async () => {
    setError(null);

    if (mode === "existing" && !selectedParentId) {
      setError("Select a parent to link.");
      return;
    }
    if (mode === "new" && (!newParent.name || !newParent.mobile)) {
      setError("Name and mobile are required for a new parent.");
      return;
    }

    setIsSubmitting(true);
    try {
      let parentId = selectedParentId;
      if (mode === "new") {
        const created = await parentService.create({
          name: newParent.name,
          mobile: newParent.mobile,
          email: newParent.email || null,
          occupation: null,
          nationality: null,
          countryOfResidence: null,
          timezone: null,
          preferredLanguage: null,
          preferredContactMethod: null,
          whatsapp: null,
          emergencyOnly: false,
          notifyAttendance: true,
          notifyExams: true,
          notifyFees: true,
          notifyNotices: true,
          notifyDiscipline: true,
          employer: null,
          jobTitle: null,
          workEmail: null,
          workPhone: null,
          billingContact: false,
          addressLine: null,
          city: null,
          state: null,
          pincode: null,
        });
        parentId = created.id;
      }

      await parentService.linkGuardian(student.id, {
        parentId,
        relationType,
        isPrimaryContact,
      });
      onLinked();
      onClose();
    } catch {
      setError(
        "Could not link this guardian. They may already be linked to this student.",
      );
    } finally {
      setIsSubmitting(false);
    }
  };

  return (
    <div className="modal-backdrop">
      <div className="modal">
        <div className="modal__header">
          <h2>🔗 Link Guardian</h2>
          <button className="modal__close" onClick={onClose}>
            ✕
          </button>
        </div>
        <div className="modal__body">
          <div className="modal__note" style={{ marginTop: 0 }}>
            {student.firstName} {student.lastName} · {student.admNo}
          </div>

          {admission &&
            (admission.fatherName ||
              admission.motherName ||
              admission.guardianName) && (
              <>
                <div className="modal__section-title">
                  Pre-fill from Admission
                </div>
                <div
                  style={{
                    display: "flex",
                    gap: 8,
                    flexWrap: "wrap",
                    marginBottom: 8,
                  }}
                >
                  {admission.fatherName && (
                    <button
                      className="btn btn--secondary btn--sm"
                      onClick={() => prefillFrom("father")}
                    >
                      Father: {admission.fatherName}
                    </button>
                  )}
                  {admission.motherName && (
                    <button
                      className="btn btn--secondary btn--sm"
                      onClick={() => prefillFrom("mother")}
                    >
                      Mother: {admission.motherName}
                    </button>
                  )}
                  {admission.guardianName && (
                    <button
                      className="btn btn--secondary btn--sm"
                      onClick={() => prefillFrom("guardian")}
                    >
                      Guardian: {admission.guardianName}
                    </button>
                  )}
                </div>
              </>
            )}

          <div className="modal__section-title">Guardian</div>
          <div style={{ display: "flex", gap: 16, marginBottom: 14 }}>
            <label>
              <input
                type="radio"
                checked={mode === "existing"}
                onChange={() => setMode("existing")}
              />{" "}
              Link existing
            </label>
            <label>
              <input
                type="radio"
                checked={mode === "new"}
                onChange={() => setMode("new")}
              />{" "}
              Create new
            </label>
          </div>

          {mode === "existing" ? (
            <div className="field">
              <label>Parent</label>
              <select
                value={selectedParentId || ""}
                onChange={(e) => setSelectedParentId(Number(e.target.value))}
              >
                <option value="">Select a parent...</option>
                {allParents.map((p) => (
                  <option key={p.id} value={p.id}>
                    {p.name} — {p.mobile}
                  </option>
                ))}
              </select>
            </div>
          ) : (
            <div className="modal__grid">
              <div className="field">
                <label>
                  Name<span className="required">*</span>
                </label>
                <input
                  value={newParent.name}
                  onChange={(e) =>
                    setNewParent((f) => ({ ...f, name: e.target.value }))
                  }
                />
              </div>
              <div className="field">
                <label>
                  Mobile<span className="required">*</span>
                </label>
                <input
                  value={newParent.mobile}
                  onChange={(e) =>
                    setNewParent((f) => ({ ...f, mobile: e.target.value }))
                  }
                />
              </div>
              <div className="field">
                <label>Email</label>
                <input
                  value={newParent.email}
                  onChange={(e) =>
                    setNewParent((f) => ({ ...f, email: e.target.value }))
                  }
                />
              </div>
            </div>
          )}

          <div className="modal__section-title">Relationship</div>
          <div className="modal__grid">
            <div className="field">
              <label>Relation Type</label>
              <select
                value={relationType}
                onChange={(e) =>
                  setRelationType(e.target.value as RelationType)
                }
              >
                <option value="Father">Father</option>
                <option value="Mother">Mother</option>
                <option value="Guardian">Guardian</option>
              </select>
            </div>
            <div className="field">
              <label>
                <input
                  type="checkbox"
                  checked={isPrimaryContact}
                  onChange={(e) => setIsPrimaryContact(e.target.checked)}
                  style={{ marginRight: 8 }}
                />
                Primary contact
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
            {isSubmitting ? "Linking…" : "Link Guardian"}
          </button>
          <button className="btn btn--secondary" onClick={onClose}>
            Cancel
          </button>
        </div>
      </div>
    </div>
  );
}
