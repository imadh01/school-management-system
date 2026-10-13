import { useMemo } from "react";
import { CircleCheck, Star, UserRound } from "lucide-react";
import { teacherService } from "../services/teacherService";
import { TEACHER_STATUSES, type TeacherResponse } from "../types/teacher.types";
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
import { Badge, StatusBadge } from "@/components/Badge";
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

const STATUS_FILTERS = ["All", ...TEACHER_STATUSES] as const;

/**
 * U2: the teachers list. Opening a teacher goes to their page (profile and class/subject
 * assignments); adding and editing are full pages.
 */
export function TeachersPage() {
  const can = useCan();
  const canCreate = can(Permission.TeachersCreate);
  const canEdit = can(Permission.TeachersEdit);
  const canDelete = can(Permission.TeachersDelete);

  const list = useAsyncData(
    () => teacherService.getAll(),
    [],
    "Could not load teachers.",
  );
  const teachers = useMemo(() => list.data ?? [], [list.data]);

  const filters = useFilters({ status: "All", specialization: "", search: "" });
  const f = filters.values;

  const specializations = useMemo(
    () => uniqueSorted(teachers.map((t) => t.specialization)),
    [teachers],
  );
  const counts: Record<string, number> = useMemo(
    () => ({ All: teachers.length, ...countBy(teachers, (t) => t.status) }),
    [teachers],
  );
  const classTeachers = teachers.filter(
    (t) => t.classTeacherOfCount > 0,
  ).length;

  const filtered = useMemo(() => {
    const q = f.search.trim().toLowerCase();
    return teachers.filter((t) => {
      if (f.status !== "All" && t.status !== f.status) return false;
      if (f.specialization && t.specialization !== f.specialization)
        return false;
      if (
        q &&
        !`${t.name} ${t.username} ${t.email} ${t.specialization ?? ""}`
          .toLowerCase()
          .includes(q)
      )
        return false;
      return true;
    });
  }, [teachers, f]);

  const remove = useConfirmAction<TeacherResponse>({
    title: "Delete teacher",
    message: (t) =>
      `Delete ${t.name}? Their login is removed too. This cannot be undone.`,
    confirmLabel: "Delete",
    danger: true,
    run: async (t) => {
      await teacherService.delete(t.id);
      await list.reload();
    },
    success: (t) => `${t.name} was deleted.`,
    failure: "Could not delete this teacher.",
  });

  const exportCsv = () =>
    downloadCsv(
      "teachers.csv",
      [
        "Name",
        "Username",
        "Email",
        "Phone",
        "Specialization",
        "Classes",
        "Subjects",
        "Class Teacher Of",
        "Status",
      ],
      filtered.map((t) => [
        t.name,
        t.username,
        t.email,
        t.phone,
        t.specialization,
        t.classCount,
        t.subjectCount,
        t.classTeacherOfCount,
        t.status,
      ]),
    );

  const newLink = canCreate ? (
    <NewRecordLink to="/teachers/new" label="Add teacher" />
  ) : undefined;

  const columns: Column<TeacherResponse>[] = [
    {
      key: "name",
      header: "Teacher",
      sortValue: (t) => t.name,
      render: (t) => (
        <PersonCell name={t.name} to={`/teachers/${t.id}`} sub={t.username} />
      ),
    },
    {
      key: "contact",
      header: "Contact",
      hideBelow: "md",
      sortValue: (t) => t.email,
      render: (t) => (
        <div>
          <div>{t.email}</div>
          {t.phone && <div className="person__sub">{t.phone}</div>}
        </div>
      ),
    },
    {
      key: "specialization",
      header: "Specialization",
      sortValue: (t) => t.specialization,
      render: (t) => t.specialization ?? <span className="cell-muted">—</span>,
    },
    {
      key: "classes",
      header: "Classes",
      sortValue: (t) => t.classCount,
      render: (t) => (
        <>
          {t.classCount}{" "}
          {t.classTeacherOfCount > 0 && (
            <Badge tone="warn">Class teacher of {t.classTeacherOfCount}</Badge>
          )}
        </>
      ),
    },
    {
      key: "subjects",
      header: "Subjects",
      hideBelow: "md",
      sortValue: (t) => t.subjectCount,
      render: (t) => t.subjectCount,
    },
    {
      key: "status",
      header: "Status",
      sortValue: (t) => t.status,
      render: (t) => <StatusBadge status={t.status} />,
    },
    {
      key: "actions",
      header: "Actions",
      render: (t) => (
        <RowActions
          name={t.name}
          viewTo={`/teachers/${t.id}`}
          editTo={canEdit ? `/teachers/${t.id}/edit` : undefined}
          onDelete={canDelete ? () => remove.ask(t) : undefined}
        />
      ),
    },
  ];

  return (
    <div>
      <PageHero
        title="Teachers"
        subtitle="Teaching staff, the subjects they teach and their class-teacher duties."
        actions={
          <>
            <PrintButton />
            {newLink}
          </>
        }
        stats={
          <StatGrid>
            <StatCard
              label="Teachers"
              value={teachers.length}
              icon={UserRound}
              tone="info"
            />
            <StatCard
              label="Active"
              value={counts.Active ?? 0}
              icon={CircleCheck}
              tone="ok"
            />
            <StatCard
              label="Class teachers"
              value={classTeachers}
              icon={Star}
              tone="accent"
            />
          </StatGrid>
        }
      />

      <Panel
        title="All teachers"
        subtitle={`${teachers.length} teachers · ${filtered.length} shown`}
        icon={UserRound}
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
            label="Specialization"
            allLabel="All specializations"
            options={specializations}
            value={f.specialization}
            onChange={(v) => filters.set("specialization", v)}
          />
          <SearchBox
            label="Search teachers"
            placeholder="Name, username, email"
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
          caption="Teachers"
          columns={columns}
          rows={filtered}
          rowKey={(t) => t.id}
          loading={list.isLoading && !list.data}
          resetPageKey={filters.key}
          empty={
            <ListEmptyState
              icon={UserRound}
              noun="teachers"
              hasRecords={teachers.length > 0}
              onClearFilters={filters.clear}
              createAction={newLink}
            />
          }
        />
      </Panel>

      {remove.dialog}
    </div>
  );
}
