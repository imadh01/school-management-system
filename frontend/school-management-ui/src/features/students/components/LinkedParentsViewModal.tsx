import { useEffect, useState } from "react";
import { parentService } from "@/features/parents/services/parentService";
import type { StudentGuardianResponse } from "@/features/parents/types/parent.types";
import type { StudentResponse } from "../types/student.types";

interface Props {
  student: StudentResponse | null;
  onClose: () => void;
}

export function LinkedParentsViewModal({ student, onClose }: Props) {
  const [guardians, setGuardians] = useState<StudentGuardianResponse[]>([]);
  const [isLoading, setIsLoading] = useState(true);

  useEffect(() => {
    if (!student) return;
    setIsLoading(true);
    parentService.getGuardiansForStudent(student.id).then((data) => {
      setGuardians(data);
      setIsLoading(false);
    });
  }, [student]);

  if (!student) return null;

  return (
    <div className="modal-backdrop">
      <div className="modal">
        <div className="modal__header">
          <h2>👪 Linked Parents</h2>
          <button className="modal__close" onClick={onClose}>
            ✕
          </button>
        </div>
        <div className="modal__body">
          <div
            style={{
              display: "flex",
              alignItems: "center",
              gap: 12,
              marginBottom: 16,
            }}
          >
            <span
              className="student-avatar"
              style={{ width: 44, height: 44, fontSize: 16 }}
            >
              {student.firstName.charAt(0)}
            </span>
            <div>
              <strong>
                {student.firstName} {student.lastName}
              </strong>
              <br />
              <span style={{ fontSize: 12, color: "var(--muted)" }}>
                {student.admNo} · {student.classSectionName}
              </span>
            </div>
          </div>

          <div className="info-banner">
            Read-only: manage links from the Parents module.
          </div>

          {isLoading ? (
            <div className="empty-state">Loading…</div>
          ) : guardians.length === 0 ? (
            <div className="empty-state">
              No parents linked to this student yet.
            </div>
          ) : (
            <>
              <div className="modal__section-title">
                Linked Parents ({guardians.length})
              </div>
              {guardians.map((g) => (
                <div
                  key={g.parentId}
                  className="card"
                  style={{ marginBottom: 10, padding: "12px 16px" }}
                >
                  <strong>{g.parentName}</strong>{" "}
                  <span className="badge badge--info">{g.relationType}</span>
                  {g.isPrimaryContact && (
                    <span className="badge badge--warn"> ★ Primary</span>
                  )}
                  <div
                    style={{
                      fontSize: 12.5,
                      color: "var(--muted)",
                      marginTop: 4,
                    }}
                  >
                    {g.parentMobile && <>📞 {g.parentMobile}</>}
                    {g.parentEmail && <> · ✉ {g.parentEmail}</>}
                  </div>
                </div>
              ))}
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
