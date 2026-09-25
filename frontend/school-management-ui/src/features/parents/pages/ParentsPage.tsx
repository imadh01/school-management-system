import { useEffect, useMemo, useState } from "react";
import { parentService } from "../services/parentService";
import type { ParentResponse } from "../types/parent.types";
import { ParentModal } from "../components/ParentModal";
import { LinkedChildrenModal } from "../components/LinkedChildrenModal";

function initials(name: string) {
  return (name || "?").trim().charAt(0).toUpperCase();
}

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

  const handleView = (p: ParentResponse) => {
    alert(
      `${p.name}\n` +
        `Status: ${p.status}\n` +
        `Mobile: ${p.mobile}${p.email ? " · " + p.email : ""}\n` +
        `Preferred contact: ${p.preferredContactMethod ?? "—"} (${p.preferredLanguage ?? "—"})`,
    );
  };

  const handleDelete = async (p: ParentResponse) => {
    if (!confirm(`Delete ${p.name}? This cannot be undone.`)) return;
    await parentService.delete(p.id);
    await loadData();
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
      <div
        style={{
          display: "flex",
          justifyContent: "space-between",
          alignItems: "center",
          marginBottom: 16,
        }}
      >
        <h1 style={{ fontSize: 19, margin: 0 }}>Parents Management</h1>
        <button
          className="btn btn--primary"
          onClick={() => {
            setEditingParent(null);
            setIsModalOpen(true);
          }}
        >
          + Add Parent
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
            <div className="empty-state">No parents match these filters.</div>
          ) : (
            <table>
              <thead>
                <tr>
                  <th>ID</th>
                  <th>Parent</th>
                  <th>Employer</th>
                  <th>Contact</th>
                  <th>Method</th>
                  <th>Status</th>
                  <th>Actions</th>
                </tr>
              </thead>
              <tbody>
                {pageRows.map((p) => (
                  <tr key={p.id}>
                    <td>{p.id}</td>
                    <td>
                      <span className="avatar-circle">{initials(p.name)}</span>
                      {p.name}
                    </td>
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
                        style={{ color: "var(--blue)" }}
                        onClick={() => setLinkedChildrenParent(p)}
                      >
                        👪
                      </button>
                      <button
                        className="btn--icon"
                        title="Fee Records"
                        style={{ color: "var(--green)" }}
                        onClick={() => setFeeRecordsParent(p)}
                      >
                        💳
                      </button>
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
                      <button
                        className="btn--icon danger"
                        title="Delete"
                        onClick={() => handleDelete(p)}
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

      <ParentModal
        isOpen={isModalOpen}
        editingParent={editingParent}
        onClose={() => setIsModalOpen(false)}
        onCreate={async (data) => {
          await parentService.create(data);
          await loadData();
        }}
        onUpdate={async (id, data) => {
          await parentService.update(id, data);
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
                <span className="avatar-circle avatar-circle--lg">
                  {initials(feeRecordsParent.name)}
                </span>
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
    </div>
  );
}
