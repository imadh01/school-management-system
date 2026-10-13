import { useMemo } from "react";
import { Link } from "react-router-dom";
import { CircleCheck, Power, School, Users } from "lucide-react";
import { classSectionService } from "../services/classSectionService";
import {
  CLASS_MEDIUMS,
  CLASS_STAGES,
  type ClassSectionResponse,
} from "../types/classSection.types";
import { classLocation } from "../types/classSectionFormat";
import { PageHero } from "@/components/PageHero";
import { StatCard, StatGrid } from "@/components/StatCard";
import { Panel } from "@/components/Panel";
import {
  ClearFiltersButton,
  FilterBar,
  FilterSelect,
} from "@/components/FilterBar";
import { SearchBox } from "@/components/SearchBox";
import { StatusPills } from "@/components/StatusPills";
import { DataTable, type Column } from "@/components/DataTable";
import { StatusBadge } from "@/components/Badge";
import { IconButton } from "@/components/IconButton";
import { Notice } from "@/components/Notice";
import { ListEmptyState } from "@/components/ListEmptyState";
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

const STATUS_FILTERS = ["All", "Active", "Inactive"] as const;

/**
 * U2: the classes and sections list. Opening a class goes to its page (details, students,
 * subjects); adding and editing are full pages. Activating / deactivating stays a one-click action
 * with a confirmation.
 */
export function ClassSectionsPage() {
  const can = useCan();
  const canCreate = can(Permission.ClassSectionsCreate);
  const canEdit = can(Permission.ClassSectionsEdit);
  const canDelete = can(Permission.ClassSectionsDelete);

  const list = useAsyncData(
    () => classSectionService.getAll(),
    [],
    "Could not load classes.",
  );
  const rows = useMemo(() => list.data ?? [], [list.data]);

  const filters = useFilters({
    status: "All",
    year: "",
    section: "",
    stage: "",
    medium: "",
    search: "",
  });
  const f = filters.values;

  const years = useMemo(
    () => uniqueSorted(rows.map((c) => c.academicYearName)),
    [rows],
  );
  const sections = useMemo(
    () => uniqueSorted(rows.map((c) => c.section)),
    [rows],
  );
  const counts: Record<string, number> = useMemo(
    () => ({ All: rows.length, ...countBy(rows, (c) => c.status) }),
    [rows],
  );
  const enrolled = rows.reduce((n, c) => n + c.enrolled, 0);

  const filtered = useMemo(() => {
    const q = f.search.trim().toLowerCase();
    return rows.filter((c) => {
      if (f.status !== "All" && c.status !== f.status) return false;
      if (f.year && c.academicYearName !== f.year) return false;
      if (f.section && c.section !== f.section) return false;
      if (f.stage && c.stage !== f.stage) return false;
      if (f.medium && c.medium !== f.medium) return false;
      if (
        q &&
        !`${c.code} ${c.displayName} ${c.room ?? ""} ${c.building ?? ""}`
          .toLowerCase()
          .includes(q)
      )
        return false;
      return true;
    });
  }, [rows, f]);

  const remove = useConfirmAction<ClassSectionResponse>({
    title: "Delete class",
    message: (c) => `Delete ${c.displayName}? This cannot be undone.`,
    confirmLabel: "Delete",
    danger: true,
    run: async (c) => {
      await classSectionService.delete(c.id);
      await list.reload();
    },
    success: (c) => `${c.displayName} was deleted.`,
    failure:
      "Could not delete this class. It may still have students or subjects.",
  });
  const toggleStatus = useConfirmAction<ClassSectionResponse>({
    title: "Change status",
    message: (c) =>
      c.status === "Active"
        ? `Deactivate ${c.displayName}? It will no longer be offered for admissions and enrolment.`
        : `Activate ${c.displayName}?`,
    confirmLabel: "Confirm",
    run: async (c) => {
      await classSectionService.changeStatus(c.id, c.status !== "Active");
      await list.reload();
    },
    success: (c) =>
      `${c.displayName} was ${c.status === "Active" ? "deactivated" : "activated"}.`,
    failure: "Could not change the status of this class.",
  });

  const exportCsv = () =>
    downloadCsv(
      "classes-sections.csv",
      [
        "Code",
        "Class",
        "Section",
        "Academic Year",
        "Stage",
        "Medium",
        "Stream",
        "Capacity",
        "Enrolled",
        "Class Teacher",
        "Building",
        "Floor",
        "Room",
        "Status",
      ],
      filtered.map((c) => [
        c.code,
        c.name,
        c.section,
        c.academicYearName,
        c.stage,
        c.medium,
        c.stream,
        c.capacity,
        c.enrolled,
        c.classTeacherName,
        c.building,
        c.floor,
        c.room,
        c.status,
      ]),
    );

  const newLink = canCreate ? (
    <NewRecordLink to="/classes-sections/new" label="Add class" />
  ) : undefined;
  const muted = <span className="cell-muted">—</span>;

  const columns: Column<ClassSectionResponse>[] = [
    {
      key: "code",
      header: "Code",
      sortValue: (c) => c.code,
      render: (c) => <span className="cell-strong">{c.code}</span>,
    },
    {
      key: "class",
      header: "Class",
      sortValue: (c) => c.displayName,
      render: (c) => (
        <div>
          <Link
            className="person__name person__link"
            to={`/classes-sections/${c.id}`}
          >
            {c.displayName}
          </Link>
          <div className="person__sub">
            {[c.academicYearName, classLocation(c)].filter(Boolean).join(" · ")}
          </div>
        </div>
      ),
    },
    {
      key: "stage",
      header: "Stage",
      hideBelow: "md",
      sortValue: (c) => c.stage,
      render: (c) => c.stage,
    },
    {
      key: "strength",
      header: "Strength",
      sortValue: (c) => c.enrolled,
      render: (c) =>
        c.capacity ? `${c.enrolled} / ${c.capacity}` : c.enrolled,
    },
    {
      key: "teacher",
      header: "Class teacher",
      hideBelow: "md",
      sortValue: (c) => c.classTeacherName,
      render: (c) => c.classTeacherName ?? muted,
    },
    {
      key: "status",
      header: "Status",
      sortValue: (c) => c.status,
      render: (c) => <StatusBadge status={c.status} />,
    },
    {
      key: "actions",
      header: "Actions",
      render: (c) => (
        <RowActions
          name={c.displayName}
          viewTo={`/classes-sections/${c.id}`}
          editTo={canEdit ? `/classes-sections/${c.id}/edit` : undefined}
          onDelete={canDelete ? () => remove.ask(c) : undefined}
        >
          {canEdit && (
            <IconButton
              icon={Power}
              tone={c.status === "Active" ? "neutral" : "ok"}
              label={`${c.status === "Active" ? "Deactivate" : "Activate"} ${c.displayName}`}
              onClick={() => toggleStatus.ask(c)}
            />
          )}
        </RowActions>
      ),
    },
  ];

  return (
    <div>
      <PageHero
        title="Classes & Sections"
        subtitle="Classes and sections of each academic year, their capacity and class teachers."
        actions={
          <>
            <PrintButton />
            {newLink}
          </>
        }
        stats={
          <StatGrid>
            <StatCard
              label="Classes"
              value={rows.length}
              icon={School}
              tone="info"
            />
            <StatCard
              label="Active"
              value={counts.Active ?? 0}
              icon={CircleCheck}
              tone="ok"
            />
            <StatCard
              label="Students enrolled"
              value={enrolled}
              icon={Users}
              tone="purple"
            />
          </StatGrid>
        }
      />

      <Panel
        title="All classes"
        subtitle={`${rows.length} classes · ${filtered.length} shown`}
        icon={School}
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
          value={f.status as (typeof STATUS_FILTERS)[number]}
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
            label="Section"
            allLabel="All sections"
            options={sections}
            value={f.section}
            onChange={(v) => filters.set("section", v)}
          />
          <FilterSelect
            label="Stage"
            allLabel="All stages"
            options={CLASS_STAGES}
            value={f.stage}
            onChange={(v) => filters.set("stage", v)}
          />
          <FilterSelect
            label="Medium"
            allLabel="All media"
            options={CLASS_MEDIUMS}
            value={f.medium}
            onChange={(v) => filters.set("medium", v)}
          />
          <SearchBox
            label="Search classes"
            placeholder="Code, class, room"
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
          caption="Classes and sections"
          columns={columns}
          rows={filtered}
          rowKey={(c) => c.id}
          loading={list.isLoading && !list.data}
          resetPageKey={filters.key}
          empty={
            <ListEmptyState
              icon={School}
              noun="classes"
              hasRecords={rows.length > 0}
              onClearFilters={filters.clear}
              createAction={newLink}
            />
          }
        />
      </Panel>

      {remove.dialog}
      {toggleStatus.dialog}
    </div>
  );
}
