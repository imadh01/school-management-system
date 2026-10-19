import { useNavigate, useParams } from "react-router-dom";
import { BookOpen, GraduationCap, Power, School } from "lucide-react";
import { classSectionService } from "../services/classSectionService";
import type { ClassSectionResponse } from "../types/classSection.types";
import { studentService } from "@/features/students/services/studentService";
import type { StudentSummaryResponse } from "@/features/students/types/student.types";
import { subjectService } from "@/features/subjects/services/subjectService";
import type { SubjectResponse } from "@/features/subjects/types/subject.types";
import { DetailHeader } from "@/components/detail/DetailHeader";
import { DetailCard } from "@/components/detail/DetailCard";
import { DeleteButton, EditButton } from "@/components/detail/HeaderButtons";
import { Tabs, TabPanel, type TabItem } from "@/components/detail/Tabs";
import { KeyValueGrid } from "@/components/detail/KeyValueGrid";
import { DataTable, type Column } from "@/components/DataTable";
import { Badge, StatusBadge } from "@/components/Badge";
import { EmptyState } from "@/components/EmptyState";
import { Notice } from "@/components/Notice";
import { PersonCell } from "@/components/PersonCell";
import { PageLoadError, PageLoading } from "@/components/PageState";
import { useAsyncData } from "@/hooks/useAsyncData";
import { useCan } from "@/hooks/useCan";
import { useConfirmAction } from "@/hooks/useConfirmAction";
import { useTabParam } from "@/hooks/useTabParam";
import { PAGE_PERMISSIONS, Permission } from "@/routes/permissions";

const STUDENT_COLUMNS: Column<StudentSummaryResponse>[] = [
  {
    key: "roll",
    header: "Roll",
    sortValue: (s) => s.rollNumber,
    render: (s) => s.rollNumber,
  },
  {
    key: "name",
    header: "Student",
    sortValue: (s) => `${s.firstName} ${s.lastName}`,
    render: (s) => (
      <PersonCell
        name={`${s.firstName} ${s.lastName}`}
        to={`/students/${s.id}`}
        sub={`Adm No ${s.admNo}`}
      />
    ),
  },
  {
    key: "gender",
    header: "Gender",
    hideBelow: "md",
    sortValue: (s) => s.gender,
    render: (s) => s.gender,
  },
  {
    key: "status",
    header: "Status",
    sortValue: (s) => s.status,
    render: (s) => <StatusBadge status={s.status} />,
  },
];

const SUBJECT_COLUMNS: Column<SubjectResponse>[] = [
  {
    key: "code",
    header: "Code",
    sortValue: (s) => s.code,
    render: (s) => <span className="cell-strong">{s.code}</span>,
  },
  {
    key: "name",
    header: "Subject",
    sortValue: (s) => s.name,
    render: (s) => s.name,
  },
  {
    key: "type",
    header: "Type",
    hideBelow: "md",
    sortValue: (s) => s.type,
    render: (s) => s.type,
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
];

/**
 * U2: one class on a page of its own (/classes-sections/:id). Tabs: Overview, Students and
 * Subjects (each shown only to staff who can read students / subjects).
 */
export function ClassSectionDetailPage() {
  const classId = Number(useParams().id);
  const navigate = useNavigate();
  const can = useCan();
  const canEdit = can(Permission.ClassSectionsEdit);
  const canDelete = can(Permission.ClassSectionsDelete);
  const canSeeStudents = can(...PAGE_PERMISSIONS["/students"]);
  const canSeeSubjects = can(Permission.SubjectsView);

  const {
    data: cls,
    error,
    reload,
  } = useAsyncData(
    () => classSectionService.getById(classId),
    [classId],
    "Could not load this class.",
  );
  // The API has no per-class filter yet, so the lists are filtered here (see the handoff notes).
  const students = useAsyncData(
    async () =>
      canSeeStudents
        ? (await studentService.getAll()).filter(
            (s) => s.classSectionId === classId,
          )
        : [],
    [classId, canSeeStudents],
    "Could not load the students.",
  );
  const subjects = useAsyncData(
    async () =>
      canSeeSubjects
        ? (await subjectService.getAll()).filter(
            (s) => s.classSectionId === classId,
          )
        : [],
    [classId, canSeeSubjects],
    "Could not load the subjects.",
  );
  const [activeTab, selectTab] = useTabParam([
    "overview",
    ...(canSeeStudents ? ["students"] : []),
    ...(canSeeSubjects ? ["subjects"] : []),
  ]);

  const remove = useConfirmAction<ClassSectionResponse>({
    title: "Delete class",
    message: (c) => `Delete ${c.displayName}? This cannot be undone.`,
    confirmLabel: "Delete",
    danger: true,
    run: async (c) => {
      await classSectionService.delete(c.id);
      navigate("/classes-sections");
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
      await reload();
    },
    success: (c) =>
      `${c.displayName} was ${c.status === "Active" ? "deactivated" : "activated"}.`,
    failure: "Could not change the status of this class.",
  });

  if (error) {
    return (
      <PageLoadError
        title="This class could not be opened"
        message={error}
        backTo="/classes-sections"
        backLabel="Back to classes"
      />
    );
  }
  if (!cls) return <PageLoading />;

  const seatsLeft =
    cls.capacity !== null ? Math.max(0, cls.capacity - cls.enrolled) : null;
  const tabs: TabItem[] = [
    { id: "overview", label: "Overview", icon: School },
    ...(canSeeStudents
      ? [
          {
            id: "students",
            label: "Students",
            icon: GraduationCap,
            count: students.data?.length,
          },
        ]
      : []),
    ...(canSeeSubjects
      ? [
          {
            id: "subjects",
            label: "Subjects",
            icon: BookOpen,
            count: subjects.data?.length,
          },
        ]
      : []),
  ];

  return (
    <div>
      <DetailHeader
        back={{ to: "/classes-sections", label: "All classes" }}
        title={cls.displayName}
        meta={[
          `Code ${cls.code}`,
          cls.academicYearName,
          `${cls.enrolled}${cls.capacity ? ` / ${cls.capacity}` : ""} students`,
        ]}
        badges={
          <>
            <StatusBadge status={cls.status} />
            <Badge tone="neutral">{cls.stage}</Badge>
            {cls.classTeacherName && (
              <Badge tone="warn">Class teacher · {cls.classTeacherName}</Badge>
            )}
          </>
        }
        actions={
          canEdit || canDelete ? (
            <>
              {canEdit && (
                <EditButton primary to={`/classes-sections/${cls.id}/edit`} />
              )}
              {canEdit && (
                <button
                  type="button"
                  className="btn btn--secondary"
                  onClick={() => toggleStatus.ask(cls)}
                >
                  <Power size={16} aria-hidden="true" />
                  {cls.status === "Active" ? "Deactivate" : "Activate"}
                </button>
              )}
              {canDelete && <DeleteButton onClick={() => remove.ask(cls)} />}
            </>
          ) : undefined
        }
      />

      <Tabs
        idBase="class"
        label="Class details"
        tabs={tabs}
        active={activeTab}
        onChange={selectTab}
      />

      {activeTab === "overview" && (
        <TabPanel idBase="class" id="overview">
          <div className="detail-cards">
            <DetailCard title="Class">
              <KeyValueGrid
                items={[
                  { label: "Class", value: cls.name },
                  { label: "Section", value: cls.section },
                  { label: "Grade", value: cls.grade },
                  { label: "Code", value: cls.code },
                  { label: "Academic year", value: cls.academicYearName },
                  { label: "Class teacher", value: cls.classTeacherName },
                ]}
              />
            </DetailCard>
            <DetailCard title="Academic">
              <KeyValueGrid
                items={[
                  { label: "Stage", value: cls.stage },
                  { label: "Medium", value: cls.medium },
                  { label: "Stream", value: cls.stream },
                  { label: "Capacity", value: cls.capacity },
                  { label: "Enrolled", value: String(cls.enrolled) },
                  {
                    label: "Seats left",
                    value: seatsLeft === null ? null : String(seatsLeft),
                  },
                ]}
              />
            </DetailCard>
            <DetailCard title="Location">
              <KeyValueGrid
                items={[
                  { label: "Building", value: cls.building },
                  { label: "Floor", value: cls.floor },
                  { label: "Room", value: cls.room },
                ]}
              />
            </DetailCard>
          </div>
        </TabPanel>
      )}

      {activeTab === "students" && (
        <TabPanel idBase="class" id="students">
          <div className="card">
            {students.error && (
              <Notice tone="warn" style={{ margin: 16 }}>
                {students.error}
              </Notice>
            )}
            <DataTable
              caption={`Students of ${cls.displayName}`}
              columns={STUDENT_COLUMNS}
              rows={students.data ?? []}
              rowKey={(s) => s.id}
              loading={students.isLoading && !students.data}
              empty={
                <EmptyState
                  icon={GraduationCap}
                  title="No students in this class yet"
                />
              }
            />
          </div>
        </TabPanel>
      )}

      {activeTab === "subjects" && (
        <TabPanel idBase="class" id="subjects">
          <div className="card">
            {subjects.error && (
              <Notice tone="warn" style={{ margin: 16 }}>
                {subjects.error}
              </Notice>
            )}
            <DataTable
              caption={`Subjects of ${cls.displayName}`}
              columns={SUBJECT_COLUMNS}
              rows={subjects.data ?? []}
              rowKey={(s) => s.id}
              loading={subjects.isLoading && !subjects.data}
              empty={
                <EmptyState
                  icon={BookOpen}
                  title="No subjects in this class yet"
                />
              }
            />
          </div>
        </TabPanel>
      )}

      {remove.dialog}
      {toggleStatus.dialog}
    </div>
  );
}
