import { useState } from "react";
import { useNavigate, useParams } from "react-router-dom";
import { BookOpen, BookPlus, UserRound } from "lucide-react";
import { teacherService } from "../services/teacherService";
import type {
  TeacherAssignment,
  TeacherResponse,
  TeacherSubject,
} from "../types/teacher.types";
import { AssignSubjectsDialog } from "../components/AssignSubjectsDialog";
import { DetailHeader } from "@/components/detail/DetailHeader";
import { DetailCard } from "@/components/detail/DetailCard";
import { ContactCard, ContactList } from "@/components/detail/ContactCard";
import { DeleteButton, EditButton } from "@/components/detail/HeaderButtons";
import { Tabs, TabPanel, type TabItem } from "@/components/detail/Tabs";
import { KeyValueGrid } from "@/components/detail/KeyValueGrid";
import { Badge, StatusBadge } from "@/components/Badge";
import { Chip, ChipList } from "@/components/Chip";
import { Notice } from "@/components/Notice";
import { PageLoadError, PageLoading } from "@/components/PageState";
import { useToast } from "@/components/Toast";
import { useAsyncData } from "@/hooks/useAsyncData";
import { useCan } from "@/hooks/useCan";
import { useConfirmAction } from "@/hooks/useConfirmAction";
import { useTabParam } from "@/hooks/useTabParam";
import { Permission } from "@/routes/permissions";

const TAB_IDS = ["overview", "assignments"] as const;

interface SubjectTarget {
  subject: TeacherSubject;
  assignment: TeacherAssignment;
}

/**
 * U2: one teacher on a page of their own (/teachers/:id). Tabs: Overview (profile and login) and
 * Assignments (classes, subjects, class-teacher duty). Assignment changes need Teachers.Assign and
 * are small pop-ups; the API answers each with the teacher's refreshed assignments.
 */
export function TeacherDetailPage() {
  const teacherId = Number(useParams().id);
  const navigate = useNavigate();
  const toast = useToast();
  const can = useCan();
  const canEdit = can(Permission.TeachersEdit);
  const canDelete = can(Permission.TeachersDelete);
  const canAssign = can(Permission.TeachersAssign);

  const { data: teacher, error } = useAsyncData(
    () => teacherService.getById(teacherId),
    [teacherId],
    "Could not load this teacher.",
  );
  const assignments = useAsyncData(
    () => teacherService.getAssignments(teacherId),
    [teacherId],
    "Could not load the assignments.",
  );
  const [activeTab, selectTab] = useTabParam(TAB_IDS);
  const [assigning, setAssigning] = useState(false);

  const remove = useConfirmAction<TeacherResponse>({
    title: "Delete teacher",
    message: (t) =>
      `Delete ${t.name}? Their login is removed too. This cannot be undone.`,
    confirmLabel: "Delete",
    danger: true,
    run: async (t) => {
      await teacherService.delete(t.id);
      navigate("/teachers");
    },
    success: (t) => `${t.name} was deleted.`,
    failure: "Could not delete this teacher.",
  });
  const unassign = useConfirmAction<SubjectTarget>({
    title: "Remove subject",
    message: ({ subject, assignment }) =>
      `Remove ${subject.name} (${assignment.classSectionName}) from ${teacher?.name ?? "this teacher"}? The subject will have no teacher until someone else is assigned.`,
    confirmLabel: "Remove",
    danger: true,
    run: async ({ subject }) =>
      assignments.setData(
        await teacherService.unassignSubject(teacherId, subject.subjectId),
      ),
    success: ({ subject }) => `${subject.name} was removed.`,
    failure: "Could not remove this subject.",
  });
  const classTeacher = useConfirmAction<TeacherAssignment>({
    title: "Class teacher",
    message: (a) =>
      a.isClassTeacher
        ? `${teacher?.name ?? "This teacher"} will no longer be the class teacher of ${a.classSectionName}.`
        : `Make ${teacher?.name ?? "this teacher"} the class teacher of ${a.classSectionName}?`,
    confirmLabel: "Confirm",
    run: async (a) =>
      assignments.setData(
        a.isClassTeacher
          ? await teacherService.clearClassTeacher(teacherId, a.classSectionId)
          : await teacherService.setClassTeacher(teacherId, a.classSectionId),
      ),
    success: (a) =>
      a.isClassTeacher
        ? `Class teacher of ${a.classSectionName} was unset.`
        : `Class teacher of ${a.classSectionName} was set.`,
    failure: "Could not change the class teacher.",
  });

  if (error) {
    return (
      <PageLoadError
        title="This teacher could not be opened"
        message={error}
        backTo="/teachers"
        backLabel="Back to teachers"
      />
    );
  }
  if (!teacher) return <PageLoading />;

  const list = assignments.data ?? [];
  const subjectCount = list.reduce((n, a) => n + a.subjects.length, 0);
  const classTeacherOf = list.filter((a) => a.isClassTeacher);

  const tabs: TabItem[] = [
    { id: "overview", label: "Overview", icon: UserRound },
    {
      id: "assignments",
      label: "Classes & subjects",
      icon: BookOpen,
      count: assignments.data?.length,
    },
  ];

  return (
    <div>
      <DetailHeader
        back={{ to: "/teachers", label: "All teachers" }}
        title={teacher.name}
        meta={[
          teacher.username,
          teacher.email,
          ...(teacher.phone ? [teacher.phone] : []),
        ]}
        badges={
          <>
            <StatusBadge status={teacher.status} />
            {teacher.specialization && (
              <Badge tone="info">{teacher.specialization}</Badge>
            )}
            {classTeacherOf.map((a) => (
              <Badge key={a.classSectionId} tone="warn">
                Class teacher · {a.classSectionName}
              </Badge>
            ))}
          </>
        }
        actions={
          canEdit || canDelete ? (
            <>
              {canEdit && (
                <EditButton primary to={`/teachers/${teacher.id}/edit`} />
              )}
              {canDelete && (
                <DeleteButton onClick={() => remove.ask(teacher)} />
              )}
            </>
          ) : undefined
        }
      />

      <Tabs
        idBase="teacher"
        label="Teacher details"
        tabs={tabs}
        active={activeTab}
        onChange={selectTab}
      />

      {activeTab === "overview" && (
        <TabPanel idBase="teacher" id="overview">
          <div className="detail-cards">
            <DetailCard title="Profile">
              <KeyValueGrid
                items={[
                  { label: "Full name", value: teacher.name },
                  { label: "Specialization", value: teacher.specialization },
                  {
                    label: "Phone",
                    value: teacher.phone && (
                      <a href={`tel:${teacher.phone}`}>{teacher.phone}</a>
                    ),
                  },
                  { label: "Status", value: teacher.status },
                ]}
              />
            </DetailCard>
            <DetailCard title="Login account">
              <KeyValueGrid
                items={[
                  { label: "Username", value: teacher.username },
                  {
                    label: "Email",
                    value: (
                      <a href={`mailto:${teacher.email}`}>{teacher.email}</a>
                    ),
                  },
                ]}
              />
            </DetailCard>
          </div>
        </TabPanel>
      )}

      {activeTab === "assignments" && (
        <TabPanel idBase="teacher" id="assignments">
          <DetailCard title="Classes & subjects">
            <p className="field-note" style={{ marginTop: 0 }}>
              {list.length} {list.length === 1 ? "class" : "classes"} ·{" "}
              {subjectCount} {subjectCount === 1 ? "subject" : "subjects"} ·
              class teacher of {classTeacherOf.length}
            </p>
            {assignments.error && (
              <Notice tone="warn" style={{ marginBottom: 12 }}>
                {assignments.error}
              </Notice>
            )}
            {assignments.isLoading && !assignments.data && (
              <p role="status">Loading…</p>
            )}
            {assignments.data && list.length === 0 && (
              <p className="cell-muted" style={{ marginTop: 0 }}>
                No classes or subjects assigned yet.
              </p>
            )}
            <ContactList>
              {list.map((a) => (
                <ContactCard
                  key={a.classSectionId}
                  name={a.classSectionName}
                  badges={
                    <>
                      <Badge tone="neutral">{a.academicYearName}</Badge>
                      {a.isClassTeacher && (
                        <Badge tone="warn">Class teacher</Badge>
                      )}
                    </>
                  }
                  actions={
                    canAssign ? (
                      <button
                        type="button"
                        className="btn btn--secondary btn--sm"
                        onClick={() => classTeacher.ask(a)}
                      >
                        {a.isClassTeacher
                          ? "Unset class teacher"
                          : "Make class teacher"}
                      </button>
                    ) : undefined
                  }
                >
                  {a.subjects.length === 0 ? (
                    <div className="contact-card__line">
                      No subjects in this class.
                    </div>
                  ) : (
                    <ChipList label={`Subjects in ${a.classSectionName}`}>
                      {a.subjects.map((s) => (
                        <Chip
                          key={s.subjectId}
                          label={`${s.name} (${s.code})`}
                          removeLabel={`Remove ${s.name} from ${a.classSectionName}`}
                          onRemove={
                            canAssign
                              ? () =>
                                  unassign.ask({ subject: s, assignment: a })
                              : undefined
                          }
                        />
                      ))}
                    </ChipList>
                  )}
                </ContactCard>
              ))}
            </ContactList>
            {canAssign && (
              <button
                type="button"
                className="btn btn--secondary"
                style={{ marginTop: 14 }}
                onClick={() => setAssigning(true)}
              >
                <BookPlus size={16} aria-hidden="true" />
                Assign subjects
              </button>
            )}
          </DetailCard>
        </TabPanel>
      )}

      {assigning && (
        <AssignSubjectsDialog
          teacher={teacher}
          assignments={list}
          onClose={() => setAssigning(false)}
          onAssigned={(next, count) => {
            assignments.setData(next);
            toast.success(
              `${count} subject${count > 1 ? "s were" : " was"} assigned to ${teacher.name}.`,
            );
          }}
        />
      )}
      {remove.dialog}
      {unassign.dialog}
      {classTeacher.dialog}
    </div>
  );
}
