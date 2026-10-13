import { useMemo } from "react";
import { BookOpen, CircleCheck, UserX } from "lucide-react";
import { subjectService } from "../services/subjectService";
import {
  SUBJECT_TYPES,
  subjectMarks,
  type SubjectResponse,
} from "../types/subject.types";
import { classSectionService } from "@/features/class-sections/services/classSectionService";
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

/** Max or pass marks; for theory + practical subjects the total, with the split underneath. */
function MarksCell({ s, kind }: { s: SubjectResponse; kind: "max" | "pass" }) {
  const total = subjectMarks(s, kind);
  if (s.type !== "Both") return <>{total ?? "—"}</>;
  const split =
    kind === "max"
      ? `T ${s.theoryMax} · P ${s.practicalMax}`
      : `T ${s.theoryPass} · P ${s.practicalPass}`;
  return (
    <div>
      <div>{total}</div>
      <div className="person__sub">{split}</div>
    </div>
  );
}

/**
 * U2: the subjects list. Adding and editing are full pages. Subjects have no page of their own:
 * everything about a subject fits in its row, and teachers are assigned from the teacher's page.
 */
export function SubjectsPage() {
  const can = useCan();
  const canCreate = can(Permission.SubjectsCreate);
  const canEdit = can(Permission.SubjectsEdit);
  const canDelete = can(Permission.SubjectsDelete);

  const list = useAsyncData(
    async () => {
      const [subjects, classSections] = await Promise.all([
        subjectService.getAll(),
        classSectionService.getAll(),
      ]);
      return { subjects, classSections };
    },
    [],
    "Could not load subjects.",
  );
  const subjects = useMemo(() => list.data?.subjects ?? [], [list.data]);
  const yearOfClass = useMemo(
    () =>
      new Map(
        (list.data?.classSections ?? []).map((c) => [c.id, c.academicYearName]),
      ),
    [list.data],
  );

  const filters = useFilters({
    status: "All",
    year: "",
    classSection: "",
    type: "",
    search: "",
  });
  const f = filters.values;

  const years = useMemo(
    () => uniqueSorted([...yearOfClass.values()]),
    [yearOfClass],
  );
  const classNames = useMemo(
    () => uniqueSorted(subjects.map((s) => s.classSectionName)),
    [subjects],
  );
  const counts: Record<string, number> = useMemo(
    () => ({ All: subjects.length, ...countBy(subjects, (s) => s.status) }),
    [subjects],
  );
  const withoutTeacher = subjects.filter((s) => s.teacherId === null).length;

  const filtered = useMemo(() => {
    const q = f.search.trim().toLowerCase();
    return subjects.filter((s) => {
      if (f.status !== "All" && s.status !== f.status) return false;
      if (f.year && yearOfClass.get(s.classSectionId) !== f.year) return false;
      if (f.classSection && s.classSectionName !== f.classSection) return false;
      if (f.type && s.type !== f.type) return false;
      if (
        q &&
        !`${s.name} ${s.code} ${s.teacherName ?? ""}`.toLowerCase().includes(q)
      )
        return false;
      return true;
    });
  }, [subjects, yearOfClass, f]);

  const remove = useConfirmAction<SubjectResponse>({
    title: "Delete subject",
    message: (s) =>
      `Delete ${s.name} (${s.code}) of ${s.classSectionName}? This cannot be undone.`,
    confirmLabel: "Delete",
    danger: true,
    run: async (s) => {
      await subjectService.delete(s.id);
      await list.reload();
    },
    success: (s) => `${s.name} was deleted.`,
    failure: "Could not delete this subject.",
  });

  const exportCsv = () =>
    downloadCsv(
      "subjects.csv",
      ["Subject", "Code", "Class", "Type", "Max", "Pass", "Teacher", "Status"],
      filtered.map((s) => [
        s.name,
        s.code,
        s.classSectionName,
        s.type,
        subjectMarks(s, "max"),
        subjectMarks(s, "pass"),
        s.teacherName,
        s.status,
      ]),
    );

  const newLink = canCreate ? (
    <NewRecordLink to="/subjects/new" label="Add subject" />
  ) : undefined;

  const columns: Column<SubjectResponse>[] = [
    {
      key: "name",
      header: "Subject",
      sortValue: (s) => s.name,
      render: (s) => (
        <div>
          <div className="person__name">{s.name}</div>
          <div className="person__sub">{s.code}</div>
        </div>
      ),
    },
    {
      key: "class",
      header: "Class",
      sortValue: (s) => s.classSectionName,
      render: (s) => s.classSectionName,
    },
    {
      key: "type",
      header: "Type",
      hideBelow: "md",
      sortValue: (s) => s.type,
      render: (s) => (
        <Badge tone={s.type === "Theory" ? "neutral" : "info"}>{s.type}</Badge>
      ),
    },
    {
      key: "max",
      header: "Max",
      hideBelow: "md",
      sortValue: (s) => subjectMarks(s, "max"),
      render: (s) => <MarksCell s={s} kind="max" />,
    },
    {
      key: "pass",
      header: "Pass",
      hideBelow: "md",
      sortValue: (s) => subjectMarks(s, "pass"),
      render: (s) => <MarksCell s={s} kind="pass" />,
    },
    {
      key: "teacher",
      header: "Teacher",
      sortValue: (s) => s.teacherName,
      render: (s) =>
        s.teacherName ?? <span className="cell-muted">Not assigned</span>,
    },
    {
      key: "status",
      header: "Status",
      sortValue: (s) => s.status,
      render: (s) => <StatusBadge status={s.status} />,
    },
    {
      key: "actions",
      header: "Actions",
      render: (s) => (
        <RowActions
          name={`${s.name} (${s.classSectionName})`}
          editTo={canEdit ? `/subjects/${s.id}/edit` : undefined}
          onDelete={canDelete ? () => remove.ask(s) : undefined}
        />
      ),
    },
  ];

  return (
    <div>
      <PageHero
        title="Subjects"
        subtitle="Subjects taught in each class, their teachers and marks rules."
        actions={
          <>
            <PrintButton />
            {newLink}
          </>
        }
        stats={
          <StatGrid>
            <StatCard
              label="Subjects"
              value={subjects.length}
              icon={BookOpen}
              tone="info"
            />
            <StatCard
              label="Active"
              value={counts.Active ?? 0}
              icon={CircleCheck}
              tone="ok"
            />
            <StatCard
              label="Without a teacher"
              value={withoutTeacher}
              icon={UserX}
              tone="warn"
            />
          </StatGrid>
        }
      />

      <Panel
        title="All subjects"
        subtitle={`${subjects.length} subjects · ${filtered.length} shown`}
        icon={BookOpen}
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
            label="Class"
            allLabel="All classes"
            options={classNames}
            value={f.classSection}
            onChange={(v) => filters.set("classSection", v)}
          />
          <FilterSelect
            label="Type"
            allLabel="All types"
            options={SUBJECT_TYPES}
            value={f.type}
            onChange={(v) => filters.set("type", v)}
          />
          <SearchBox
            label="Search subjects"
            placeholder="Name, code, teacher"
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
          caption="Subjects"
          columns={columns}
          rows={filtered}
          rowKey={(s) => s.id}
          loading={list.isLoading && !list.data}
          resetPageKey={filters.key}
          empty={
            <ListEmptyState
              icon={BookOpen}
              noun="subjects"
              hasRecords={subjects.length > 0}
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
