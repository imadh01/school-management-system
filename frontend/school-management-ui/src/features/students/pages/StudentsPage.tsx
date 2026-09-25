import { useEffect, useMemo, useState } from "react";
import { studentService } from "../services/studentService";
import { classSectionService } from "@/features/class-sections/services/classSectionService";
import type {
  StudentResponse,
  StudentStatus,
  CreateStudentRequest,
} from "../types/student.types";
import type { ClassSectionResponse } from "@/features/class-sections/types/classSection.types";
import { StudentModal } from "../components/StudentModal";
import { NotYetAvailableModal } from "../components/NotYetAvailableModal";
import { LinkedParentsViewModal } from "../components/LinkedParentsViewModal";

const PIPELINE_STEPS: {
  key: StudentStatus | "All";
  label: string;
  activeClass: string;
}[] = [
  { key: "All", label: "All", activeClass: "pipeline__step--dark" },
  { key: "Active", label: "Active", activeClass: "pipeline__step--green" },
  { key: "Inactive", label: "Inactive", activeClass: "" },
  {
    key: "Transferred",
    label: "Transferred",
    activeClass: "pipeline__step--purple",
  },
  {
    key: "Passed Out",
    label: "Passed Out",
    activeClass: "pipeline__step--purple",
  },
  { key: "Suspended", label: "Suspended", activeClass: "pipeline__step--red" },
];

const STATUS_BADGE_CLASS: Record<string, string> = {
  Active: "badge--active",
  Inactive: "badge--inactive",
  Transferred: "badge--purple",
  "Passed Out": "badge--purple",
  Suspended: "badge--danger",
};

function unique(values: (string | null)[]): string[] {
  const seen = new Set<string>();
  const out: string[] = [];
  values.forEach((v) => {
    if (v && !seen.has(v)) {
      seen.add(v);
      out.push(v);
    }
  });
  return out.sort();
}

function toCsv(rows: StudentResponse[]): string {
  const headers = [
    "Adm #",
    "Student",
    "Nationality",
    "Curriculum",
    "EAL",
    "Class",
    "Status",
    "House",
    "Allergies",
  ];
  const lines = rows.map((s) =>
    [
      s.admNo,
      `${s.firstName} ${s.lastName}`,
      s.nationality ?? "",
      s.curriculumTrack ?? "",
      s.ealCode ?? "",
      s.classSectionName,
      s.status,
      s.house ?? "",
      s.allergies ?? "",
    ]
      .map((v) => `"${String(v).replace(/"/g, '""')}"`)
      .join(","),
  );
  return [headers.join(","), ...lines].join("\n");
}

export function StudentsPage() {
  const [students, setStudents] = useState<StudentResponse[]>([]);
  const [classSections, setClassSections] = useState<ClassSectionResponse[]>(
    [],
  );
  const [statusFilter, setStatusFilter] = useState<StudentStatus | "All">(
    "All",
  );
  const [search, setSearch] = useState("");
  const [quickSearch, setQuickSearch] = useState("");
  const [classFilter, setClassFilter] = useState("");
  const [genderFilter, setGenderFilter] = useState("");
  const [curriculumFilter, setCurriculumFilter] = useState("");
  const [ealFilter, setEalFilter] = useState("");
  const [houseFilter, setHouseFilter] = useState("");
  const [hasAllergies, setHasAllergies] = useState(false);
  const [pageSize, setPageSize] = useState(10);
  const [page, setPage] = useState(1);
  const [isLoading, setIsLoading] = useState(true);

  const [isModalOpen, setIsModalOpen] = useState(false);
  const [editingStudent, setEditingStudent] = useState<StudentResponse | null>(
    null,
  );
  const [feeSummaryTarget, setFeeSummaryTarget] =
    useState<StudentResponse | null>(null);
  const [attendanceTarget, setAttendanceTarget] =
    useState<StudentResponse | null>(null);
  const [reportCardTarget, setReportCardTarget] =
    useState<StudentResponse | null>(null);
  const [linkedParentsTarget, setLinkedParentsTarget] =
    useState<StudentResponse | null>(null);

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

  const classNames = useMemo(
    () => unique(students.map((s) => s.classSectionName)),
    [students],
  );
  const curricula = useMemo(
    () => unique(students.map((s) => s.curriculumTrack)),
    [students],
  );
  const ealLevels = useMemo(
    () => unique(students.map((s) => s.ealCode)),
    [students],
  );
  const houses = useMemo(
    () => unique(students.map((s) => s.house)),
    [students],
  );

  const counts = useMemo(() => {
    const c: Record<string, number> = {
      All: students.length,
      Active: 0,
      Inactive: 0,
      Transferred: 0,
      "Passed Out": 0,
      Suspended: 0,
    };
    students.forEach((s) => {
      c[s.status] = (c[s.status] ?? 0) + 1;
    });
    return c;
  }, [students]);

  const filtered = useMemo(() => {
    return students.filter((s) => {
      if (statusFilter !== "All" && s.status !== statusFilter) return false;
      if (classFilter && s.classSectionName !== classFilter) return false;
      if (genderFilter && s.gender !== genderFilter) return false;
      if (curriculumFilter && s.curriculumTrack !== curriculumFilter)
        return false;
      if (ealFilter && s.ealCode !== ealFilter) return false;
      if (houseFilter && s.house !== houseFilter) return false;
      if (hasAllergies && !s.allergies) return false;

      if (search) {
        const haystack =
          `${s.firstName} ${s.lastName} ${s.admNo} ${s.rollNumber}`.toLowerCase();
        if (!haystack.includes(search.toLowerCase())) return false;
      }
      if (quickSearch) {
        const everything =
          `${s.firstName} ${s.lastName} ${s.admNo} ${s.rollNumber} ${s.classSectionName} ${s.status}`.toLowerCase();
        if (!everything.includes(quickSearch.toLowerCase())) return false;
      }
      return true;
    });
  }, [
    students,
    statusFilter,
    search,
    quickSearch,
    classFilter,
    genderFilter,
    curriculumFilter,
    ealFilter,
    houseFilter,
    hasAllergies,
  ]);

  const totalPages = Math.max(1, Math.ceil(filtered.length / pageSize));
  const pageRows = filtered.slice((page - 1) * pageSize, page * pageSize);

  useEffect(() => {
    setPage(1);
  }, [
    statusFilter,
    search,
    quickSearch,
    classFilter,
    genderFilter,
    curriculumFilter,
    ealFilter,
    houseFilter,
    hasAllergies,
    pageSize,
  ]);

  const clearFilters = () => {
    setSearch("");
    setQuickSearch("");
    setClassFilter("");
    setGenderFilter("");
    setCurriculumFilter("");
    setEalFilter("");
    setHouseFilter("");
    setHasAllergies(false);
    setStatusFilter("All");
  };

  const openCreate = () => {
    setEditingStudent(null);
    setIsModalOpen(true);
  };
  const openEdit = (s: StudentResponse) => {
    setEditingStudent(s);
    setIsModalOpen(true);
  };

  const handleView = (s: StudentResponse) => {
    window.alert(
      `${s.firstName} ${s.lastName} (${s.admNo})\n${s.classSectionName} · Roll ${s.rollNumber}\n` +
        `Status: ${s.status} · House: ${s.house ?? "—"}\n` +
        `Nationality: ${s.nationality ?? "—"} · DOB: ${s.dateOfBirth}\n` +
        (s.allergies ? `Allergies: ${s.allergies}` : "No allergies recorded"),
    );
  };

  const handleDelete = async (s: StudentResponse) => {
    if (
      !window.confirm(
        `Delete ${s.firstName} ${s.lastName} (${s.admNo})? This cannot be undone.`,
      )
    )
      return;
    await studentService.delete(s.id);
    await loadData();
  };

  const handleExportCsv = () => {
    const blob = new Blob([toCsv(filtered)], {
      type: "text/csv;charset=utf-8;",
    });
    const url = URL.createObjectURL(blob);
    const link = document.createElement("a");
    link.href = url;
    link.download = "students.csv";
    link.click();
    URL.revokeObjectURL(url);
  };

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
        <button className="btn btn--primary" onClick={openCreate}>
          + Add Student
        </button>
      </div>

      <div className="pipeline">
        {PIPELINE_STEPS.map((step) => (
          <button
            key={step.key}
            className={`pipeline__step ${statusFilter === step.key ? step.activeClass : ""}`}
            onClick={() => setStatusFilter(step.key)}
          >
            {step.label} ({counts[step.key] ?? 0})
          </button>
        ))}
      </div>

      <div className="filters">
        <div className="field">
          <label>Search</label>
          <input
            placeholder="Search students..."
            value={search}
            onChange={(e) => setSearch(e.target.value)}
          />
        </div>
        <div className="field">
          <label>Class</label>
          <select
            value={classFilter}
            onChange={(e) => setClassFilter(e.target.value)}
          >
            <option value="">All Classes</option>
            {classNames.map((c) => (
              <option key={c} value={c}>
                {c}
              </option>
            ))}
          </select>
        </div>
        <div className="field">
          <label>Gender</label>
          <select
            value={genderFilter}
            onChange={(e) => setGenderFilter(e.target.value)}
          >
            <option value="">All Genders</option>
            <option>Male</option>
            <option>Female</option>
          </select>
        </div>
        <div className="field">
          <label>Curriculum</label>
          <select
            value={curriculumFilter}
            onChange={(e) => setCurriculumFilter(e.target.value)}
          >
            <option value="">All Curricula</option>
            {curricula.map((c) => (
              <option key={c} value={c}>
                {c}
              </option>
            ))}
          </select>
        </div>
        <div className="field">
          <label>English Level</label>
          <select
            value={ealFilter}
            onChange={(e) => setEalFilter(e.target.value)}
          >
            <option value="">All Levels</option>
            {ealLevels.map((l) => (
              <option key={l} value={l}>
                {l}
              </option>
            ))}
          </select>
        </div>
        <div className="field">
          <label>House</label>
          <select
            value={houseFilter}
            onChange={(e) => setHouseFilter(e.target.value)}
          >
            <option value="">All Houses</option>
            {houses.map((h) => (
              <option key={h} value={h}>
                {h}
              </option>
            ))}
          </select>
        </div>
        <div className="field" style={{ justifyContent: "flex-end" }}>
          <label style={{ display: "flex", alignItems: "center", gap: 6 }}>
            <input
              type="checkbox"
              checked={hasAllergies}
              onChange={(e) => setHasAllergies(e.target.checked)}
            />{" "}
            Has Allergies
          </label>
        </div>
        <button
          className="btn btn--secondary filters__clear"
          onClick={clearFilters}
        >
          Clear
        </button>
      </div>

      <div
        style={{
          display: "flex",
          justifyContent: "space-between",
          alignItems: "center",
          marginBottom: 12,
          flexWrap: "wrap",
          gap: 8,
        }}
      >
        <div style={{ display: "flex", gap: 8 }}>
          <button
            className="export-btn"
            style={{ background: "var(--green)" }}
            onClick={handleExportCsv}
          >
            CSV
          </button>
          <button
            className="export-btn"
            style={{ background: "var(--red)" }}
            onClick={() => window.alert("PDF export not implemented yet.")}
          >
            PDF
          </button>
          <button
            className="export-btn"
            style={{ background: "var(--navy-700)" }}
            onClick={() => window.print()}
          >
            PRINT
          </button>
        </div>
        <div style={{ display: "flex", gap: 10, alignItems: "center" }}>
          <span style={{ fontSize: 12.5, color: "var(--muted)" }}>
            Show{" "}
            <select
              value={pageSize}
              onChange={(e) => setPageSize(Number(e.target.value))}
            >
              <option value={10}>10</option>
              <option value={25}>25</option>
            </select>{" "}
            entries
          </span>
          <input
            className="search-box"
            type="search"
            placeholder="Search..."
            value={quickSearch}
            onChange={(e) => setQuickSearch(e.target.value)}
          />
        </div>
      </div>

      {isLoading ? (
        <div className="empty-state">Loading…</div>
      ) : pageRows.length === 0 ? (
        <div className="empty-state">No students found.</div>
      ) : (
        <div style={{ overflowX: "auto" }}>
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
              {pageRows.map((s) => (
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
                      className={`badge ${STATUS_BADGE_CLASS[s.status] ?? "badge--gray"}`}
                    >
                      {s.status}
                    </span>
                  </td>
                  <td>{s.house ?? "—"}</td>
                  <td>{s.allergies || "—"}</td>
                  <td style={{ whiteSpace: "nowrap" }}>
                    <button
                      className="btn--icon"
                      title="View"
                      onClick={() => handleView(s)}
                    >
                      👁
                    </button>
                    <button
                      className="btn--icon"
                      title="Fee Summary"
                      style={{ color: "var(--green)" }}
                      onClick={() => setFeeSummaryTarget(s)}
                    >
                      💳
                    </button>
                    <button
                      className="btn--icon"
                      title="Attendance Report"
                      style={{ color: "var(--amber)" }}
                      onClick={() => setAttendanceTarget(s)}
                    >
                      📅
                    </button>
                    <button
                      className="btn--icon"
                      title="Report Card"
                      style={{ color: "var(--blue)" }}
                      onClick={() => setReportCardTarget(s)}
                    >
                      🎓
                    </button>
                    <button
                      className="btn--icon"
                      title="Linked Parents"
                      style={{ color: "var(--purple)" }}
                      onClick={() => setLinkedParentsTarget(s)}
                    >
                      👪
                    </button>
                    <button
                      className="btn--icon"
                      title="Edit"
                      onClick={() => openEdit(s)}
                    >
                      ✎
                    </button>
                    <button
                      className="btn--icon danger"
                      title="Delete"
                      onClick={() => handleDelete(s)}
                    >
                      🗑
                    </button>
                  </td>
                </tr>
              ))}
            </tbody>
          </table>
        </div>
      )}

      <div className="table-footer">
        <span>
          Showing {pageRows.length ? (page - 1) * pageSize + 1 : 0} to{" "}
          {(page - 1) * pageSize + pageRows.length} of {filtered.length} entries
        </span>
        <div style={{ display: "flex", gap: 8, alignItems: "center" }}>
          <button
            className="btn btn--secondary btn--sm"
            disabled={page <= 1}
            onClick={() => setPage((p) => p - 1)}
          >
            Previous
          </button>
          <span>
            {page} / {totalPages}
          </span>
          <button
            className="btn btn--secondary btn--sm"
            disabled={page >= totalPages}
            onClick={() => setPage((p) => p + 1)}
          >
            Next
          </button>
        </div>
      </div>

      <StudentModal
        isOpen={isModalOpen}
        editingStudent={editingStudent}
        classSections={classSections}
        onClose={() => setIsModalOpen(false)}
        onSubmit={async (data: CreateStudentRequest, status) => {
          if (editingStudent) {
            const {
              admNo: _admNo,
              admissionDate: _admissionDate,
              ...rest
            } = data;
            await studentService.update(editingStudent.id, {
              ...rest,
              status: (status ?? "Active") as StudentStatus,
            });
          } else {
            await studentService.create(data);
          }
          await loadData();
        }}
      />

      <NotYetAvailableModal
        title="Fee Summary"
        icon="💳"
        requiredModule="Fees Collection"
        isOpen={!!feeSummaryTarget}
        onClose={() => setFeeSummaryTarget(null)}
      />
      <NotYetAvailableModal
        title="Attendance Report"
        icon="📅"
        requiredModule="Attendance"
        isOpen={!!attendanceTarget}
        onClose={() => setAttendanceTarget(null)}
      />
      <NotYetAvailableModal
        title="Report Card"
        icon="🎓"
        requiredModule="Exams / Results"
        isOpen={!!reportCardTarget}
        onClose={() => setReportCardTarget(null)}
      />
      <LinkedParentsViewModal
        student={linkedParentsTarget}
        onClose={() => setLinkedParentsTarget(null)}
      />
    </div>
  );
}
