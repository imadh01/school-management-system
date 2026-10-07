import { useEffect, useMemo, useState } from "react";
import { ConfirmDialog } from "@/components/ConfirmDialog";
import { ValidationModal } from "@/components/ValidationModal";
import { classSectionService } from "@/features/class-sections/services/classSectionService";
import type { ClassSectionResponse } from "@/features/class-sections/types/classSection.types";
import { getApiErrors } from "@/utils/apiError";
import { subjectService } from "../services/subjectService";
import { SubjectModal } from "../components/SubjectModal";
import {
  SUBJECT_TYPES,
  type SubjectRequest,
  type SubjectResponse,
} from "../types/subject.types";

type StatusFilter = "" | "Active" | "Inactive";

const unique = (values: string[]) => [...new Set(values)].sort();

function MarksCell({ s, kind }: { s: SubjectResponse; kind: "max" | "pass" }) {
  if (s.type === "Both") {
    const top =
      kind === "max"
        ? (s.theoryMax ?? 0) + (s.practicalMax ?? 0)
        : (s.theoryPass ?? 0) + (s.practicalPass ?? 0);
    const sub =
      kind === "max"
        ? `T:${s.theoryMax} · P:${s.practicalMax}`
        : `T:${s.theoryPass} · P:${s.practicalPass}`;
    return (
      <>
        {top}
        <br />
        <span style={{ fontSize: 11, color: "var(--muted)" }}>{sub}</span>
      </>
    );
  }
  return <>{kind === "max" ? s.maxMarks : s.passMarks}</>;
}

function downloadCsv(rows: SubjectResponse[]) {
  const headers = [
    "ID",
    "Subject",
    "Code",
    "Class",
    "Type",
    "Max",
    "Pass",
    "Teacher",
    "Status",
  ];
  const lines = rows.map((s) => {
    const both = s.type === "Both";
    const max = both
      ? (s.theoryMax ?? 0) + (s.practicalMax ?? 0)
      : (s.maxMarks ?? "");
    const pass = both
      ? (s.theoryPass ?? 0) + (s.practicalPass ?? 0)
      : (s.passMarks ?? "");
    return [
      s.id,
      s.name,
      s.code,
      s.classSectionName,
      s.type,
      max,
      pass,
      s.teacherName ?? "",
      s.status,
    ]
      .map((v) => `"${String(v).replace(/"/g, '""')}"`)
      .join(",");
  });
  const blob = new Blob([[headers.join(","), ...lines].join("\n")], {
    type: "text/csv;charset=utf-8;",
  });
  const url = URL.createObjectURL(blob);
  const a = document.createElement("a");
  a.href = url;
  a.download = "subjects.csv";
  a.click();
  URL.revokeObjectURL(url);
}

export function SubjectsPage() {
  const [subjects, setSubjects] = useState<SubjectResponse[]>([]);
  const [classSections, setClassSections] = useState<ClassSectionResponse[]>(
    [],
  );
  const [isLoading, setIsLoading] = useState(true);

  const [statusFilter, setStatusFilter] = useState<StatusFilter>("");
  const [search, setSearch] = useState("");
  const [quickSearch, setQuickSearch] = useState("");
  const [classFilter, setClassFilter] = useState("");
  const [yearFilter, setYearFilter] = useState("");
  const [typeFilter, setTypeFilter] = useState("");

  const [page, setPage] = useState(1);
  const [pageSize, setPageSize] = useState(10);

  const [isModalOpen, setIsModalOpen] = useState(false);
  const [editing, setEditing] = useState<SubjectResponse | null>(null);
  const [deleting, setDeleting] = useState<SubjectResponse | null>(null);
  const [errors, setErrors] = useState<string[] | null>(null);

  const loadData = async () => {
    try {
      const [subjectList, sections] = await Promise.all([
        subjectService.getAll(),
        classSectionService.getAll(),
      ]);
      setSubjects(subjectList);
      setClassSections(sections);
    } catch (err) {
      setErrors(getApiErrors(err, "Could not load subjects."));
    } finally {
      setIsLoading(false);
    }
  };

  useEffect(() => {
    loadData();
  }, []);

  const years = useMemo(
    () => unique(classSections.map((c) => c.academicYearName)),
    [classSections],
  );
  const classNames = useMemo(
    () => unique(subjects.map((s) => s.classSectionName)),
    [subjects],
  );

  const counts = useMemo(
    () => ({
      all: subjects.length,
      active: subjects.filter((s) => s.status === "Active").length,
      inactive: subjects.filter((s) => s.status === "Inactive").length,
    }),
    [subjects],
  );

  const filtered = useMemo(() => {
    const q = (quickSearch || search).trim().toLowerCase();
    return subjects.filter((s) => {
      if (statusFilter && s.status !== statusFilter) return false;
      if (classFilter && s.classSectionName !== classFilter) return false;
      if (yearFilter && !s.classSectionName.includes(`(${yearFilter})`))
        return false;
      if (typeFilter && s.type !== typeFilter) return false;
      if (q) {
        const haystack =
          `${s.name} ${s.code} ${s.teacherName ?? ""}`.toLowerCase();
        if (!haystack.includes(q)) return false;
      }
      return true;
    });
  }, [
    subjects,
    statusFilter,
    classFilter,
    yearFilter,
    typeFilter,
    search,
    quickSearch,
  ]);

  const totalPages = Math.max(1, Math.ceil(filtered.length / pageSize));
  const pageRows = filtered.slice((page - 1) * pageSize, page * pageSize);

  useEffect(() => {
    setPage(1);
  }, [
    statusFilter,
    classFilter,
    yearFilter,
    typeFilter,
    search,
    quickSearch,
    pageSize,
  ]);

  const clearFilters = () => {
    setSearch("");
    setQuickSearch("");
    setClassFilter("");
    setYearFilter("");
    setTypeFilter("");
    setStatusFilter("");
  };

  const handleSave = async (data: SubjectRequest) => {
    if (editing) await subjectService.update(editing.id, data);
    else await subjectService.create(data);
    if (!editing) setStatusFilter("");
    await loadData();
  };

  const handleConfirmDelete = async () => {
    if (!deleting) return;
    const target = deleting;
    setDeleting(null);
    try {
      await subjectService.delete(target.id);
      await loadData();
    } catch (err) {
      setErrors(getApiErrors(err, "Could not delete this subject."));
    }
  };

  const pipelineSteps: {
    key: StatusFilter;
    label: string;
    activeClass: string;
  }[] = [
    {
      key: "",
      label: `All (${counts.all})`,
      activeClass: "pipeline__step--dark",
    },
    {
      key: "Active",
      label: `Active (${counts.active})`,
      activeClass: "pipeline__step--green",
    },
    {
      key: "Inactive",
      label: `Inactive (${counts.inactive})`,
      activeClass: "",
    },
  ];

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
        <h1 style={{ fontSize: 19, margin: 0 }}>Subjects</h1>
        <button
          className="btn btn--primary"
          onClick={() => {
            setEditing(null);
            setIsModalOpen(true);
          }}
        >
          + Add Subject
        </button>
      </div>

      <div className="card">
        <div className="pipeline">
          {pipelineSteps.map((s) => (
            <button
              key={s.key}
              type="button"
              className={`pipeline__step ${statusFilter === s.key ? "pipeline__step--active" : s.activeClass}`}
              onClick={() => setStatusFilter(s.key)}
            >
              {s.label}
            </button>
          ))}
        </div>

        <div className="filters">
          <div className="field">
            <label>Search</label>
            <input
              placeholder="Search subjects..."
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
                <option key={c}>{c}</option>
              ))}
            </select>
          </div>
          <div className="field">
            <label>Academic Year</label>
            <select
              value={yearFilter}
              onChange={(e) => setYearFilter(e.target.value)}
            >
              <option value="">All Years</option>
              {years.map((y) => (
                <option key={y}>{y}</option>
              ))}
            </select>
          </div>
          <div className="field">
            <label>Type</label>
            <select
              value={typeFilter}
              onChange={(e) => setTypeFilter(e.target.value)}
            >
              <option value="">All Types</option>
              {SUBJECT_TYPES.map((t) => (
                <option key={t}>{t}</option>
              ))}
            </select>
          </div>
          <button
            className="btn btn--secondary filters__clear"
            onClick={clearFilters}
          >
            Clear
          </button>
        </div>

        <div className="table-toolbar">
          <div className="table-toolbar__left">
            <button
              className="export-btn export-btn--csv"
              onClick={() => downloadCsv(filtered)}
            >
              CSV
            </button>
            <button
              className="export-btn export-btn--pdf"
              onClick={() => alert("PDF export coming soon.")}
            >
              PDF
            </button>
            <button
              className="export-btn export-btn--print"
              onClick={() => window.print()}
            >
              PRINT
            </button>
          </div>
          <div className="table-toolbar__right">
            <div className="pagesize">
              Show{" "}
              <select
                value={pageSize}
                onChange={(e) => setPageSize(Number(e.target.value))}
              >
                <option value={10}>10</option>
                <option value={25}>25</option>
                <option value={50}>50</option>
              </select>{" "}
              entries
            </div>
            <input
              className="search-box"
              type="search"
              placeholder="Search..."
              value={quickSearch}
              onChange={(e) => setQuickSearch(e.target.value)}
            />
          </div>
        </div>

        <div style={{ overflowX: "auto" }}>
          {isLoading ? (
            <div className="empty-state">Loading…</div>
          ) : pageRows.length === 0 ? (
            <div className="empty-state">No subjects match these filters.</div>
          ) : (
            <table>
              <thead>
                <tr>
                  <th>#</th>
                  <th>Subject</th>
                  <th>Code</th>
                  <th>Class</th>
                  <th>Type</th>
                  <th>Max</th>
                  <th>Pass</th>
                  <th>Teacher</th>
                  <th>Status</th>
                  <th>Actions</th>
                </tr>
              </thead>
              <tbody>
                {pageRows.map((s, index) => (
                  <tr key={s.id}>
                    <td>{(page - 1) * pageSize + index + 1}</td>
                    <td>{s.name}</td>
                    <td>
                      <span className="badge badge--warn">{s.code}</span>
                    </td>
                    <td>{s.classSectionName}</td>
                    <td>
                      <span
                        className={`badge ${s.type === "Both" ? "badge--purple" : "badge--info"}`}
                      >
                        {s.type}
                      </span>
                    </td>
                    <td>
                      <MarksCell s={s} kind="max" />
                    </td>
                    <td>
                      <MarksCell s={s} kind="pass" />
                    </td>
                    <td>
                      {s.teacherName ?? (
                        <span style={{ color: "var(--muted)" }}>—</span>
                      )}
                    </td>
                    <td>
                      <span
                        className={`badge ${s.status === "Active" ? "badge--active" : "badge--inactive"}`}
                      >
                        {s.status}
                      </span>
                    </td>
                    <td style={{ whiteSpace: "nowrap" }}>
                      <button
                        className="btn--icon"
                        title="Edit"
                        onClick={() => {
                          setEditing(s);
                          setIsModalOpen(true);
                        }}
                      >
                        ✎
                      </button>
                      <button
                        className="btn--icon danger"
                        title="Delete"
                        onClick={() => setDeleting(s)}
                      >
                        🗑
                      </button>
                    </td>
                  </tr>
                ))}
              </tbody>
            </table>
          )}
        </div>

        <div className="table-footer">
          <span>
            Showing {filtered.length === 0 ? 0 : (page - 1) * pageSize + 1} to{" "}
            {Math.min(page * pageSize, filtered.length)} of {filtered.length}{" "}
            entries
          </span>
          <div className="table-footer__pages">
            <button
              className="btn btn--secondary btn--sm"
              disabled={page <= 1}
              onClick={() => setPage((p) => Math.max(1, p - 1))}
            >
              Previous
            </button>
            <span>
              Page {page} of {totalPages}
            </span>
            <button
              className="btn btn--secondary btn--sm"
              disabled={page >= totalPages}
              onClick={() => setPage((p) => Math.min(totalPages, p + 1))}
            >
              Next
            </button>
          </div>
        </div>
      </div>

      <SubjectModal
        isOpen={isModalOpen}
        editing={editing}
        classSections={classSections}
        onClose={() => setIsModalOpen(false)}
        onSave={handleSave}
      />

      <ConfirmDialog
        isOpen={!!deleting}
        title="Delete Subject"
        message={`Delete ${deleting?.name ?? "this subject"} (${deleting?.classSectionName ?? ""})? This cannot be undone. If a teacher is assigned to it, that assignment is removed too.`}
        confirmLabel="Delete"
        onConfirm={handleConfirmDelete}
        onCancel={() => setDeleting(null)}
      />

      <ValidationModal errors={errors} onClose={() => setErrors(null)} />
    </div>
  );
}
