import { useEffect, useMemo, useState } from "react";
import { admissionService } from "../services/admissionService";
import { classSectionService } from "@/features/class-sections/services/classSectionService";
import type {
  AdmissionResponse,
  AdmissionStatus,
} from "../types/admission.types";
import type { ClassSectionResponse } from "@/features/class-sections/types/classSection.types";
import { RegistrationModal } from "../components/RegistrationModal";
import { ConfirmAdmissionModal } from "../components/ConfirmAdmissionModal";
import { EnrollModal } from "../components/EnrollModal";
import { ViewReasonModal } from "../components/ViewReasonModal";
import { CreateStudentModal } from "@/features/students/components/CreateStudentModal";

const PIPELINE_STEPS: {
  key: AdmissionStatus | "All";
  label: string;
  activeClass: string;
}[] = [
  { key: "All", label: "All", activeClass: "pipeline__step--dark" },
  {
    key: "Registered",
    label: "Registered",
    activeClass: "pipeline__step--active",
  },
  { key: "Admitted", label: "Admitted", activeClass: "pipeline__step--amber" },
  { key: "Enrolled", label: "Enrolled", activeClass: "pipeline__step--green" },
  { key: "Rejected", label: "Rejected", activeClass: "" },
];

const STATUS_BADGE_CLASS: Record<AdmissionStatus, string> = {
  Registered: "badge--info",
  Admitted: "badge--warn",
  Enrolled: "badge--active",
  Rejected: "badge--inactive",
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

function toCsv(rows: AdmissionResponse[]): string {
  const headers = [
    "Reg No",
    "Applicant",
    "Gender",
    "DOB",
    "Applied For",
    "Type",
    "Previous School",
    "Phone",
    "Email",
    "Reg Date",
    "Admission Fee",
    "Status",
  ];
  const lines = rows.map((a) =>
    [
      a.regNo,
      `${a.firstName} ${a.lastName}`,
      a.gender,
      a.dateOfBirth,
      a.appliedForClassSectionName,
      a.admissionType,
      a.previousSchool ?? "",
      a.phone,
      a.email ?? "",
      a.registrationDate,
      a.admissionFee ?? "",
      a.status,
    ]
      .map((v) => `"${String(v).replace(/"/g, '""')}"`)
      .join(","),
  );
  return [headers.join(","), ...lines].join("\n");
}

export function AdmissionsPage() {
  const [admissions, setAdmissions] = useState<AdmissionResponse[]>([]);
  const [classSections, setClassSections] = useState<ClassSectionResponse[]>(
    [],
  );
  const [statusFilter, setStatusFilter] = useState<AdmissionStatus | "All">(
    "All",
  );
  const [search, setSearch] = useState("");
  const [quickSearch, setQuickSearch] = useState("");
  const [yearFilter, setYearFilter] = useState("");
  const [typeFilter, setTypeFilter] = useState("");
  const [classFilter, setClassFilter] = useState("");
  const [dateFrom, setDateFrom] = useState("");
  const [dateTo, setDateTo] = useState("");
  const [pageSize, setPageSize] = useState(10);
  const [page, setPage] = useState(1);
  const [isLoading, setIsLoading] = useState(true);

  const [isRegistrationOpen, setIsRegistrationOpen] = useState(false);
  const [editingAdmission, setEditingAdmission] =
    useState<AdmissionResponse | null>(null);
  const [confirmTarget, setConfirmTarget] = useState<AdmissionResponse | null>(
    null,
  );
  const [enrollTarget, setEnrollTarget] = useState<AdmissionResponse | null>(
    null,
  );
  const [createStudentTarget, setCreateStudentTarget] =
    useState<AdmissionResponse | null>(null);
  const [viewReason, setViewReason] = useState<string | null>(null);

  const loadData = async () => {
    setIsLoading(true);
    const [admissionsData, classSectionsData] = await Promise.all([
      admissionService.getAll(),
      classSectionService.getAll(),
    ]);
    setAdmissions(admissionsData);
    setClassSections(classSectionsData);
    setIsLoading(false);
  };

  useEffect(() => {
    loadData();
  }, []);

  const years = useMemo(
    () => unique(admissions.map((a) => a.academicYearName)),
    [admissions],
  );
  const classNames = useMemo(
    () => unique(admissions.map((a) => a.appliedForClassSectionName)),
    [admissions],
  );

  const counts = useMemo(() => {
    const c: Record<string, number> = {
      All: admissions.length,
      Registered: 0,
      Admitted: 0,
      Enrolled: 0,
      Rejected: 0,
    };
    admissions.forEach((a) => {
      c[a.status] = (c[a.status] ?? 0) + 1;
    });
    return c;
  }, [admissions]);

  const filtered = useMemo(() => {
    return admissions.filter((a) => {
      if (statusFilter !== "All" && a.status !== statusFilter) return false;
      if (yearFilter && a.academicYearName !== yearFilter) return false;
      if (typeFilter && a.admissionType !== typeFilter) return false;
      if (classFilter && a.appliedForClassSectionName !== classFilter)
        return false;
      if (dateFrom && a.registrationDate < dateFrom) return false;
      if (dateTo && a.registrationDate > dateTo) return false;

      if (search) {
        const haystack =
          `${a.firstName} ${a.lastName} ${a.regNo}`.toLowerCase();
        if (!haystack.includes(search.toLowerCase())) return false;
      }
      if (quickSearch) {
        const everything =
          `${a.firstName} ${a.lastName} ${a.regNo} ${a.email ?? ""} ${a.phone} ${a.appliedForClassSectionName} ${a.status}`.toLowerCase();
        if (!everything.includes(quickSearch.toLowerCase())) return false;
      }
      return true;
    });
  }, [
    admissions,
    statusFilter,
    search,
    quickSearch,
    yearFilter,
    typeFilter,
    classFilter,
    dateFrom,
    dateTo,
  ]);

  const totalPages = Math.max(1, Math.ceil(filtered.length / pageSize));
  const pageRows = filtered.slice((page - 1) * pageSize, page * pageSize);

  useEffect(() => {
    setPage(1);
  }, [
    statusFilter,
    search,
    quickSearch,
    yearFilter,
    typeFilter,
    classFilter,
    dateFrom,
    dateTo,
    pageSize,
  ]);

  const clearFilters = () => {
    setSearch("");
    setQuickSearch("");
    setYearFilter("");
    setTypeFilter("");
    setClassFilter("");
    setDateFrom("");
    setDateTo("");
    setStatusFilter("All");
  };

  const openCreate = () => {
    setEditingAdmission(null);
    setIsRegistrationOpen(true);
  };
  const openEdit = (a: AdmissionResponse) => {
    setEditingAdmission(a);
    setIsRegistrationOpen(true);
  };

  const handleReject = async (a: AdmissionResponse) => {
    const reason = window.prompt(
      `Reason for rejecting ${a.firstName} ${a.lastName} (optional):`,
      "",
    );
    if (reason === null) return;
    await admissionService.reject(a.id, { rejectionReason: reason.trim() });
    await loadData();
  };

  const handleDelete = async (a: AdmissionResponse) => {
    if (
      !window.confirm(
        `Delete the application for ${a.firstName} ${a.lastName} (${a.regNo})? This cannot be undone.`,
      )
    )
      return;
    await admissionService.delete(a.id);
    await loadData();
  };

  const handleExportCsv = () => {
    const blob = new Blob([toCsv(filtered)], {
      type: "text/csv;charset=utf-8;",
    });
    const url = URL.createObjectURL(blob);
    const link = document.createElement("a");
    link.href = url;
    link.download = "admissions.csv";
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
          + New Registration
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
            placeholder="Search applicant, reg no..."
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
              <option key={y} value={y}>
                {y}
              </option>
            ))}
          </select>
        </div>
        <div className="field">
          <label>Admission Type</label>
          <select
            value={typeFilter}
            onChange={(e) => setTypeFilter(e.target.value)}
          >
            <option value="">All Types</option>
            <option value="New">New</option>
            <option value="Transfer">Transfer</option>
          </select>
        </div>
        <div className="field">
          <label>Applied-for Class</label>
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
          <label>Reg. Date From</label>
          <input
            type="date"
            value={dateFrom}
            onChange={(e) => setDateFrom(e.target.value)}
          />
        </div>
        <div className="field">
          <label>Reg. Date To</label>
          <input
            type="date"
            value={dateTo}
            onChange={(e) => setDateTo(e.target.value)}
          />
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
              <option value={50}>50</option>
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
        <div className="empty-state">No applications match these filters.</div>
      ) : (
        <div style={{ overflowX: "auto" }}>
          <table>
            <thead>
              <tr>
                <th>Reg. No.</th>
                <th>Applicant</th>
                <th>Gender</th>
                <th>DOB</th>
                <th>Applied For</th>
                <th>Type</th>
                <th>Previous School</th>
                <th>Contact</th>
                <th>Reg. Date</th>
                <th>Admission Fee</th>
                <th>Status</th>
                <th>Actions</th>
              </tr>
            </thead>
            <tbody>
              {pageRows.map((a) => (
                <tr key={a.id}>
                  <td>{a.regNo}</td>
                  <td>
                    {a.firstName} {a.lastName}
                  </td>
                  <td>{a.gender}</td>
                  <td>{a.dateOfBirth}</td>
                  <td>{a.appliedForClassSectionName}</td>
                  <td>
                    <span
                      className={`badge ${a.admissionType === "Transfer" ? "badge--warn" : "badge--info"}`}
                    >
                      {a.admissionType}
                    </span>
                  </td>
                  <td>{a.previousSchool || "—"}</td>
                  <td>
                    {a.phone}
                    <br />
                    <span style={{ color: "var(--muted)", fontSize: 11.5 }}>
                      {a.email}
                    </span>
                  </td>
                  <td>{a.registrationDate}</td>
                  <td>
                    {a.admissionFee ? (
                      <>
                        {a.admissionFee}
                        <br />
                        <span style={{ color: "var(--muted)", fontSize: 11 }}>
                          {a.admissionFeeReference}
                        </span>
                      </>
                    ) : (
                      "—"
                    )}
                  </td>
                  <td>
                    <span className={`badge ${STATUS_BADGE_CLASS[a.status]}`}>
                      {a.status}
                    </span>
                  </td>
                  <td style={{ whiteSpace: "nowrap" }}>
                    {a.status === "Rejected" ? (
                      <>
                        <button
                          className="btn--icon"
                          title="View Reason"
                          style={{ color: "var(--amber)" }}
                          onClick={() =>
                            setViewReason(
                              a.rejectionReason || "No reason recorded.",
                            )
                          }
                        >
                          👁
                        </button>
                        <button
                          className="btn--icon danger"
                          title="Delete"
                          onClick={() => handleDelete(a)}
                        >
                          🗑
                        </button>
                      </>
                    ) : (
                      <>
                        <button
                          className="btn--icon"
                          title="Edit"
                          onClick={() => openEdit(a)}
                        >
                          ✎
                        </button>
                        {a.status === "Registered" && (
                          <button
                            className="btn--icon"
                            title="Confirm Admission"
                            style={{ color: "var(--green)" }}
                            onClick={() => setConfirmTarget(a)}
                          >
                            ✓
                          </button>
                        )}
                        {a.status === "Admitted" && (
                          <button
                            className="btn--icon"
                            title="Enroll to Class"
                            style={{ color: "var(--blue)" }}
                            onClick={() => setEnrollTarget(a)}
                          >
                            🎓
                          </button>
                        )}
                        {a.status === "Enrolled" && !a.studentId && (
                          <button
                            className="btn--icon"
                            title="Create Student Record"
                            onClick={() => setCreateStudentTarget(a)}
                          >
                            🧑‍🎓
                          </button>
                        )}
                        {a.studentId && (
                          <span title="Student record created">🔗</span>
                        )}
                        {(a.status === "Registered" ||
                          a.status === "Admitted") && (
                          <button
                            className="btn--icon danger"
                            title="Reject"
                            onClick={() => handleReject(a)}
                          >
                            ⊘
                          </button>
                        )}
                        <button
                          className="btn--icon danger"
                          title="Delete"
                          onClick={() => handleDelete(a)}
                        >
                          🗑
                        </button>
                      </>
                    )}
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

      <RegistrationModal
        isOpen={isRegistrationOpen}
        editingAdmission={editingAdmission}
        classSections={classSections}
        onClose={() => setIsRegistrationOpen(false)}
        onSubmit={async (data) => {
          if (editingAdmission) {
            await admissionService.update(editingAdmission.id, data);
          } else {
            await admissionService.create(data);
          }
          await loadData();
        }}
      />

      <ConfirmAdmissionModal
        admission={confirmTarget}
        onClose={() => setConfirmTarget(null)}
        onSubmit={async (id, data) => {
          await admissionService.confirmAdmission(id, data);
          await loadData();
        }}
      />

      <EnrollModal
        admission={enrollTarget}
        classSections={classSections}
        onClose={() => setEnrollTarget(null)}
        onSubmit={async (id, data) => {
          await admissionService.enroll(id, data);
          await loadData();
        }}
      />

      <CreateStudentModal
        admission={createStudentTarget}
        onClose={() => setCreateStudentTarget(null)}
        onSubmit={async (id, data) => {
          await admissionService.createStudent(id, data);
          await loadData();
        }}
      />

      <ViewReasonModal
        reason={viewReason}
        onClose={() => setViewReason(null)}
      />
    </div>
  );
}
