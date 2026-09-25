import { useEffect, useMemo, useState } from "react";
import { studentService } from "../services/studentService";
import { classSectionService } from "@/features/class-sections/services/classSectionService";
import type { StudentResponse } from "../types/student.types";
import type { ClassSectionResponse } from "@/features/class-sections/types/classSection.types";
import { StudentModal } from "../components/StudentModal";
import { GuardianLinkModal } from "@/features/parents/components/GuardianLinkModal";

const STATUS_BADGE_CLASS: Record<string, string> = {
  Active: "badge--active",
  Inactive: "badge--inactive",
  Left: "badge--danger",
};

export function StudentsPage() {
  const [students, setStudents] = useState<StudentResponse[]>([]);
  const [classSections, setClassSections] = useState<ClassSectionResponse[]>(
    [],
  );
  const [search, setSearch] = useState("");
  const [isLoading, setIsLoading] = useState(true);
  const [isModalOpen, setIsModalOpen] = useState(false);
  const [guardianTarget, setGuardianTarget] = useState<StudentResponse | null>(
    null,
  );

  const loadData = async () => {
    setIsLoading(true);
    const [studentsData, classSectionsData] = await Promise.all([
      studentService.getAll(),
      classSectionService.getAll(),
    ]);
    setStudents(studentsData);
    setClassSections(classSectionsData);
    setIsLoading(false);
  };

  useEffect(() => {
    loadData();
  }, []);

  const filtered = useMemo(() => {
    if (!search) return students;
    const q = search.toLowerCase();
    return students.filter((s) =>
      `${s.firstName} ${s.lastName} ${s.admNo} ${s.rollNumber}`
        .toLowerCase()
        .includes(q),
    );
  }, [students, search]);

  return (
    <div>
      <div
        style={{
          display: "flex",
          justifyContent: "space-between",
          alignItems: "center",
          marginBottom: 16,
        }}
      >
        <div />
        <button
          className="btn btn--primary"
          onClick={() => setIsModalOpen(true)}
        >
          + Add Student
        </button>
      </div>

      <div className="filters">
        <div className="field">
          <label>Search</label>
          <input
            placeholder="Name, adm #, roll number..."
            value={search}
            onChange={(e) => setSearch(e.target.value)}
          />
        </div>
        <button
          className="btn btn--secondary filters__clear"
          onClick={() => setSearch("")}
        >
          Clear
        </button>
      </div>

      {isLoading ? (
        <div className="empty-state">Loading…</div>
      ) : filtered.length === 0 ? (
        <div className="empty-state">No students found.</div>
      ) : (
        <table>
          <thead>
            <tr>
              <th>Adm #</th>
              <th>Student</th>
              <th>Nationality</th>
              <th>Curriculum</th>
              <th>EAL</th>
              <th>Class</th>
              <th>Status</th>
              <th>House</th>
              <th>Allergies</th>
              <th>Actions</th>
            </tr>
          </thead>
          <tbody>
            {filtered.map((s) => (
              <tr key={s.id}>
                <td>
                  {s.admNo}
                  {s.admissionRegNo && (
                    <span title={`From admission ${s.admissionRegNo}`}>
                      {" "}
                      🔗
                    </span>
                  )}
                </td>
                <td>
                  {s.firstName} {s.lastName}
                </td>
                <td>{s.nationality ?? "—"}</td>
                <td>{s.curriculumTrack ?? "—"}</td>
                <td>{s.ealCode ?? "—"}</td>
                <td>{s.classSectionName}</td>
                <td>
                  <span
                    className={`badge ${STATUS_BADGE_CLASS[s.status] ?? "badge--inactive"}`}
                  >
                    {s.status}
                  </span>
                </td>
                <td>{s.house ?? "—"}</td>
                <td>{s.allergies || "—"}</td>
                <td>
                  <button
                    className="btn--icon"
                    title="Manage Guardians"
                    onClick={() => setGuardianTarget(s)}
                  >
                    👪
                  </button>
                </td>
              </tr>
            ))}
          </tbody>
        </table>
      )}

      <StudentModal
        isOpen={isModalOpen}
        editingStudent={null}
        classSections={classSections}
        onClose={() => setIsModalOpen(false)}
        onSubmit={async (data) => {
          await studentService.create(data);
          await loadData();
        }}
      />

      <GuardianLinkModal
        student={guardianTarget}
        onClose={() => setGuardianTarget(null)}
        onLinked={loadData}
      />
    </div>
  );
}
