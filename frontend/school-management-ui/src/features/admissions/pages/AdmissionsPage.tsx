import { useEffect, useMemo, useState } from "react";
import { Link, useNavigate } from "react-router-dom";
import {
  CircleCheck,
  ClipboardList,
  Download,
  Eye,
  FileText,
  GraduationCap,
  Pencil,
  Plus,
  Printer,
  Search,
  Trash,
  CircleX,
} from "lucide-react";
import { admissionService } from "../services/admissionService";
import {
  ADMISSION_STATUS_TONE,
  isAdmissionEditable,
  type AdmissionResponse,
  type AdmissionStatus,
} from "../types/admission.types";
import { PageHero } from "@/components/PageHero";
import { StatCard, StatGrid } from "@/components/StatCard";
import { Panel } from "@/components/Panel";
import { FilterBar } from "@/components/FilterBar";
import { DataTable, type Column } from "@/components/DataTable";
import { Badge } from "@/components/Badge";
import { IconButton } from "@/components/IconButton";
import { EmptyState } from "@/components/EmptyState";
import { ConfirmDialog } from "@/components/ConfirmDialog";
import { useToast } from "@/components/Toast";
import { getApiErrors, getReadErrors } from "@/utils/apiError";
import { useAuth } from "@/store/AuthContext";
import { Permission } from "@/routes/permissions";

const STATUS_FILTERS: (AdmissionStatus | "All")[] = [
  "All",
  "Registered",
  "Admitted",
  "Enrolled",
  "Rejected",
];

function unique(values: (string | null)[]): string[] {
  return [...new Set(values.filter((v): v is string => !!v))].sort();
}

function formatDate(iso: string): string {
  const d = new Date(`${iso}T00:00:00`);
  return Number.isNaN(d.getTime())
    ? iso
    : d.toLocaleDateString("en-GB", {
        day: "numeric",
        month: "short",
        year: "numeric",
      });
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

/**
 * U2: the admissions list. Opening a row goes to the application's own page, where the decisions
 * (confirm, reject, enrol) are made. Registering opens the full-page form.
 */
export function AdmissionsPage() {
  const toast = useToast();
  const navigate = useNavigate();
  const { hasAnyPermission } = useAuth();
  const canCreate = hasAnyPermission([Permission.AdmissionsCreate]);
  const canEdit = hasAnyPermission([Permission.AdmissionsEdit]);
  const canDelete = hasAnyPermission([Permission.AdmissionsDelete]);

  const [admissions, setAdmissions] = useState<AdmissionResponse[]>([]);
  const [isLoading, setIsLoading] = useState(true);
  const [statusFilter, setStatusFilter] = useState<AdmissionStatus | "All">(
    "All",
  );
  const [search, setSearch] = useState("");
  const [yearFilter, setYearFilter] = useState("");
  const [typeFilter, setTypeFilter] = useState("");
  const [classFilter, setClassFilter] = useState("");
  const [dateFrom, setDateFrom] = useState("");
  const [dateTo, setDateTo] = useState("");
  const [deleteTarget, setDeleteTarget] = useState<AdmissionResponse | null>(
    null,
  );

  const loadData = async () => {
    try {
      setAdmissions(await admissionService.getAll());
    } catch (err) {
      toast.error(getReadErrors(err, "Could not load applications.")[0]);
    } finally {
      setIsLoading(false);
    }
  };

  useEffect(() => {
    void loadData();
    // Load once. (loadData uses toast, whose object changes on every message: listing it would reload in a loop.)
    // eslint-disable-next-line react-hooks/exhaustive-deps
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
    const c: Record<AdmissionStatus | "All", number> = {
      All: admissions.length,
      Registered: 0,
      Admitted: 0,
      Enrolled: 0,
      Rejected: 0,
    };
    admissions.forEach((a) => (c[a.status] += 1));
    return c;
  }, [admissions]);

  const filtered = useMemo(() => {
    const q = search.trim().toLowerCase();
    return admissions.filter((a) => {
      if (statusFilter !== "All" && a.status !== statusFilter) return false;
      if (yearFilter && a.academicYearName !== yearFilter) return false;
      if (typeFilter && a.admissionType !== typeFilter) return false;
      if (classFilter && a.appliedForClassSectionName !== classFilter)
        return false;
      if (dateFrom && a.registrationDate < dateFrom) return false;
      if (dateTo && a.registrationDate > dateTo) return false;
      if (q) {
        const haystack =
          `${a.firstName} ${a.lastName} ${a.regNo} ${a.phone} ${a.email ?? ""}`.toLowerCase();
        if (!haystack.includes(q)) return false;
      }
      return true;
    });
  }, [
    admissions,
    statusFilter,
    search,
    yearFilter,
    typeFilter,
    classFilter,
    dateFrom,
    dateTo,
  ]);

  const filterKey = [
    statusFilter,
    search,
    yearFilter,
    typeFilter,
    classFilter,
    dateFrom,
    dateTo,
  ].join("|");
  const anyFilter =
    statusFilter !== "All" ||
    !!(search || yearFilter || typeFilter || classFilter || dateFrom || dateTo);

  const clearFilters = () => {
    setStatusFilter("All");
    setSearch("");
    setYearFilter("");
    setTypeFilter("");
    setClassFilter("");
    setDateFrom("");
    setDateTo("");
  };

  const confirmDelete = async () => {
    const target = deleteTarget;
    setDeleteTarget(null);
    if (!target) return;
    try {
      await admissionService.delete(target.id);
      toast.success(`Application ${target.regNo} was deleted.`);
      await loadData();
    } catch (err) {
      toast.error(getApiErrors(err, "Could not delete this application.")[0]);
    }
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

  const fullName = (a: AdmissionResponse) => `${a.firstName} ${a.lastName}`;

  const columns: Column<AdmissionResponse>[] = [
    {
      key: "regNo",
      header: "Reg #",
      sortValue: (a) => a.regNo,
      render: (a) => <span className="cell-strong">{a.regNo}</span>,
    },
    {
      key: "applicant",
      header: "Applicant",
      sortValue: (a) => fullName(a),
      render: (a) => (
        <div>
          <Link
            className="person__name person__link"
            to={`/admissions/${a.id}`}
          >
            {fullName(a)}
          </Link>
          <div className="person__sub">
            {a.gender} · born {formatDate(a.dateOfBirth)}
          </div>
        </div>
      ),
    },
    {
      key: "class",
      header: "Applied for",
      sortValue: (a) => a.appliedForClassSectionName,
      render: (a) => a.appliedForClassSectionName,
    },
    {
      key: "type",
      header: "Type",
      hideBelow: "md",
      sortValue: (a) => a.admissionType,
      render: (a) => (
        <Badge tone={a.admissionType === "Transfer" ? "warn" : "neutral"}>
          {a.admissionType}
        </Badge>
      ),
    },
    {
      key: "regDate",
      header: "Registered",
      hideBelow: "md",
      sortValue: (a) => a.registrationDate,
      render: (a) => formatDate(a.registrationDate),
    },
    {
      key: "status",
      header: "Status",
      sortValue: (a) => a.status,
      render: (a) => (
        <Badge tone={ADMISSION_STATUS_TONE[a.status]}>{a.status}</Badge>
      ),
    },
    {
      key: "actions",
      header: "Actions",
      render: (a) => (
        <div className="row-actions">
          <IconButton
            icon={Eye}
            label={`Open the application of ${fullName(a)}`}
            onClick={() => navigate(`/admissions/${a.id}`)}
          />
          {canEdit && isAdmissionEditable(a.status) && (
            <IconButton
              icon={Pencil}
              tone="info"
              label={`Edit the application of ${fullName(a)}`}
              onClick={() => navigate(`/admissions/${a.id}/edit`)}
            />
          )}
          {canDelete && (
            <IconButton
              icon={Trash}
              tone="bad"
              label={`Delete the application of ${fullName(a)}`}
              onClick={() => setDeleteTarget(a)}
            />
          )}
        </div>
      ),
    },
  ];

  return (
    <div>
      <PageHero
        title="Admissions"
        subtitle="Applications from registration to confirmation and enrolment."
        actions={
          <>
            <button
              type="button"
              className="btn btn--secondary"
              onClick={() => window.print()}
            >
              <Printer size={16} aria-hidden="true" />
              Print
            </button>
            {canCreate && (
              <Link className="btn btn--primary" to="/admissions/new">
                <Plus size={16} aria-hidden="true" />
                New registration
              </Link>
            )}
          </>
        }
        stats={
          <StatGrid>
            <StatCard
              label="Registered"
              value={counts.Registered}
              icon={ClipboardList}
              tone="info"
            />
            <StatCard
              label="Admitted"
              value={counts.Admitted}
              icon={CircleCheck}
              tone="warn"
            />
            <StatCard
              label="Enrolled"
              value={counts.Enrolled}
              icon={GraduationCap}
              tone="ok"
            />
            <StatCard
              label="Rejected"
              value={counts.Rejected}
              icon={CircleX}
              tone="bad"
            />
          </StatGrid>
        }
      />

      <Panel
        title="All applications"
        subtitle={`${admissions.length} applications · ${filtered.length} shown`}
        icon={FileText}
        actions={
          <button
            type="button"
            className="btn btn--secondary"
            onClick={handleExportCsv}
            disabled={filtered.length === 0}
          >
            <Download size={16} aria-hidden="true" />
            Export CSV
          </button>
        }
      >
        <div className="pipeline" role="group" aria-label="Filter by status">
          {STATUS_FILTERS.map((s) => (
            <button
              key={s}
              type="button"
              aria-pressed={statusFilter === s}
              className={`pipeline__step ${statusFilter === s ? "pipeline__step--active" : ""}`}
              onClick={() => setStatusFilter(s)}
            >
              {s} ({counts[s]})
            </button>
          ))}
        </div>

        <FilterBar>
          <label>
            <span className="visually-hidden">Academic year</span>
            <select
              className="select"
              value={yearFilter}
              onChange={(e) => setYearFilter(e.target.value)}
            >
              <option value="">All years</option>
              {years.map((y) => (
                <option key={y}>{y}</option>
              ))}
            </select>
          </label>
          <label>
            <span className="visually-hidden">Admission type</span>
            <select
              className="select"
              value={typeFilter}
              onChange={(e) => setTypeFilter(e.target.value)}
            >
              <option value="">All types</option>
              <option>New</option>
              <option>Transfer</option>
            </select>
          </label>
          <label>
            <span className="visually-hidden">Applied-for class</span>
            <select
              className="select"
              value={classFilter}
              onChange={(e) => setClassFilter(e.target.value)}
            >
              <option value="">All classes</option>
              {classNames.map((c) => (
                <option key={c}>{c}</option>
              ))}
            </select>
          </label>
          <label className="filter-date">
            <span>From</span>
            <input
              className="select"
              type="date"
              value={dateFrom}
              onChange={(e) => setDateFrom(e.target.value)}
            />
          </label>
          <label className="filter-date">
            <span>To</span>
            <input
              className="select"
              type="date"
              value={dateTo}
              onChange={(e) => setDateTo(e.target.value)}
            />
          </label>
          <label
            className="search-box"
            style={{
              display: "flex",
              alignItems: "center",
              gap: 8,
              marginLeft: "auto",
            }}
          >
            <Search size={16} aria-hidden="true" />
            <span className="visually-hidden">Search applications</span>
            <input
              type="search"
              placeholder="Name, reg no., phone"
              value={search}
              onChange={(e) => setSearch(e.target.value)}
              style={{
                border: 0,
                outline: "none",
                background: "transparent",
                color: "inherit",
                font: "inherit",
                width: "100%",
              }}
            />
          </label>
          {anyFilter && (
            <button
              type="button"
              className="btn btn--secondary btn--sm"
              onClick={clearFilters}
            >
              Clear
            </button>
          )}
        </FilterBar>

        <DataTable
          caption="Admission applications"
          columns={columns}
          rows={filtered}
          rowKey={(a) => a.id}
          loading={isLoading}
          resetPageKey={filterKey}
          empty={
            <EmptyState
              icon={FileText}
              title={
                admissions.length === 0
                  ? "No applications yet"
                  : "No applications match these filters"
              }
              action={
                admissions.length === 0 && canCreate ? (
                  <Link className="btn btn--primary" to="/admissions/new">
                    <Plus size={16} aria-hidden="true" />
                    New registration
                  </Link>
                ) : admissions.length > 0 ? (
                  <button
                    type="button"
                    className="btn btn--secondary"
                    onClick={clearFilters}
                  >
                    Clear filters
                  </button>
                ) : undefined
              }
            />
          }
        />
      </Panel>

      <ConfirmDialog
        isOpen={deleteTarget !== null}
        title="Delete application"
        message={
          deleteTarget
            ? `Delete the application for ${fullName(deleteTarget)} (${deleteTarget.regNo})? This cannot be undone.`
            : ""
        }
        confirmLabel="Delete"
        danger
        onConfirm={confirmDelete}
        onCancel={() => setDeleteTarget(null)}
      />
    </div>
  );
}
