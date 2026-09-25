import { useEffect, useState } from "react";
import { parentService } from "../services/parentService";
import { studentService } from "@/features/students/services/studentService";
import type {
  ParentResponse,
  LinkedStudentResponse,
  RelationType,
} from "../types/parent.types";
import type { StudentResponse } from "@/features/students/types/student.types";

interface Props {
  isOpen: boolean;
  onClose: () => void;
  parent: ParentResponse | null;
  onChanged?: () => void;
}

function initials(name: string) {
  return (name || "?").trim().charAt(0).toUpperCase();
}

export function LinkedChildrenModal({
  isOpen,
  onClose,
  parent,
  onChanged,
}: Props) {
  const [linked, setLinked] = useState<LinkedStudentResponse[]>([]);
  const [allStudents, setAllStudents] = useState<StudentResponse[]>([]);
  const [isLoading, setIsLoading] = useState(false);
  const [selectedStudentId, setSelectedStudentId] = useState("");
  const [relation, setRelation] = useState<RelationType>("Father");
  const [markPrimary, setMarkPrimary] = useState(false);
  const [error, setError] = useState<string | null>(null);

  const load = async () => {
    if (!parent) return;
    setIsLoading(true);
    setError(null);
    try {
      const [linkedRes, studentsRes] = await Promise.all([
        parentService.getLinkedStudents(parent.id),
        allStudents.length
          ? Promise.resolve(allStudents)
          : studentService.getAll(),
      ]);
      setLinked(linkedRes);
      setAllStudents(studentsRes);
    } finally {
      setIsLoading(false);
    }
  };

  useEffect(() => {
    if (isOpen && parent) {
      setSelectedStudentId("");
      setMarkPrimary(false);
      load();
    }
    // eslint-disable-next-line react-hooks/exhaustive-deps
  }, [isOpen, parent?.id]);

  if (!isOpen || !parent) return null;

  const linkedIds = new Set(linked.map((c) => c.studentId));
  const available = allStudents.filter((s) => !linkedIds.has(s.id));

  const handleLink = async () => {
    if (!selectedStudentId) {
      setError("Please pick a student to link.");
      return;
    }
    setError(null);
    try {
      await parentService.linkStudent(parent.id, {
        studentId: Number(selectedStudentId),
        relationType: relation,
        isPrimaryContact: markPrimary,
      });
      setSelectedStudentId("");
      setMarkPrimary(false);
      await load();
      onChanged?.();
    } catch {
      setError("Could not link this student.");
    }
  };

  const handleUnlink = async (studentId: number, studentName: string) => {
    if (!confirm(`Unlink ${studentName} from ${parent.name}?`)) return;
    await parentService.unlinkStudent(parent.id, studentId);
    await load();
    onChanged?.();
  };

  const handleMakePrimary = async (studentId: number) => {
    await parentService.setPrimaryStudent(parent.id, studentId);
    await load();
    onChanged?.();
  };

  return (
    <div className="modal-backdrop open">
      <div className="modal">
        <div className="modal__header">
          <h2>👪 Linked Children</h2>
          <button className="modal__close" onClick={onClose}>
            ✕
          </button>
        </div>
        <div className="modal__body">
          <div className="modal-context-card">
            <span className="avatar-circle avatar-circle--lg">
              {initials(parent.name)}
            </span>
            <div>
              <strong>{parent.name}</strong>
              <br />
              📞 {parent.mobile}
            </div>
          </div>

          {isLoading ? (
            <div className="empty-state">Loading…</div>
          ) : (
            <>
              <div className="modal__section-title">
                Currently Linked ({linked.length})
              </div>
              {linked.length === 0 ? (
                <div className="empty-state">No children linked yet.</div>
              ) : (
                linked.map((c) => (
                  <div className="linked-child-row" key={c.studentId}>
                    <span
                      className="avatar-circle"
                      style={{
                        background: "var(--blue-bg)",
                        color: "var(--blue)",
                      }}
                    >
                      S
                    </span>
                    <div className="linked-child-row__info">
                      <strong>{c.studentName}</strong>
                      <br />
                      <span className="badge badge--gray">{c.admNo}</span>{" "}
                      {c.classSectionName} · Roll {c.rollNumber} ·{" "}
                      <span className="badge badge--info">
                        {c.relationType}
                      </span>
                    </div>
                    {c.isPrimaryContact ? (
                      <span className="badge badge--warn">★ Primary</span>
                    ) : (
                      <button
                        className="btn btn--secondary btn--sm"
                        onClick={() => handleMakePrimary(c.studentId)}
                      >
                        Make Primary
                      </button>
                    )}
                    <button
                      className="btn--icon danger"
                      title="Unlink"
                      onClick={() => handleUnlink(c.studentId, c.studentName)}
                    >
                      ✕
                    </button>
                  </div>
                ))
              )}

              <div className="modal__section-title">Link Another Child</div>
              {available.length === 0 ? (
                <div className="empty-state">
                  All students are already linked to this parent.
                </div>
              ) : (
                <>
                  <div className="field" style={{ marginBottom: 12 }}>
                    <label>Student</label>
                    <select
                      value={selectedStudentId}
                      onChange={(e) => setSelectedStudentId(e.target.value)}
                    >
                      <option value="">Pick a student to link...</option>
                      {available.map((s) => (
                        <option key={s.id} value={s.id}>
                          {s.firstName} {s.lastName} · {s.admNo} ·{" "}
                          {s.classSectionName}
                        </option>
                      ))}
                    </select>
                  </div>
                  <div className="field" style={{ marginBottom: 12 }}>
                    <label>Relation</label>
                    <select
                      value={relation}
                      onChange={(e) =>
                        setRelation(e.target.value as RelationType)
                      }
                    >
                      <option>Father</option>
                      <option>Mother</option>
                      <option>Guardian</option>
                    </select>
                  </div>
                  <div
                    className="checkbox-row"
                    style={{ marginTop: 0, marginBottom: 14 }}
                  >
                    <input
                      type="checkbox"
                      id="link-primary-check"
                      checked={markPrimary}
                      onChange={(e) => setMarkPrimary(e.target.checked)}
                    />
                    <label htmlFor="link-primary-check">
                      Mark as primary contact
                    </label>
                  </div>
                  <button className="btn btn--primary" onClick={handleLink}>
                    Link Student
                  </button>
                </>
              )}

              {error && (
                <div
                  className="modal__note"
                  style={{
                    background: "var(--red-bg)",
                    color: "var(--red)",
                    marginTop: 14,
                  }}
                >
                  {error}
                </div>
              )}
            </>
          )}
        </div>
        <div className="modal__footer">
          <button className="btn btn--secondary" onClick={onClose}>
            Close
          </button>
        </div>
      </div>
    </div>
  );
}
