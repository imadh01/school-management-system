import { useMemo } from "react";
import { IconButton } from "@/components/IconButton";
import {
  CircleCheck,
  CircleMinus,
  GraduationCap,
  HeartPulse,
  LogOut,
} from "lucide-react";
import { studentService } from "../services/studentService";
import type { StudentSummaryResponse } from "../types/student.types";
import { PageHero } from "@/components/PageHero";
import { StatCard, StatGrid } from "@/components/StatCard";
import { Panel } from "@/components/Panel";
import {
  ClearFiltersButton,
  FilterBar,
  FilterCheckbox,
  FilterSelect,
} from "@/components/FilterBar";
import { SearchBox } from "@/components/SearchBox";
import { DataTable, type Column } from "@/components/DataTable";
import { StatusBadge } from "@/components/Badge";
import { ListEmptyState } from "@/components/ListEmptyState";
import { Notice } from "@/components/Notice";
import { StudentPersonCell } from "../components/StudentPersonCell";
import {
  ExportCsvButton,
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

const fullName = (s: { firstName: string; lastName: string }) =>
  `${s.firstName} ${s.lastName}`;

/** The allergy flag (U1): an icon for everyone; the text, as a tooltip, only for ViewSensitive holders. */
function AllergyFlag({ student }: { student: StudentSummaryResponse }) {
  if (!student.hasAllergies) {
    return (
      <span className="cell-muted">
        <span aria-hidden="true">—</span>
        <span className="visually-hidden">No allergies recorded</span>
      </span>
    );
  }
  const label = student.allergies
    ? `Allergies: ${student.allergies}`
    : "Has allergies";
  return (
    <span className="flag" title={label}>
      <HeartPulse size={16} aria-hidden="true" />
      <span className="visually-hidden">{label}</span>
    </span>
  );
}

/**
 * The students list. A teacher holding only Students.ViewScoped gets their own students from the
 * API (F1) and no edit or delete buttons.
 */
export function StudentsPage() {
  const can = useCan();
  const canEdit = can(Permission.StudentsEdit);

  const list = useAsyncData(
    () => studentService.getAll(),
    [],
    "Could not load students.",
  );
  const students = useMemo(() => list.data ?? [], [list.data]);

  const filters = useFilters({
    status: "",
    search: "",
    classSection: "",
    gender: "",
    curriculum: "",
    house: "",
    allergiesOnly: false,
  });
  const f = filters.values;

  const options = useMemo(
    () => ({
      classes: uniqueSorted(students.map((s) => s.classSectionName)),
      curricula: uniqueSorted(students.map((s) => s.curriculumTrack)),
      houses: uniqueSorted(students.map((s) => s.house)),
    }),
    [students],
  );
  const counts = useMemo(() => countBy(students, (s) => s.status), [students]);

  const filtered = useMemo(() => {
    const q = f.search.trim().toLowerCase();
    return students.filter((s) => {
      if (f.status && s.status !== f.status) return false;
      if (f.classSection && s.classSectionName !== f.classSection) return false;
      if (f.gender && s.gender !== f.gender) return false;
      if (f.curriculum && s.curriculumTrack !== f.curriculum) return false;
      if (f.house && s.house !== f.house) return false;
      if (f.allergiesOnly && !s.hasAllergies) return false;
      if (q) {
        const haystack =
          `${fullName(s)} ${s.admNo} ${s.rollNumber} ${s.classSectionName}`.toLowerCase();
        if (!haystack.includes(q)) return false;
      }
      return true;
    });
  }, [students, f]);

  // Students are never deleted from the list: one who has gone is marked as Left (the record and
  // the class history stay). The seat is freed and the student leaves the attendance roster.
  const leave = useConfirmAction<StudentSummaryResponse>({
    title: "Mark student as left",
    message: (s) =>
      `Mark ${fullName(s)} (${s.admNo}) as left? Their class seat is released and they will no longer appear in attendance. The record and history are kept.`,
    confirmLabel: "Mark as left",
    danger: true,
    run: async (s) => {
      await studentService.markLeft(s.id);
      await list.reload();
    },
    success: (s) => `${fullName(s)} was marked as left.`,
    failure: "Could not mark this student as left.",
  });

  const exportCsv = () =>
    downloadCsv(
      "students.csv",
      [
        "Adm No",
        "Student",
        "Class",
        "Gender",
        "House",
        "Nationality",
        "Curriculum",
        "Status",
        "Allergies",
      ],
      filtered.map((s) => [
        s.admNo,
        fullName(s),
        s.classSectionName,
        s.gender,
        s.house,
        s.nationality,
        s.curriculumTrack,
        s.status,
        // The text only for Students.ViewSensitive holders; everyone else exports the flag.
        s.allergies ?? (s.hasAllergies ? "Yes" : ""),
      ]),
    );

  const columns: Column<StudentSummaryResponse>[] = [
    {
      key: "admNo",
      header: "Adm No",
      sortValue: (s) => s.admNo,
      render: (s) => (
        <span
          className="cell-strong"
          title={
            s.admissionRegNo ? `From admission ${s.admissionRegNo}` : undefined
          }
        >
          {s.admNo}
        </span>
      ),
    },
    {
      key: "name",
      header: "Student",
      sortValue: (s) => fullName(s),
      render: (s) => (
        <StudentPersonCell
          studentId={s.id}
          hasPhoto={s.photoUrl != null}
          name={fullName(s)}
          roll={s.rollNumber}
        />
      ),
    },
    {
      key: "class",
      header: "Class",
      sortValue: (s) => s.classSectionName,
      render: (s) => s.classSectionName,
    },
    {
      key: "gender",
      header: "Gender",
      hideBelow: "md",
      sortValue: (s) => s.gender,
      render: (s) => s.gender,
    },
    {
      key: "house",
      header: "House",
      hideBelow: "md",
      sortValue: (s) => s.house,
      render: (s) => s.house ?? <span className="cell-muted">—</span>,
    },
    {
      key: "allergies",
      header: "Allergies",
      render: (s) => <AllergyFlag student={s} />,
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
          name={fullName(s)}
          viewTo={`/students/${s.id}`}
          editTo={canEdit ? `/students/${s.id}/edit` : undefined}
        >
          {canEdit && s.status !== "Left" && (
            <IconButton
              icon={LogOut}
              tone="bad"
              label={`Mark ${fullName(s)} as left`}
              onClick={() => leave.ask(s)}
            />
          )}
        </RowActions>
      ),
    },
  ];

  return (
    <div>
      <PageHero
        title="Students"
        subtitle="Enrolled students, their classes, guardians and academic history."
        actions={<PrintButton />}
        stats={
          <StatGrid>
            <StatCard
              label="Total"
              value={students.length}
              icon={GraduationCap}
              tone="info"
            />
            <StatCard
              label="Active"
              value={counts.Active ?? 0}
              icon={CircleCheck}
              tone="ok"
            />
            <StatCard
              label="Inactive"
              value={counts.Inactive ?? 0}
              icon={CircleMinus}
              tone="neutral"
            />
            <StatCard
              label="Left"
              value={counts.Left ?? 0}
              icon={LogOut}
              tone="purple"
            />
          </StatGrid>
        }
      />

      <Panel
        title="All students"
        subtitle={`${students.length} students · ${filtered.length} shown`}
        icon={GraduationCap}
        actions={
          <ExportCsvButton
            onClick={exportCsv}
            disabled={filtered.length === 0}
          />
        }
      >
        <FilterBar>
          <FilterSelect
            label="Class"
            allLabel="All classes"
            options={options.classes}
            value={f.classSection}
            onChange={(v) => filters.set("classSection", v)}
          />
          <FilterSelect
            label="Gender"
            allLabel="All genders"
            options={["Male", "Female"]}
            value={f.gender}
            onChange={(v) => filters.set("gender", v)}
          />
          <FilterSelect
            label="House"
            allLabel="All houses"
            options={options.houses}
            value={f.house}
            onChange={(v) => filters.set("house", v)}
          />
          <FilterSelect
            label="Status"
            allLabel="All statuses"
            options={["Active", "Inactive", "Left"]}
            value={f.status}
            onChange={(v) => filters.set("status", v)}
          />
          {options.curricula.length > 0 && (
            <FilterSelect
              label="Curriculum"
              allLabel="All curricula"
              options={options.curricula}
              value={f.curriculum}
              onChange={(v) => filters.set("curriculum", v)}
            />
          )}
          <FilterCheckbox
            label="Has allergies"
            checked={f.allergiesOnly}
            onChange={(v) => filters.set("allergiesOnly", v)}
          />
          <SearchBox
            label="Search students"
            placeholder="Name, admission no., roll"
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
          caption="Students"
          columns={columns}
          rows={filtered}
          rowKey={(s) => s.id}
          loading={list.isLoading && !list.data}
          resetPageKey={filters.key}
          empty={
            <ListEmptyState
              icon={GraduationCap}
              noun="students"
              hasRecords={students.length > 0}
              onClearFilters={filters.clear}
              emptyMessage="Students appear here once they are enrolled from Admissions."
            />
          }
        />
      </Panel>

      {leave.dialog}
    </div>
  );
}
