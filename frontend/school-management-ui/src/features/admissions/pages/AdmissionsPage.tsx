import { useMemo } from "react";
import { DeletedAdmissionsPanel } from "../components/DeletedAdmissionsPanel";
import {
  CircleCheck,
  CircleX,
  ClipboardList,
  FileText,
  GraduationCap,
} from "lucide-react";
import { admissionService } from "../services/admissionService";
import {
  ADMISSION_STATUS_TONE,
  isAdmissionDeletable,
  isAdmissionEditable,
  type AdmissionResponse,
  type AdmissionStatus,
} from "../types/admission.types";
import { PageHero } from "@/components/PageHero";
import { StatCard, StatGrid } from "@/components/StatCard";
import { Panel } from "@/components/Panel";
import {
  ClearFiltersButton,
  FilterBar,
  FilterDate,
  FilterSelect,
} from "@/components/FilterBar";
import { SearchBox } from "@/components/SearchBox";
import { StatusPills } from "@/components/StatusPills";
import { DataTable, type Column } from "@/components/DataTable";
import { Badge } from "@/components/Badge";
import { Notice } from "@/components/Notice";
import { ListEmptyState } from "@/components/ListEmptyState";
import { PersonCell } from "@/components/PersonCell";
import {
  ExportCsvButton,
  NewRecordLink,
  PrintButton,
  RowActions,
} from "@/components/ListButtons";
import { useAsyncData } from "@/hooks/useAsyncData";
import { useCan } from "@/hooks/useCan";
import { useConfirmAction } from "@/hooks/useConfirmAction";
import { useFilters } from "@/hooks/useFilters";
import { Permission } from "@/routes/permissions";
import { countBy, uniqueSorted } from "@/utils/collections";
import { downloadCsv } from "@/utils/csv";
import { formatDate } from "@/utils/format";

const STATUS_FILTERS: readonly (AdmissionStatus | "All")[] = [
  "All",
  "Registered",
  "Admitted",
  "Enrolled",
  "Rejected",
];

const fullName = (a: AdmissionResponse) => `${a.firstName} ${a.lastName}`;

/**
 * U2: the admissions list. Opening a row goes to the application's own page, where the decisions
 * (confirm, reject, enrol) are made. Registering opens the full-page form.
 */
export function AdmissionsPage() {
  const can = useCan();
  const canCreate = can(Permission.AdmissionsCreate);
  const canEdit = can(Permission.AdmissionsEdit);
  const canDelete = can(Permission.AdmissionsDelete);

  const list = useAsyncData(
    () => admissionService.getAll(),
    [],
    "Could not load applications.",
  );
  const admissions = useMemo(() => list.data ?? [], [list.data]);

  const filters = useFilters({
    status: "All",
    search: "",
    year: "",
    type: "",
    classSection: "",
    dateFrom: "",
    dateTo: "",
  });
  const f = filters.values;

  const years = useMemo(
    () => uniqueSorted(admissions.map((a) => a.academicYearName)),
    [admissions],
  );
  const classNames = useMemo(
    () => uniqueSorted(admissions.map((a) => a.appliedForClassSectionName)),
    [admissions],
  );
  const counts = useMemo(
    (): Record<string, number> => ({
      All: admissions.length,
      ...countBy(admissions, (a) => a.status),
    }),
    [admissions],
  );

  const filtered = useMemo(() => {
    const q = f.search.trim().toLowerCase();
    return admissions.filter((a) => {
      if (f.status !== "All" && a.status !== f.status) return false;
      if (f.year && a.academicYearName !== f.year) return false;
      if (f.type && a.admissionType !== f.type) return false;
      if (f.classSection && a.appliedForClassSectionName !== f.classSection)
        return false;
      if (f.dateFrom && a.registrationDate < f.dateFrom) return false;
      if (f.dateTo && a.registrationDate > f.dateTo) return false;
      if (q) {
        const haystack =
          `${fullName(a)} ${a.regNo} ${a.phone} ${a.email ?? ""}`.toLowerCase();
        if (!haystack.includes(q)) return false;
      }
      return true;
    });
  }, [admissions, f]);

  const remove = useConfirmAction<AdmissionResponse>({
    title: "Delete application",
    message: (a) =>
      `Delete the application for ${fullName(a)} (${a.regNo})? It is removed from this list and kept in the Deleted applications list.`,
    confirmLabel: "Delete",
    danger: true,
    run: async (a) => {
      await admissionService.delete(a.id);
      await list.reload();
    },
    success: (a) => `Application ${a.regNo} was deleted.`,
    failure: "Could not delete this application.",
  });

  const exportCsv = () =>
    downloadCsv(
      "admissions.csv",
      [
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
      ],
      filtered.map((a) => [
        a.regNo,
        fullName(a),
        a.gender,
        a.dateOfBirth,
        a.appliedForClassSectionName,
        a.admissionType,
        a.previousSchool,
        a.phone,
        a.email,
        a.registrationDate,
        a.admissionFee,
        a.status,
      ]),
    );

  const newLink = canCreate ? (
    <NewRecordLink to="/admissions/new" label="New registration" />
  ) : undefined;

  const columns: Column<AdmissionResponse>[] = [
    {
      key: "regNo",
      header: "Reg No",
      sortValue: (a) => a.regNo,
      render: (a) => <span className="cell-strong">{a.regNo}</span>,
    },
    {
      key: "applicant",
      header: "Applicant",
      sortValue: (a) => fullName(a),
      render: (a) => (
        <PersonCell
          name={fullName(a)}
          to={`/admissions/${a.id}`}
          sub={`${a.gender} · born ${formatDate(a.dateOfBirth)}`}
        />
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
        <RowActions
          name={`the application of ${fullName(a)}`}
          viewTo={`/admissions/${a.id}`}
          editTo={
            canEdit && isAdmissionEditable(a.status)
              ? `/admissions/${a.id}/edit`
              : undefined
          }
          onDelete={
            canDelete && isAdmissionDeletable(a.status)
              ? () => remove.ask(a)
              : undefined
          }
        />
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
            <PrintButton />
            {newLink}
          </>
        }
        stats={
          <StatGrid>
            <StatCard
              label="Registered"
              value={counts.Registered ?? 0}
              icon={ClipboardList}
              tone="info"
            />
            <StatCard
              label="Admitted"
              value={counts.Admitted ?? 0}
              icon={CircleCheck}
              tone="warn"
            />
            <StatCard
              label="Enrolled"
              value={counts.Enrolled ?? 0}
              icon={GraduationCap}
              tone="ok"
            />
            <StatCard
              label="Rejected"
              value={counts.Rejected ?? 0}
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
          <ExportCsvButton
            onClick={exportCsv}
            disabled={filtered.length === 0}
          />
        }
      >
        <StatusPills
          label="Filter by status"
          options={STATUS_FILTERS}
          value={f.status as AdmissionStatus | "All"}
          onChange={(v) => filters.set("status", v)}
          counts={counts}
        />

        <FilterBar>
          <FilterSelect
            label="Academic year"
            allLabel="All years"
            options={years}
            value={f.year}
            onChange={(v) => filters.set("year", v)}
          />
          <FilterSelect
            label="Admission type"
            allLabel="All types"
            options={["New", "Transfer"]}
            value={f.type}
            onChange={(v) => filters.set("type", v)}
          />
          <FilterSelect
            label="Applied-for class"
            allLabel="All classes"
            options={classNames}
            value={f.classSection}
            onChange={(v) => filters.set("classSection", v)}
          />
          <FilterDate
            label="From"
            value={f.dateFrom}
            onChange={(v) => filters.set("dateFrom", v)}
          />
          <FilterDate
            label="To"
            value={f.dateTo}
            onChange={(v) => filters.set("dateTo", v)}
          />
          <SearchBox
            label="Search applications"
            placeholder="Name, reg no., phone"
            value={f.search}
            onChange={(v) => filters.set("search", v)}
          />
          <ClearFiltersButton
            visible={filters.isActive}
            onClear={filters.clear}
          />
        </FilterBar>

        {list.error && (
          <Notice tone="warn" style={{ margin: 16 }}>
            {list.error}
          </Notice>
        )}
        <DataTable
          caption="Admission applications"
          columns={columns}
          rows={filtered}
          rowKey={(a) => a.id}
          loading={list.isLoading && !list.data}
          resetPageKey={filters.key}
          empty={
            <ListEmptyState
              icon={FileText}
              noun="applications"
              hasRecords={admissions.length > 0}
              onClearFilters={filters.clear}
              createAction={newLink}
            />
          }
        />
      </Panel>

      {canDelete && (
        <DeletedAdmissionsPanel
          version={admissions.length}
          onRestored={() => void list.reload()}
        />
      )}

      {remove.dialog}
    </div>
  );
}
