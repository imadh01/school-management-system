import { useEffect, useState } from "react";
import { Link, useNavigate, useParams } from "react-router-dom";
import {
  Ban,
  Check,
  CircleCheck,
  FileText,
  GraduationCap,
  UserRound,
  Users,
  X,
} from "lucide-react";
import { admissionService } from "../services/admissionService";
import { classSectionService } from "@/features/class-sections/services/classSectionService";
import {
  ADMISSION_STATUS_TONE,
  isAdmissionDeletable,
  isAdmissionEditable,
  type AdmissionResponse,
  type AdmissionStatus,
} from "../types/admission.types";
import type { ClassSectionResponse } from "@/features/class-sections/types/classSection.types";
import { ConfirmAdmissionModal } from "../components/ConfirmAdmissionModal";
import { RejectAdmissionModal } from "../components/RejectAdmissionModal";
import { EnrollModal } from "../components/EnrollModal";
import { DocumentsPanel } from "@/features/documents/components/DocumentsPanel";
import { DetailHeader } from "@/components/detail/DetailHeader";
import { AddressList } from "@/components/detail/AddressList";
import { DetailCard } from "@/components/detail/DetailCard";
import { ContactCard, ContactList } from "@/components/detail/ContactCard";
import { DeleteButton, EditButton } from "@/components/detail/HeaderButtons";
import { Tabs, TabPanel, type TabItem } from "@/components/detail/Tabs";
import { KeyValueGrid } from "@/components/detail/KeyValueGrid";
import { Badge } from "@/components/Badge";
import { Notice } from "@/components/Notice";
import { PageLoadError, PageLoading } from "@/components/PageState";
import { useToast } from "@/components/Toast";
import { useAsyncData } from "@/hooks/useAsyncData";
import { useCan } from "@/hooks/useCan";
import { useConfirmAction } from "@/hooks/useConfirmAction";
import { useTabParam } from "@/hooks/useTabParam";
import { PAGE_PERMISSIONS, Permission } from "@/routes/permissions";
import { formatDate } from "@/utils/format";

/** Registered → Admitted → Enrolled, or Rejected at any point before enrolment. */
function StatusSteps({ status }: { status: AdmissionStatus }) {
  const order: AdmissionStatus[] = ["Registered", "Admitted", "Enrolled"];
  const reached = order.indexOf(status);
  return (
    <ol className="steps" aria-label="Application progress">
      {order.map((step, i) => {
        const state =
          status === "Rejected"
            ? i === 0
              ? "done"
              : "todo"
            : i < reached || status === "Enrolled"
              ? "done"
              : i === reached
                ? "current"
                : "todo";
        return (
          <li
            key={step}
            data-state={state}
            aria-current={state === "current" ? "step" : undefined}
          >
            <span className="steps__dot" aria-hidden="true">
              {state === "done" ? <Check size={14} /> : i + 1}
            </span>
            {step}
            <span className="visually-hidden">
              {state === "done"
                ? " (done)"
                : state === "current"
                  ? " (current step)"
                  : ""}
            </span>
          </li>
        );
      })}
      {status === "Rejected" && (
        <li data-state="rejected">
          <span className="steps__dot" aria-hidden="true">
            <X size={14} />
          </span>
          Rejected
        </li>
      )}
    </ol>
  );
}

const TAB_IDS = ["overview", "guardians", "enrolment", "documents"] as const;

/**
 * U2: one application on a page of its own (/admissions/:id). The decisions (confirm, reject,
 * enrol) stay pop-ups opened from here, and only the ones that fit the current status and the
 * user's permissions are offered.
 */
export function AdmissionDetailPage() {
  const admissionId = Number(useParams().id);
  const navigate = useNavigate();
  const toast = useToast();
  const can = useCan();
  const canEdit = can(Permission.AdmissionsEdit);
  const canEnrol = can(Permission.AdmissionsEnroll);
  const canDelete = can(Permission.AdmissionsDelete);
  const canOpenStudents = can(...PAGE_PERMISSIONS["/students"]);
  const canSeeDocuments = can(Permission.DocumentsView);

  const {
    data: admission,
    error,
    reload,
  } = useAsyncData(
    () => admissionService.getById(admissionId),
    [admissionId],
    "Could not load this application.",
  );
  const [activeTab, selectTab] = useTabParam(TAB_IDS);
  const [dialog, setDialog] = useState<"confirm" | "reject" | "enrol" | null>(
    null,
  );
  const [classSections, setClassSections] = useState<ClassSectionResponse[]>(
    [],
  );

  // The class list is needed only by the enrolment pop-up.
  useEffect(() => {
    if (dialog === "enrol" && classSections.length === 0)
      classSectionService
        .getAll()
        .then(setClassSections)
        .catch(() => undefined);
  }, [dialog, classSections.length]);

  const remove = useConfirmAction<AdmissionResponse>({
    title: "Delete application",
    message: (a) =>
      `Delete the application for ${a.firstName} ${a.lastName} (${a.regNo})? This cannot be undone.`,
    confirmLabel: "Delete",
    danger: true,
    run: async (a) => {
      await admissionService.delete(a.id);
      navigate("/admissions");
    },
    success: (a) => `Application ${a.regNo} was deleted.`,
    failure: "Could not delete this application.",
  });

  if (error) {
    return (
      <PageLoadError
        title="This application could not be opened"
        message={error}
        backTo="/admissions"
        backLabel="Back to admissions"
      />
    );
  }
  if (!admission) return <PageLoading />;

  const a = admission;
  const name = `${a.firstName} ${a.lastName}`;
  const editable = isAdmissionEditable(a.status);
  const enrolled = a.status === "Enrolled";

  const tabs: TabItem[] = [
    { id: "overview", label: "Overview", icon: UserRound },
    {
      id: "guardians",
      label: "Parents / guardians",
      icon: Users,
      count: a.guardians.length,
    },
    ...(enrolled
      ? [{ id: "enrolment", label: "Enrolment", icon: GraduationCap }]
      : []),
    { id: "documents", label: "Documents", icon: FileText },
  ];
  // "?tab=enrolment" on an application that is not enrolled falls back to the overview.
  const shownTab =
    activeTab === "enrolment" && !enrolled ? "overview" : activeTab;

  const afterAction = async (message: string) => {
    toast.success(message);
    await reload();
  };

  const actions = (
    <>
      {editable && canEdit && <EditButton to={`/admissions/${a.id}/edit`} />}
      {a.status === "Registered" && canEdit && (
        <button
          type="button"
          className="btn btn--primary"
          onClick={() => setDialog("confirm")}
        >
          <CircleCheck size={16} aria-hidden="true" />
          Confirm admission
        </button>
      )}
      {a.status === "Admitted" && canEnrol && (
        <button
          type="button"
          className="btn btn--primary"
          onClick={() => setDialog("enrol")}
        >
          <GraduationCap size={16} aria-hidden="true" />
          Enrol to class
        </button>
      )}
      {enrolled && a.studentId && canOpenStudents && (
        <Link className="btn btn--primary" to={`/students/${a.studentId}`}>
          <GraduationCap size={16} aria-hidden="true" />
          Open student record
        </Link>
      )}
      {editable && canEdit && (
        <button
          type="button"
          className="btn btn--secondary"
          onClick={() => setDialog("reject")}
        >
          <Ban size={16} aria-hidden="true" />
          Reject
        </button>
      )}
      {canDelete && isAdmissionDeletable(a.status) && (
        <DeleteButton onClick={() => remove.ask(a)} />
      )}
    </>
  );

  return (
    <div>
      <DetailHeader
        back={{ to: "/admissions", label: "All applications" }}
        title={name}
        meta={[
          `Reg No ${a.regNo}`,
          `Applied for ${a.appliedForClassSectionName}`,
          `Registered ${formatDate(a.registrationDate)}`,
        ]}
        badges={
          <>
            <Badge tone={ADMISSION_STATUS_TONE[a.status]}>{a.status}</Badge>
            <Badge tone={a.admissionType === "Transfer" ? "warn" : "neutral"}>
              {a.admissionType}
            </Badge>
            <Badge tone="neutral">{a.academicYearName}</Badge>
          </>
        }
        actions={actions}
      />

      <section
        className="detail-card"
        aria-label="Application progress"
        style={{ marginBottom: 18 }}
      >
        <StatusSteps status={a.status} />
        {a.status === "Rejected" && (
          <Notice tone="warn" icon={Ban} style={{ marginTop: 14 }}>
            <strong>Rejected.</strong>{" "}
            {a.rejectionReason || "No reason recorded."} This application is
            closed and can no longer be edited.
          </Notice>
        )}
        {enrolled && (
          <Notice icon={GraduationCap} style={{ marginTop: 14 }}>
            Enrolled into {a.allottedClassSectionName ?? "a class"}. This
            application is closed; changes are made on the student record.
          </Notice>
        )}
      </section>

      <Tabs
        idBase="admission"
        label="Application details"
        tabs={tabs}
        active={shownTab}
        onChange={selectTab}
      />

      {shownTab === "overview" && (
        <TabPanel idBase="admission" id="overview">
          <div className="detail-cards">
            <DetailCard title="Applicant">
              <KeyValueGrid
                items={[
                  { label: "First name", value: a.firstName },
                  { label: "Middle name", value: a.middleName },
                  { label: "Last name", value: a.lastName },
                  { label: "Gender", value: a.gender },
                  { label: "Date of birth", value: formatDate(a.dateOfBirth) },
                ]}
              />
            </DetailCard>
            <DetailCard title="Applying for">
              <KeyValueGrid
                items={[
                  { label: "Class", value: a.appliedForClassSectionName },
                  { label: "Academic year", value: a.academicYearName },
                  { label: "Admission type", value: a.admissionType },
                  { label: "Previous school", value: a.previousSchool },
                ]}
              />
            </DetailCard>
            <DetailCard title="Contact">
              <KeyValueGrid
                items={[
                  {
                    label: "Phone",
                    value: <a href={`tel:${a.phone}`}>{a.phone}</a>,
                  },
                  {
                    label: "Email",
                    value: a.email && (
                      <a href={`mailto:${a.email}`}>{a.email}</a>
                    ),
                  },
                ]}
              />
            </DetailCard>
            <DetailCard title="Addresses">
              <AddressList addresses={a.addresses} />
            </DetailCard>
            <DetailCard title="Admission">
              <KeyValueGrid
                items={[
                  {
                    label: "Admission fee",
                    value:
                      a.admissionFee != null
                        ? a.admissionFee.toLocaleString()
                        : null,
                  },
                  {
                    label: "Payment reference",
                    value: a.admissionFeeReference,
                  },
                  { label: "Blood group", value: a.bloodGroup },
                  { label: "Religion", value: a.religion },
                  { label: "Category", value: a.category },
                  { label: "Medical notes", value: a.medicalNotes, wide: true },
                  { label: "Remarks", value: a.remarks, wide: true },
                ]}
              />
            </DetailCard>
          </div>
        </TabPanel>
      )}

      {shownTab === "guardians" && (
        <TabPanel idBase="admission" id="guardians">
          <DetailCard title="Parents / guardians">
            {a.guardians.length === 0 ? (
              <p className="cell-muted" style={{ margin: 0 }}>
                No guardians added yet. Enrolment needs at least one, with a
                mobile number.
              </p>
            ) : (
              <ContactList>
                {a.guardians.map((g) => (
                  <ContactCard
                    key={g.id}
                    name={g.name}
                    badges={
                      <>
                        <Badge tone="info">{g.relationType}</Badge>
                        {g.isPrimaryContact && (
                          <Badge tone="warn">Primary contact</Badge>
                        )}
                      </>
                    }
                    phone={g.mobile}
                    email={g.email}
                  >
                    {!g.mobile && (
                      <p className="field__error" style={{ marginTop: 6 }}>
                        No mobile number: needed before enrolment.
                      </p>
                    )}
                  </ContactCard>
                ))}
              </ContactList>
            )}
          </DetailCard>
        </TabPanel>
      )}

      {shownTab === "enrolment" && (
        <TabPanel idBase="admission" id="enrolment">
          <DetailCard title="Enrolment">
            <KeyValueGrid
              items={[
                { label: "Class", value: a.allottedClassSectionName },
                { label: "Roll number", value: a.rollNumber },
                { label: "Admission number", value: a.admissionNumber },
                { label: "Admission date", value: formatDate(a.admissionDate) },
                { label: "Entry point", value: a.entryPoint },
                {
                  label: "Transport",
                  value: a.transportRequired ? "Required" : "Not required",
                },
                {
                  label: "Student record",
                  value:
                    a.studentId && canOpenStudents ? (
                      <Link to={`/students/${a.studentId}`}>
                        Open student record
                      </Link>
                    ) : a.studentId ? (
                      "Created"
                    ) : null,
                },
              ]}
            />
          </DetailCard>
        </TabPanel>
      )}

      {shownTab === "documents" && (
        <TabPanel idBase="admission" id="documents">
          {canSeeDocuments ? (
            <DocumentsPanel
              owner={{ type: "admission", id: a.id }}
              ownerLabel={`${name} · ${a.regNo}`}
              uploadBlockedNote={
                enrolled ? (
                  <>
                    Enrolment moved this application's documents to the student
                    record
                    {a.studentId && canOpenStudents ? (
                      <>
                        {": "}
                        <Link to={`/students/${a.studentId}?tab=documents`}>
                          open the student's documents
                        </Link>
                      </>
                    ) : null}
                    . New documents are uploaded there.
                  </>
                ) : undefined
              }
            />
          ) : (
            <Notice>
              Documents are visible to staff with the Documents permission.
            </Notice>
          )}
        </TabPanel>
      )}

      {dialog === "confirm" && (
        <ConfirmAdmissionModal
          admission={a}
          onClose={() => setDialog(null)}
          onSubmit={async (id, data) => {
            await admissionService.confirmAdmission(id, data);
            await afterAction(`${name} was admitted.`);
          }}
        />
      )}
      {dialog === "reject" && (
        <RejectAdmissionModal
          admission={a}
          onClose={() => setDialog(null)}
          onSubmit={async (id, reason) => {
            await admissionService.reject(id, { rejectionReason: reason });
            await afterAction(`Application ${a.regNo} was rejected.`);
          }}
        />
      )}
      {dialog === "enrol" && (
        <EnrollModal
          admission={a}
          classSections={classSections}
          onClose={() => setDialog(null)}
          onSubmit={async (id, data) => {
            await admissionService.enroll(id, data);
            await afterAction(`${name} was enrolled.`);
          }}
        />
      )}
      {remove.dialog}
    </div>
  );
}
