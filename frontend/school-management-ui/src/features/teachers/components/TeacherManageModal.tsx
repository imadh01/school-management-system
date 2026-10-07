import { useEffect, useState } from "react";
import { ValidationModal } from "@/components/ValidationModal";
import { classSectionService } from "@/features/class-sections/services/classSectionService";
import type { ClassSectionResponse } from "@/features/class-sections/types/classSection.types";
import { subjectService } from "@/features/subjects/services/subjectService";
import type { SubjectResponse } from "@/features/subjects/types/subject.types";
import { getApiErrors } from "@/utils/apiError";
import { teacherService } from "../services/teacherService";
import type {
  TeacherAssignment,
  TeacherResponse,
} from "../types/teacher.types";

interface Props {
  teacher: TeacherResponse | null;
  onClose: () => void;
  onChanged: () => void;
}

const sectionTitle: React.CSSProperties = {
  fontSize: 12,
  fontWeight: 700,
  textTransform: "uppercase",
  color: "var(--muted)",
  margin: "18px 0 8px",
};

export function TeacherManageModal({ teacher, onClose, onChanged }: Props) {
  const [assignments, setAssignments] = useState<TeacherAssignment[]>([]);
  const [classSections, setClassSections] = useState<ClassSectionResponse[]>(
    [],
  );
  const [subjects, setSubjects] = useState<SubjectResponse[]>([]);
  const [isLoading, setIsLoading] = useState(false);
  const [isSaving, setIsSaving] = useState(false);
  const [errors, setErrors] = useState<string[] | null>(null);

  const [classId, setClassId] = useState<number | "">("");
  const [selected, setSelected] = useState<number[]>([]);
  const [makeClassTeacher, setMakeClassTeacher] = useState(false);

  const teacherId = teacher?.id;

  const loadReference = async () => {
    const [sections, subjectList] = await Promise.all([
      classSectionService.getAll(),
      subjectService.getAll(),
    ]);
    setClassSections(sections);
    setSubjects(subjectList);
  };

  useEffect(() => {
    if (teacherId === undefined) return;
    setClassId("");
    setSelected([]);
    setMakeClassTeacher(false);
    setIsLoading(true);
    Promise.all([teacherService.getAssignments(teacherId), loadReference()])
      .then(([a]) => setAssignments(a))
      .catch((err) =>
        setErrors(getApiErrors(err, "Could not load assignments.")),
      )
      .finally(() => setIsLoading(false));
  }, [teacherId]);

  if (!teacher) return null;

  // Runs a mutation, shows the refreshed assignments, refreshes the pickers
  // (subject owners / class teachers changed) and the parent list counts.
  const run = async (action: () => Promise<TeacherAssignment[]>) => {
    setIsSaving(true);
    try {
      setAssignments(await action());
      await loadReference();
      onChanged();
      return true;
    } catch (err) {
      setErrors(getApiErrors(err, "Could not update assignments."));
      return false;
    } finally {
      setIsSaving(false);
    }
  };

  const section = classSections.find((c) => c.id === classId);
  const mine = assignments.find((a) => a.classSectionId === classId);
  const iAmClassTeacher = mine?.isClassTeacher ?? false;
  const otherClassTeacher =
    section?.classTeacherId != null && section.classTeacherId !== teacher.id
      ? (section.classTeacherName ?? "another teacher")
      : null;

  // Subjects of the chosen class that this teacher doesn't teach yet.
  const pickable = subjects.filter(
    (s) =>
      s.classSectionId === classId &&
      s.status === "Active" &&
      s.teacherId !== teacher.id,
  );

  const toggleSubject = (id: number) =>
    setSelected((cur) =>
      cur.includes(id) ? cur.filter((x) => x !== id) : [...cur, id],
    );

  const handleAdd = async () => {
    if (classId === "" || selected.length === 0) return;
    const ok = await run(() =>
      teacherService.assignSubjects(teacher.id, {
        classSectionId: classId,
        subjectIds: selected,
        makeClassTeacher:
          makeClassTeacher && !iAmClassTeacher && !otherClassTeacher,
      }),
    );
    if (ok) {
      setSelected([]);
      setMakeClassTeacher(false);
    }
  };

  const totalSubjects = assignments.reduce((n, a) => n + a.subjects.length, 0);
  const classTeacherOf = assignments.filter((a) => a.isClassTeacher).length;

  const statTile = (value: number, label: string, color: string) => (
    <div
      style={{
        flex: 1,
        border: "1px solid var(--border)",
        borderRadius: 8,
        padding: "10px 14px",
      }}
    >
      <div style={{ fontSize: 22, fontWeight: 700, color }}>{value}</div>
      <div style={{ fontSize: 11.5, color: "var(--muted)" }}>{label}</div>
    </div>
  );

  return (
    <>
      <div className="modal-backdrop open">
        <div className="modal modal--wide">
          <div className="modal__header">
            <h2>📋 Teacher Profile &amp; Assignments</h2>
            <button className="modal__close" onClick={onClose}>
              ✕
            </button>
          </div>
          <div className="modal__body">
            <div style={{ display: "flex", alignItems: "center", gap: 12 }}>
              <span
                className="avatar-circle"
                style={{ width: 44, height: 44, fontSize: 16, marginRight: 0 }}
              >
                {teacher.name.charAt(0).toUpperCase()}
              </span>
              <div>
                <strong>{teacher.name}</strong>{" "}
                <span
                  className={`badge ${teacher.status === "Active" ? "badge--active" : teacher.status === "Suspended" ? "badge--danger" : "badge--inactive"}`}
                >
                  {teacher.status}
                </span>
                <br />
                <span style={{ fontSize: 12, color: "var(--muted)" }}>
                  {teacher.username} · {teacher.email}
                  {teacher.phone ? ` · ${teacher.phone}` : ""} ·{" "}
                  {teacher.specialization ?? "—"}
                </span>
              </div>
            </div>

            <div style={{ display: "flex", gap: 10, marginTop: 16 }}>
              {statTile(assignments.length, "Classes", "var(--blue)")}
              {statTile(totalSubjects, "Subjects", "var(--purple)")}
              {statTile(classTeacherOf, "Class Teacher Of", "var(--amber)")}
            </div>

            <div style={sectionTitle}>
              Current Assignments ({assignments.length})
            </div>
            {isLoading ? (
              <div className="empty-state">Loading…</div>
            ) : assignments.length === 0 ? (
              <div className="empty-state">
                No assignments yet — add one below.
              </div>
            ) : (
              assignments.map((a) => (
                <div
                  key={a.classSectionId}
                  style={{
                    display: "flex",
                    gap: 12,
                    alignItems: "flex-start",
                    border: "1px solid var(--border)",
                    borderRadius: 8,
                    padding: "10px 14px",
                    marginBottom: 10,
                  }}
                >
                  <div style={{ flex: 1, fontSize: 13 }}>
                    <strong>{a.classSectionName}</strong>{" "}
                    {a.isClassTeacher && (
                      <span className="badge badge--warn">★ Class Teacher</span>
                    )}
                    <div
                      style={{
                        marginTop: 6,
                        display: "flex",
                        flexWrap: "wrap",
                        gap: 6,
                      }}
                    >
                      {a.subjects.map((s) => (
                        <span
                          key={s.subjectId}
                          className="badge badge--info"
                          style={{
                            display: "inline-flex",
                            alignItems: "center",
                            gap: 6,
                          }}
                        >
                          {s.name} {s.code}
                          <button
                            type="button"
                            title="Remove this subject"
                            disabled={isSaving}
                            style={{
                              border: 0,
                              background: "none",
                              cursor: "pointer",
                              color: "inherit",
                              padding: 0,
                            }}
                            onClick={() =>
                              run(() =>
                                teacherService.unassignSubject(
                                  teacher.id,
                                  s.subjectId,
                                ),
                              )
                            }
                          >
                            ✕
                          </button>
                        </span>
                      ))}
                    </div>
                  </div>
                  <button
                    className="btn btn--secondary btn--sm"
                    disabled={isSaving}
                    onClick={() =>
                      run(() =>
                        a.isClassTeacher
                          ? teacherService.clearClassTeacher(
                              teacher.id,
                              a.classSectionId,
                            )
                          : teacherService.setClassTeacher(
                              teacher.id,
                              a.classSectionId,
                            ),
                      )
                    }
                  >
                    {a.isClassTeacher ? "Unset" : "Make Class Teacher"}
                  </button>
                </div>
              ))
            )}

            <div style={sectionTitle}>Add More Subjects</div>
            <div className="field">
              <label>Class</label>
              <select
                value={classId}
                onChange={(e) => {
                  setClassId(e.target.value ? Number(e.target.value) : "");
                  setSelected([]);
                  setMakeClassTeacher(false);
                }}
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

            {classId !== "" && (
              <>
                <div className="field" style={{ marginTop: 10 }}>
                  <label>Subjects in this Class ({pickable.length})</label>
                  <div className="tag-group">
                    {pickable.length === 0 ? (
                      <span style={{ fontSize: 12.5, color: "var(--muted)" }}>
                        No more subjects to assign in this class.
                      </span>
                    ) : (
                      pickable.map((s) => {
                        const taken = s.teacherId !== null;
                        const isSelected = selected.includes(s.id);
                        return (
                          <button
                            key={s.id}
                            type="button"
                            disabled={taken}
                            title={
                              taken
                                ? `Already taught by ${s.teacherName ?? "another teacher"}`
                                : undefined
                            }
                            className={`tag-btn ${isSelected ? "selected" : ""}`}
                            style={
                              taken
                                ? { opacity: 0.5, cursor: "not-allowed" }
                                : undefined
                            }
                            onClick={() => toggleSubject(s.id)}
                          >
                            {isSelected ? "✓ " : ""}
                            {s.name} {s.code}
                            {taken ? ` — ${s.teacherName ?? "assigned"}` : ""}
                          </button>
                        );
                      })
                    )}
                  </div>
                </div>

                <div className="checkbox-row">
                  <input
                    type="checkbox"
                    id="mt-classteacher"
                    checked={makeClassTeacher || iAmClassTeacher}
                    disabled={iAmClassTeacher || !!otherClassTeacher}
                    onChange={(e) => setMakeClassTeacher(e.target.checked)}
                  />
                  <label htmlFor="mt-classteacher">
                    Assign as Class Teacher (Homeroom) of this class
                  </label>
                </div>
                <div className="field-note">
                  {iAmClassTeacher
                    ? "This teacher is already the class teacher here."
                    : otherClassTeacher
                      ? `${otherClassTeacher} is the class teacher — remove them first.`
                      : "One teacher per class can be the class teacher."}
                </div>

                <div
                  style={{
                    display: "flex",
                    alignItems: "center",
                    justifyContent: "space-between",
                  }}
                >
                  <span style={{ fontSize: 12.5, color: "var(--muted)" }}>
                    {selected.length} subject(s) selected
                  </span>
                  <button
                    className="btn btn--primary"
                    disabled={selected.length === 0 || isSaving}
                    onClick={handleAdd}
                  >
                    + Add {selected.length} Subject(s)
                  </button>
                </div>
              </>
            )}
          </div>
          <div className="modal__footer" style={{ justifyContent: "flex-end" }}>
            <button className="btn btn--secondary" onClick={onClose}>
              Close
            </button>
          </div>
        </div>
      </div>
      <ValidationModal errors={errors} onClose={() => setErrors(null)} />
    </>
  );
}
