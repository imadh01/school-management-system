import { PageHero } from "@/components/PageHero";
import { ConfirmDialog } from "@/components/ConfirmDialog";
import { useToast } from "@/components/Toast";
import { Plus } from "lucide-react";
import { isConcurrencyConflict } from "@/utils/apiError";
import { useEffect, useMemo, useState } from "react";
import { parentService } from "../services/parentService";
import type { ParentResponse } from "../types/parent.types";
import { ParentModal } from "../components/ParentModal";
import { LinkedChildrenModal } from "../components/LinkedChildrenModal";
import { useAuth } from "@/store/AuthContext";
import { Permission } from "@/routes/permissions";

// function initials(name: string) {
//   return (name || "?").trim().charAt(0).toUpperCase();
// }

function toCsv(rows: ParentResponse[]) {
  const headers = [
    "ID",
    "Name",
    "Mobile",
    "Email",
    "Employer",
    "Job Title",
    "Method",
    "Status",
  ];
  const lines = rows.map((p) =>
    [
      p.id,
      p.name,
      p.mobile,
      p.email ?? "",
      p.employer ?? "",
      p.jobTitle ?? "",
      p.preferredContactMethod ?? "",
      p.status,
    ]
      .map((v) => `"${String(v).replace(/"/g, '""')}"`)
      .join(","),
  );
  return [headers.join(","), ...lines].join("\n");
}

function downloadCsv(rows: ParentResponse[]) {
  const csv = toCsv(rows);
  const blob = new Blob([csv], { type: "text/csv;charset=utf-8;" });
  const url = URL.createObjectURL(blob);
  const a = document.createElement("a");
  a.href = url;
  a.download = "parents.csv";
  a.click();
  URL.revokeObjectURL(url);
}

export function ParentsPage() {
  // F1: hide what this role cannot do (the API refuses it anyway). A teacher holding
  // only Parents.ViewScoped sees the parents of their students: name and mobile.
  const { hasAnyPermission } = useAuth();
  const canCreate = hasAnyPermission([Permission.ParentsCreate]);
  const canEdit = hasAnyPermission([Permission.ParentsEdit]);
  const canDelete = hasAnyPermission([Permission.ParentsDelete]);

  const toast = useToast();
  const [viewingParent, setViewingParent] = useState<ParentResponse | null>(
    null,
  );
  const [deletingParent, setDeletingParent] = useState<ParentResponse | null>(
    null,
  );
  const [parents, setParents] = useState<ParentResponse[]>([]);
  const [isLoading, setIsLoading] = useState(true);

  const [statusFilter, setStatusFilter] = useState<"" | "Active" | "Inactive">(
    "",
  );
  const [search, setSearch] = useState("");
  const [quickSearch, setQuickSearch] = useState("");
  const [methodFilter, setMethodFilter] = useState("");

  const [page, setPage] = useState(1);
  const [pageSize, setPageSize] = useState(10);

  const [isModalOpen, setIsModalOpen] = useState(false);
  const [editingParent, setEditingParent] = useState<ParentResponse | null>(
    null,
  );

  const [linkedChildrenParent, setLinkedChildrenParent] =
    useState<ParentResponse | null>(null);
  const [feeRecordsParent, setFeeRecordsParent] =
    useState<ParentResponse | null>(null);

  const loadData = async () => {
    setIsLoading(true);
    setParents(await parentService.getAll());
    setIsLoading(false);
  };

  useEffect(() => {
    loadData();
  }, []);

  const counts = useMemo(
    () => ({
      all: parents.length,
      active: parents.filter((p) => p.status === "Active").length,
      inactive: parents.filter((p) => p.status === "Inactive").length,
    }),
    [parents],
  );

  const filtered = useMemo(() => {
    const q = (quickSearch || search).trim().toLowerCase();
    return parents.filter((p) => {
      if (statusFilter && p.status !== statusFilter) return false;
      if (methodFilter && p.preferredContactMethod !== methodFilter)
        return false;
      if (q) {
        const haystack = `${p.name} ${p.mobile} ${p.email ?? ""}`.toLowerCase();
        if (!haystack.includes(q)) return false;
      }
      return true;
    });
  }, [parents, statusFilter, search, quickSearch, methodFilter]);

  const totalPages = Math.max(1, Math.ceil(filtered.length / pageSize));
  const pageRows = filtered.slice((page - 1) * pageSize, page * pageSize);

  useEffect(() => {
    setPage(1);
  }, [statusFilter, search, quickSearch, methodFilter, pageSize]);

  const clearFilters = () => {
    setSearch("");
    setQuickSearch("");
    setMethodFilter("");
    setStatusFilter("");
  };

  const handleView = (p: ParentResponse) => setViewingParent(p);

  const handleDelete = (p: ParentResponse) => setDeletingParent(p);

  const confirmDelete = async () => {
    const p = deletingParent;
    setDeletingParent(null);
    if (!p) return;
    try {
      await parentService.delete(p.id);
      toast.success(`${p.name} was deleted.`);
      await loadData();
    } catch {
      toast.error(
        `Could not delete ${p.name}. They may still be linked to students.`,
      );
    }
  };

  const pipelineSteps: {
    key: "" | "Active" | "Inactive";
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
      <PageHero
        title="Parents"
        subtitle="Parents and guardians, and the students they are linked to."
        actions={
          <>
            {canCreate && (
              <button
                className="btn btn--primary"
                onClick={() => {
                  setEditingParent(null);
                  setIsModalOpen(true);
                }}
              >
                <Plus size={16} aria-hidden="true" />
                Add parent
              </button>
            )}
          </>
        }
      />

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
              placeholder="Search by name, mobile, email..."
              value={search}
              onChange={(e) => setSearch(e.target.value)}
            />
          </div>
          <div className="field">
            <label>Contact Method</label>
            <select
              value={methodFilter}
              onChange={(e) => setMethodFilter(e.target.value)}
            >
              <option value="">All Methods</option>
              <option>Email</option>
              <option>Phone</option>
              <option>WhatsApp</option>
              <option>App</option>
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
              onClick={() =>
                toast.info("PDF export is coming in a later release.")
              }
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
            <div className="empty-state">No parents match these filters.</div>
          ) : (
            <table>
              <thead>
                <tr>
                  <th>#</th>
                  <th>Parent</th>
                  <th>Employer</th>
                  <th>Contact</th>
                  <th>Method</th>
                  <th>Status</th>
                  <th>Actions</th>
                </tr>
              </thead>
              <tbody>
                {pageRows.map((p, index) => (
                  <tr key={p.id}>
                    <td>{(page - 1) * pageSize + index + 1}</td>
                    <td>{p.name}</td>
                    <td>
                      {p.employer ? (
                        <>
                          {p.employer}
                          <br />
                          <span style={{ fontSize: 11, color: "var(--muted)" }}>
                            {p.jobTitle || ""}
                          </span>
                        </>
                      ) : (
                        "—"
                      )}
                    </td>
                    <td>
                      {p.mobile}
                      <br />
                      <span style={{ fontSize: 11, color: "var(--muted)" }}>
                        {p.email || "—"}
                      </span>
                    </td>
                    <td>
                      <span className="badge badge--info">
                        {p.preferredContactMethod ?? "—"}
                      </span>
                    </td>
                    <td>
                      <span
                        className={`badge ${p.status === "Active" ? "badge--active" : "badge--inactive"}`}
                      >
                        {p.status}
                      </span>
                    </td>
                    <td style={{ whiteSpace: "nowrap" }}>
                      <button
                        className="btn--icon"
                        title="View"
                        onClick={() => handleView(p)}
                      >
                        👁
                      </button>
                      <button
                        className="btn--icon"
                        title="Linked Children"
                        style={{ color: "var(--info)" }}
                        onClick={() => setLinkedChildrenParent(p)}
                      >
                        👪
                      </button>
                      <button
                        className="btn--icon"
                        title="Fee Records"
                        style={{ color: "var(--ok)" }}
                        onClick={() => setFeeRecordsParent(p)}
                      >
                        💳
                      </button>
                      {canEdit && (
                        <button
                          className="btn--icon"
                          title="Edit"
                          onClick={() => {
                            setEditingParent(p);
                            setIsModalOpen(true);
                          }}
                        >
                          ✎
                        </button>
                      )}
                      {canDelete && (
                        <button
                          className="btn--icon danger"
                          title="Delete"
                          onClick={() => handleDelete(p)}
                        >
                          🗑
                        </button>
                      )}
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

      <ParentModal
        isOpen={isModalOpen}
        editingParent={editingParent}
        onClose={() => setIsModalOpen(false)}
        onCreate={async (data) => {
          await parentService.create(data);
          await loadData();
        }}
        onUpdate={async (id, data) => {
          try {
            await parentService.update(id, {
              ...data,
              rowVersion: editingParent?.rowVersion ?? "",
            });
          } catch (err) {
            // Someone else saved this parent first: refresh the list so the next try starts fresh.
            if (isConcurrencyConflict(err)) await loadData();
            throw err;
          }
          await loadData();
        }}
      />

      <LinkedChildrenModal
        isOpen={!!linkedChildrenParent}
        onClose={() => setLinkedChildrenParent(null)}
        parent={linkedChildrenParent}
        onChanged={loadData}
      />

      {feeRecordsParent && (
        <div className="modal-backdrop open">
          <div className="modal modal--wide">
            <div className="modal__header">
              <h2>💳 Parent Fees Records</h2>
              <button
                className="modal__close"
                onClick={() => setFeeRecordsParent(null)}
              >
                ✕
              </button>
            </div>
            <div className="modal__body">
              <div className="modal-context-card">
                <div>
                  <strong>{feeRecordsParent.name}</strong>
                  <br />
                  📞 {feeRecordsParent.mobile}
                </div>
              </div>
              <div className="empty-state">
                Fee records need the Fees Collection module, which isn't built
                yet.
              </div>
            </div>
            <div className="modal__footer">
              <button
                className="btn btn--secondary"
                onClick={() => setFeeRecordsParent(null)}
              >
                Close
              </button>
            </div>
          </div>
        </div>
      )}

      {viewingParent && (
        <div className="modal-backdrop open">
          <div
            className="modal"
            style={{ maxWidth: 480 }}
            role="dialog"
            aria-modal="true"
            aria-labelledby="parent-view-title"
          >
            <div className="modal__header">
              <h2 id="parent-view-title">{viewingParent.name}</h2>
              <button
                className="modal__close"
                aria-label="Close"
                onClick={() => setViewingParent(null)}
              >
                ✕
              </button>
            </div>
            <div className="modal__body">
              <dl className="detail-list">
                <dt>Status</dt>
                <dd>{viewingParent.status}</dd>
                <dt>Mobile</dt>
                <dd>{viewingParent.mobile}</dd>
                <dt>Email</dt>
                <dd>{viewingParent.email || "—"}</dd>
                <dt>Preferred contact</dt>
                <dd>
                  {viewingParent.preferredContactMethod ?? "—"} (
                  {viewingParent.preferredLanguage ?? "—"})
                </dd>
              </dl>
            </div>
            <div className="modal__footer">
              <button
                className="btn btn--secondary"
                onClick={() => setViewingParent(null)}
              >
                Close
              </button>
            </div>
          </div>
        </div>
      )}

      <ConfirmDialog
        isOpen={deletingParent !== null}
        title="Delete parent?"
        message={`Delete ${deletingParent?.name ?? ""}? This cannot be undone.`}
        confirmLabel="Delete"
        danger
        onConfirm={confirmDelete}
        onCancel={() => setDeletingParent(null)}
      />
    </div>
  );
}
