import { useEffect, useMemo, useState } from "react";
import { ConfirmDialog } from "@/components/ConfirmDialog";
import { ValidationModal } from "@/components/ValidationModal";
import { getApiErrors } from "@/utils/apiError";
import { teacherService } from "../services/teacherService";
import { TeacherModal } from "../components/TeacherModal";
import { TeacherManageModal } from "../components/TeacherManageModal";
import type {
  CreateTeacherRequest,
  TeacherResponse,
  UpdateTeacherRequest,
} from "../types/teacher.types";

type StatusFilter = "" | "Active" | "Inactive" | "Suspended";

const STATUS_BADGE: Record<string, string> = {
  Active: "badge--active",
  Inactive: "badge--inactive",
  Suspended: "badge--danger",
};

function downloadCsv(rows: TeacherResponse[]) {
  const headers = [
    "ID",
    "Name",
    "Username",
    "Email",
    "Phone",
    "Specialization",
    "Classes",
    "Subjects",
    "Class Teacher Of",
    "Status",
  ];
  const lines = rows.map((t) =>
    [
      t.id,
      t.name,
      t.username,
      t.email,
      t.phone ?? "",
      t.specialization ?? "",
      t.classCount,
      t.subjectCount,
      t.classTeacherOfCount,
      t.status,
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
  a.download = "teachers.csv";
  a.click();
  URL.revokeObjectURL(url);
}

export function TeachersPage() {
  const [teachers, setTeachers] = useState<TeacherResponse[]>([]);
  const [isLoading, setIsLoading] = useState(true);

  const [statusFilter, setStatusFilter] = useState<StatusFilter>("");
  const [search, setSearch] = useState("");
  const [quickSearch, setQuickSearch] = useState("");

  const [page, setPage] = useState(1);
  const [pageSize, setPageSize] = useState(10);

  const [isModalOpen, setIsModalOpen] = useState(false);
  const [editing, setEditing] = useState<TeacherResponse | null>(null);
  const [managingId, setManagingId] = useState<number | null>(null);
  const [deleting, setDeleting] = useState<TeacherResponse | null>(null);
  const [errors, setErrors] = useState<string[] | null>(null);

  const loadData = async () => {
    try {
      setTeachers(await teacherService.getAll());
    } catch (err) {
      setErrors(getApiErrors(err, "Could not load teachers."));
    } finally {
      setIsLoading(false);
    }
  };

  useEffect(() => {
    loadData();
  }, []);

  const counts = useMemo(
    () => ({
      all: teachers.length,
      active: teachers.filter((t) => t.status === "Active").length,
      inactive: teachers.filter((t) => t.status === "Inactive").length,
      suspended: teachers.filter((t) => t.status === "Suspended").length,
      classTeachers: teachers.reduce((n, t) => n + t.classTeacherOfCount, 0),
    }),
    [teachers],
  );

  const filtered = useMemo(() => {
    const q = (quickSearch || search).trim().toLowerCase();
    return teachers.filter((t) => {
      if (statusFilter && t.status !== statusFilter) return false;
      if (q) {
        const haystack =
          `${t.name} ${t.username} ${t.email} ${t.specialization ?? ""}`.toLowerCase();
        if (!haystack.includes(q)) return false;
      }
      return true;
    });
  }, [teachers, statusFilter, search, quickSearch]);

  const totalPages = Math.max(1, Math.ceil(filtered.length / pageSize));
  const pageRows = filtered.slice((page - 1) * pageSize, page * pageSize);

  useEffect(() => {
    setPage(1);
  }, [statusFilter, search, quickSearch, pageSize]);

  const clearFilters = () => {
    setSearch("");
    setQuickSearch("");
    setStatusFilter("");
  };

  const handleConfirmDelete = async () => {
    if (!deleting) return;
    const target = deleting;
    setDeleting(null);
    try {
      await teacherService.delete(target.id);
      await loadData();
    } catch (err) {
      setErrors(getApiErrors(err, "Could not delete this teacher."));
    }
  };

  const managing = teachers.find((t) => t.id === managingId) ?? null;

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
    {
      key: "Suspended",
      label: `Suspended (${counts.suspended})`,
      activeClass: "pipeline__step--red",
    },
  ];

  const statCard = (value: number, label: string, color?: string) => (
    <div className="card" style={{ minWidth: 160, padding: "12px 18px" }}>
      <div style={{ fontSize: 24, fontWeight: 700, color }}>{value}</div>
      <div style={{ fontSize: 12, color: "var(--muted)" }}>{label}</div>
    </div>
  );

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
        <h1 style={{ fontSize: 19, margin: 0 }}>Teachers Management</h1>
        <button
          className="btn btn--primary"
          onClick={() => {
            setEditing(null);
            setIsModalOpen(true);
          }}
        >
          + Add Teacher
        </button>
      </div>

      <div style={{ display: "flex", gap: 14, marginBottom: 16 }}>
        {statCard(counts.all, "Total Teachers")}
        {statCard(counts.classTeachers, "Class Teachers", "var(--amber)")}
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
          <div className="field" style={{ gridColumn: "span 2" }}>
            <label>Search</label>
            <input
              placeholder="Name / username / email..."
              value={search}
              onChange={(e) => setSearch(e.target.value)}
            />
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
            <div className="empty-state">No teachers match these filters.</div>
          ) : (
            <table>
              <thead>
                <tr>
                  <th>Teacher</th>
                  <th>Contact</th>
                  <th>Specialization</th>
                  <th>Classes</th>
                  <th>Status</th>
                  <th>Actions</th>
                </tr>
              </thead>
              <tbody>
                {pageRows.map((t) => (
                  <tr key={t.id}>
                    <td>
                      <span className="avatar-circle">
                        {t.name.charAt(0).toUpperCase()}
                      </span>
                      <strong>{t.name}</strong>
                      <br />
                      <span style={{ fontSize: 11, color: "var(--muted)" }}>
                        {t.username}
                      </span>
                    </td>
                    <td>
                      {t.email}
                      {t.phone && (
                        <>
                          <br />
                          <span style={{ fontSize: 11, color: "var(--muted)" }}>
                            {t.phone}
                          </span>
                        </>
                      )}
                    </td>
                    <td>
                      <span className="badge badge--info">
                        {t.specialization ?? "—"}
                      </span>
                    </td>
                    <td>
                      {t.classCount}
                      {t.classTeacherOfCount > 0 && (
                        <span
                          className="badge badge--warn"
                          style={{ marginLeft: 6 }}
                          title={`Class teacher of ${t.classTeacherOfCount}`}
                        >
                          ★ {t.classTeacherOfCount}
                        </span>
                      )}
                    </td>
                    <td>
                      <span
                        className={`badge ${STATUS_BADGE[t.status] ?? "badge--inactive"}`}
                      >
                        {t.status}
                      </span>
                    </td>
                    <td style={{ whiteSpace: "nowrap" }}>
                      <button
                        className="btn btn--secondary btn--sm"
                        onClick={() => setManagingId(t.id)}
                      >
                        Manage
                      </button>{" "}
                      <button
                        className="btn--icon"
                        title="Edit"
                        onClick={() => {
                          setEditing(t);
                          setIsModalOpen(true);
                        }}
                      >
                        ✎
                      </button>
                      <button
                        className="btn--icon danger"
                        title="Delete"
                        onClick={() => setDeleting(t)}
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

      <TeacherModal
        isOpen={isModalOpen}
        editing={editing}
        onClose={() => setIsModalOpen(false)}
        onCreate={async (data: CreateTeacherRequest) => {
          await teacherService.create(data);
          setStatusFilter("");
          await loadData();
        }}
        onUpdate={async (id: number, data: UpdateTeacherRequest) => {
          await teacherService.update(id, data);
          await loadData();
        }}
      />

      <TeacherManageModal
        teacher={managing}
        onClose={() => setManagingId(null)}
        onChanged={loadData}
      />

      <ConfirmDialog
        isOpen={!!deleting}
        title="Delete Teacher"
        message={`Delete ${deleting?.name ?? "this teacher"} (${deleting?.username ?? ""})? Their login is removed too. Teachers who still have subjects or a class-teacher role cannot be deleted — remove those first.`}
        confirmLabel="Delete"
        onConfirm={handleConfirmDelete}
        onCancel={() => setDeleting(null)}
      />

      <ValidationModal errors={errors} onClose={() => setErrors(null)} />
    </div>
  );
}
