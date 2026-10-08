import { useEffect, useState } from "react";
import { studentService } from "../services/studentService";
import type {
  StudentEnrollmentResponse,
  StudentSummaryResponse,
} from "../types/student.types";

interface Props {
  student: StudentSummaryResponse | null;
  onClose: () => void;
}

const STATUS_BADGE: Record<string, string> = {
  Active: "badge--green",
  Promoted: "badge--info",
  Repeated: "badge--warn",
  Transferred: "badge--info",
  Left: "badge--danger",
};

export function StudentHistoryModal({ student, onClose }: Props) {
  const [rows, setRows] = useState<StudentEnrollmentResponse[]>([]);
  const [isLoading, setIsLoading] = useState(true);
  const [failed, setFailed] = useState(false);

  useEffect(() => {
    if (!student) return;
    setIsLoading(true);
    setFailed(false);
    studentService
      .getEnrollments(student.id)
      .then(setRows)
      .catch(() => setFailed(true))
      .finally(() => setIsLoading(false));
  }, [student]);

  if (!student) return null;

  return (
    <div className="modal-backdrop">
      <div className="modal modal--wide">
        <div className="modal__header">
          <h2>📚 Academic History</h2>
          <button className="modal__close" onClick={onClose}>
            ✕
          </button>
        </div>
        <div className="modal__body">
          <p style={{ margin: "0 0 12px" }}>
            <strong>
              {student.firstName} {student.lastName}
            </strong>{" "}
            <span style={{ fontSize: 12, color: "var(--muted)" }}>
              {student.admNo}
            </span>
          </p>

          <div className="info-banner">
            Read-only. A new row appears when the student changes class or is
            promoted.
          </div>

          {isLoading ? (
            <div className="empty-state">Loading…</div>
          ) : failed ? (
            <div className="empty-state">Could not load the history.</div>
          ) : rows.length === 0 ? (
            <div className="empty-state">No academic history yet.</div>
          ) : (
            <table>
              <thead>
                <tr>
                  <th>Academic Year</th>
                  <th>Class</th>
                  <th>Roll No</th>
                  <th>From</th>
                  <th>To</th>
                  <th>Outcome</th>
                  <th>Remarks</th>
                </tr>
              </thead>
              <tbody>
                {rows.map((r) => (
                  <tr key={r.id}>
                    <td>{r.academicYearName}</td>
                    <td>{r.classSectionName}</td>
                    <td>{r.rollNumber}</td>
                    <td>{r.startDate}</td>
                    <td>{r.endDate ?? "—"}</td>
                    <td>
                      <span
                        className={`badge ${STATUS_BADGE[r.status] ?? "badge--info"}`}
                      >
                        {r.status}
                      </span>
                    </td>
                    <td>{r.remarks ?? ""}</td>
                  </tr>
                ))}
              </tbody>
            </table>
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
