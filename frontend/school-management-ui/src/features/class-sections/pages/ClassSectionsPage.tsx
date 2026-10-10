import { useEffect, useMemo, useState } from "react";
import { ConfirmDialog } from "@/components/ConfirmDialog";
import { ValidationModal } from "@/components/ValidationModal";
import { ViewDetailsModal } from "@/components/ViewDetailsModal";
import { getApiErrors, isConcurrencyConflict } from "@/utils/apiError";
import { classSectionService } from "../services/classSectionService";
import { ClassSectionModal } from "../components/ClassSectionModal";
import {
  CLASS_MEDIUMS,
  CLASS_STAGES,
  type ClassSectionResponse,
  type ClassSectionFields,
} from "../types/classSection.types";

type StatusFilter = "" | "Active" | "Inactive";

const unique = (values: string[]) => [...new Set(values)].sort();

function locationText(c: ClassSectionResponse) {
  const parts = [
    c.building,
    c.floor !== null ? `Floor ${c.floor}` : null,
    c.room ? `Room ${c.room}` : null,
  ].filter(Boolean);
  return parts.length ? parts.join(" · ") : "—";
}

function downloadCsv(rows: ClassSectionResponse[]) {
  const headers = [
    "Code",
    "Class",
    "Section",
    "Academic Year",
    "Stage",
    "Medium",
    "Stream",
    "Capacity",
    "Enrolled",
    "Building",
    "Floor",
    "Room",
    "Status",
  ];
  const lines = rows.map((c) =>
    [
      c.code,
      c.name,
      c.section,
      c.academicYearName,
      c.stage,
      c.medium,
      c.stream,
      c.capacity ?? "",
      c.enrolled,
      c.building ?? "",
      c.floor ?? "",
      c.room ?? "",
      c.status,
    ]
      .map((v) => `"${String(v).replace(/"/g, '""')}"`)
      .join(","),
  );
  const blob = new Blob([[headers.join(","), ...lines].join("\n")], {
    type: "text/csv;charset=utf-8;",
  });
  const url = URL.createObjectURL(blob);
  const a = document.createElement("a");
  a.href = url;
  a.download = "classes-sections.csv";
  a.click();
  URL.revokeObjectURL(url);
}

export function ClassSectionsPage() {
  const [rows, setRows] = useState<ClassSectionResponse[]>([]);
  const [isLoading, setIsLoading] = useState(true);

  const [statusFilter, setStatusFilter] = useState<StatusFilter>("");
  const [search, setSearch] = useState("");
  const [quickSearch, setQuickSearch] = useState("");
  const [yearFilter, setYearFilter] = useState("");
  const [sectionFilter, setSectionFilter] = useState("");
  const [stageFilter, setStageFilter] = useState("");
  const [mediumFilter, setMediumFilter] = useState("");

  const [page, setPage] = useState(1);
  const [pageSize, setPageSize] = useState(10);

  const [isModalOpen, setIsModalOpen] = useState(false);
  const [editing, setEditing] = useState<ClassSectionResponse | null>(null);
  const [viewing, setViewing] = useState<ClassSectionResponse | null>(null);
  const [deleting, setDeleting] = useState<ClassSectionResponse | null>(null);
  const [errors, setErrors] = useState<string[] | null>(null);

  const loadData = async () => {
    setIsLoading(true);
    try {
      setRows(await classSectionService.getAll());
    } catch (err) {
      setErrors(getApiErrors(err, "Could not load classes."));
    } finally {
      setIsLoading(false);
    }
  };

  useEffect(() => {
    loadData();
  }, []);

  const years = useMemo(
    () => unique(rows.map((c) => c.academicYearName)),
    [rows],
  );
  const sections = useMemo(() => unique(rows.map((c) => c.section)), [rows]);

  const counts = useMemo(
    () => ({
      all: rows.length,
      active: rows.filter((c) => c.status === "Active").length,
      inactive: rows.filter((c) => c.status === "Inactive").length,
    }),
    [rows],
  );

  const filtered = useMemo(() => {
    const q = (quickSearch || search).trim().toLowerCase();
    return rows.filter((c) => {
      if (statusFilter && c.status !== statusFilter) return false;
      if (yearFilter && c.academicYearName !== yearFilter) return false;
      if (sectionFilter && c.section !== sectionFilter) return false;
      if (stageFilter && c.stage !== stageFilter) return false;
      if (mediumFilter && c.medium !== mediumFilter) return false;
      if (q) {
        const haystack =
          `${c.code} ${c.displayName} ${c.room ?? ""} ${c.building ?? ""}`.toLowerCase();
        if (!haystack.includes(q)) return false;
      }
      return true;
    });
  }, [
    rows,
    statusFilter,
    yearFilter,
    sectionFilter,
    stageFilter,
    mediumFilter,
    search,
    quickSearch,
  ]);

  const totalPages = Math.max(1, Math.ceil(filtered.length / pageSize));
  const pageRows = filtered.slice((page - 1) * pageSize, page * pageSize);

  useEffect(() => {
    setPage(1);
  }, [
    statusFilter,
    yearFilter,
    sectionFilter,
    stageFilter,
    mediumFilter,
    search,
    quickSearch,
    pageSize,
  ]);

  const clearFilters = () => {
    setSearch("");
    setQuickSearch("");
    setYearFilter("");
    setSectionFilter("");
    setStageFilter("");
    setMediumFilter("");
    setStatusFilter("");
  };

  const handleSave = async (data: ClassSectionFields) => {
    if (editing) {
      try {
        await classSectionService.update(editing.id, {
          ...data,
          rowVersion: editing.rowVersion,
        });
      } catch (err) {
        // Someone else saved this class first: refresh the list so the next try starts fresh.
        if (isConcurrencyConflict(err)) await loadData();
        throw err;
      }
    } else {
      await classSectionService.create(data);
    }
    await loadData();
  };

  const handleToggleStatus = async (c: ClassSectionResponse) => {
    try {
      await classSectionService.changeStatus(c.id, c.status !== "Active");
      await loadData();
    } catch (err) {
      setErrors(getApiErrors(err, "Could not change the status."));
    }
  };

  const handleConfirmDelete = async () => {
    if (!deleting) return;
    const target = deleting;
    setDeleting(null);
    try {
      await classSectionService.delete(target.id);
      await loadData();
    } catch (err) {
      setErrors(getApiErrors(err, "Could not delete this class."));
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
        <h1 style={{ fontSize: 19, margin: 0 }}>Classes &amp; Sections</h1>
        <button
          className="btn btn--primary"
          onClick={() => {
            setEditing(null);
            setIsModalOpen(true);
          }}
        >
          + Add Class
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
              placeholder="Search by code, class, room..."
              value={search}
              onChange={(e) => setSearch(e.target.value)}
            />
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
            <label>Section</label>
            <select
              value={sectionFilter}
              onChange={(e) => setSectionFilter(e.target.value)}
            >
              <option value="">All Sections</option>
              {sections.map((s) => (
                <option key={s}>{s}</option>
              ))}
            </select>
          </div>
          <div className="field">
            <label>Stage</label>
            <select
              value={stageFilter}
              onChange={(e) => setStageFilter(e.target.value)}
            >
              <option value="">All Stages</option>
              {CLASS_STAGES.map((s) => (
                <option key={s}>{s}</option>
              ))}
            </select>
          </div>
          <div className="field">
            <label>Medium</label>
            <select
              value={mediumFilter}
              onChange={(e) => setMediumFilter(e.target.value)}
            >
              <option value="">All Mediums</option>
              {CLASS_MEDIUMS.map((m) => (
                <option key={m}>{m}</option>
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
            <div className="empty-state">No classes match these filters.</div>
          ) : (
            <table>
              <thead>
                <tr>
                  <th>Code</th>
                  <th>Class</th>
                  <th>Section</th>
                  <th>Stage</th>
                  <th>Medium</th>
                  <th>Strength</th>
                  <th>Location</th>
                  <th>Status</th>
                  <th>Actions</th>
                </tr>
              </thead>
              <tbody>
                {pageRows.map((c) => (
                  <tr key={c.id}>
                    <td>
                      <strong>{c.code}</strong>
                    </td>
                    <td>
                      {c.name}
                      <br />
                      <span style={{ fontSize: 11, color: "var(--muted)" }}>
                        {c.academicYearName}
                      </span>
                    </td>
                    <td>{c.section}</td>
                    <td>{c.stage}</td>
                    <td>{c.medium}</td>
                    <td>
                      {c.enrolled} / {c.capacity ?? "—"}
                    </td>
                    <td>{locationText(c)}</td>
                    <td>
                      <span
                        className={`badge ${c.status === "Active" ? "badge--active" : "badge--inactive"}`}
                      >
                        {c.status}
                      </span>
                    </td>
                    <td style={{ whiteSpace: "nowrap" }}>
                      <button
                        className="btn--icon"
                        title="View"
                        onClick={() => setViewing(c)}
                      >
                        👁
                      </button>
                      <button
                        className="btn--icon"
                        title="Edit"
                        onClick={() => {
                          setEditing(c);
                          setIsModalOpen(true);
                        }}
                      >
                        ✎
                      </button>
                      <button
                        className="btn--icon"
                        title={
                          c.status === "Active" ? "Deactivate" : "Activate"
                        }
                        onClick={() => handleToggleStatus(c)}
                      >
                        {c.status === "Active" ? "⏸" : "▶"}
                      </button>
                      <button
                        className="btn--icon danger"
                        title="Delete"
                        onClick={() => setDeleting(c)}
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

      <ClassSectionModal
        isOpen={isModalOpen}
        editing={editing}
        onClose={() => setIsModalOpen(false)}
        onSave={handleSave}
      />

      <ViewDetailsModal
        isOpen={!!viewing}
        title={viewing?.displayName ?? ""}
        subtitle={
          viewing ? `${viewing.code} · ${viewing.academicYearName}` : ""
        }
        onClose={() => setViewing(null)}
        sections={
          viewing
            ? [
                {
                  heading: "Class",
                  rows: [
                    { label: "Code", value: viewing.code },
                    { label: "Class", value: viewing.name },
                    { label: "Section", value: viewing.section },
                    { label: "Grade", value: viewing.grade },
                    { label: "Status", value: viewing.status },
                    { label: "Academic Year", value: viewing.academicYearName },
                  ],
                },
                {
                  heading: "Academic",
                  rows: [
                    { label: "Stage", value: viewing.stage },
                    { label: "Medium", value: viewing.medium },
                    { label: "Stream", value: viewing.stream },
                    { label: "Capacity", value: viewing.capacity },
                    { label: "Enrolled", value: viewing.enrolled },
                    {
                      label: "Seats Left",
                      value:
                        viewing.capacity !== null
                          ? Math.max(0, viewing.capacity - viewing.enrolled)
                          : null,
                    },
                  ],
                },
                {
                  heading: "Location",
                  rows: [
                    { label: "Building", value: viewing.building },
                    { label: "Floor", value: viewing.floor },
                    { label: "Room", value: viewing.room },
                  ],
                },
              ]
            : []
        }
      />

      <ConfirmDialog
        isOpen={!!deleting}
        title="Delete Class"
        message={`Delete ${deleting?.displayName ?? "this class"}? This cannot be undone. Classes that have students, admissions or subjects cannot be deleted — deactivate them instead.`}
        confirmLabel="Delete"
        onConfirm={handleConfirmDelete}
        onCancel={() => setDeleting(null)}
      />

      <ValidationModal errors={errors} onClose={() => setErrors(null)} />
    </div>
  );
}
