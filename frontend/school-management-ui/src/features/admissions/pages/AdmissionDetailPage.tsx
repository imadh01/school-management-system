import { useCallback, useEffect, useState } from "react";
import {
  Link,
  useNavigate,
  useParams,
  useSearchParams,
} from "react-router-dom";
import {
  Ban,
  Check,
  CircleCheck,
  FileText,
  FileUp,
  GraduationCap,
  Mail,
  Pencil,
  Phone,
  Trash,
  UserRound,
  Users,
  UserX,
  X,
} from "lucide-react";
import { admissionService } from "../services/admissionService";
import { classSectionService } from "@/features/class-sections/services/classSectionService";
import {
  ADMISSION_STATUS_TONE,
  isAdmissionEditable,
  type AdmissionResponse,
  type AdmissionStatus,
} from "../types/admission.types";
import type { ClassSectionResponse } from "@/features/class-sections/types/classSection.types";
import { ConfirmAdmissionModal } from "../components/ConfirmAdmissionModal";
import { RejectAdmissionModal } from "../components/RejectAdmissionModal";
import { EnrollModal } from "../components/EnrollModal";
import { DetailHeader } from "@/components/detail/DetailHeader";
import { Tabs, TabPanel, type TabItem } from "@/components/detail/Tabs";
import { KeyValueGrid } from "@/components/detail/KeyValueGrid";
import { Badge } from "@/components/Badge";
import { ConfirmDialog } from "@/components/ConfirmDialog";
import { EmptyState } from "@/components/EmptyState";
import { useToast } from "@/components/Toast";
import { getApiErrors, getReadErrors } from "@/utils/apiError";
import { useAuth } from "@/store/AuthContext";
import { PAGE_PERMISSIONS, Permission } from "@/routes/permissions";

function formatDate(iso: string | null | undefined): string | null {
  if (!iso) return null;
  const d = new Date(`${iso}T00:00:00`);
  return Number.isNaN(d.getTime())
    ? iso
    : d.toLocaleDateString("en-GB", {
        day: "numeric",
        month: "short",
        year: "numeric",
      });
}

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

/**
 * U2: one application on a page of its own (/admissions/:id), replacing the details dialog and the
 * "view reason" dialog. The decisions (confirm, reject, enrol) stay pop-ups opened from here, and
 * only the ones that fit the current status and the user's permissions are offered.
 */
export function AdmissionDetailPage() {
  const { id } = useParams();
  const admissionId = Number(id);
  const navigate = useNavigate();
  const toast = useToast();
  const [searchParams, setSearchParams] = useSearchParams();
  const { hasAnyPermission } = useAuth();
  const canEdit = hasAnyPermission([Permission.AdmissionsEdit]);
  const canEnrol = hasAnyPermission([Permission.AdmissionsEnroll]);
  const canDelete = hasAnyPermission([Permission.AdmissionsDelete]);
  const canOpenStudents = hasAnyPermission(PAGE_PERMISSIONS["/students"]);

  const [admission, setAdmission] = useState<AdmissionResponse | null>(null);
  const [classSections, setClassSections] = useState<ClassSectionResponse[]>(
    [],
  );
  const [loadError, setLoadError] = useState<string | null>(null);
  const [dialog, setDialog] = useState<
    "confirm" | "reject" | "enrol" | "delete" | null
  >(null);

  const load = useCallback(async () => {
    try {
      setAdmission(await admissionService.getById(admissionId));
    } catch (err) {
      setLoadError(getReadErrors(err, "Could not load this application.")[0]);
    }
  }, [admissionId]);

  useEffect(() => {
    void load();
  }, [load]);

  // The class list is needed only by the enrolment pop-up.
  useEffect(() => {
    if (dialog === "enrol" && classSections.length === 0)
      classSectionService
        .getAll()
        .then(setClassSections)
        .catch(() => undefined);
  }, [dialog, classSections.length]);

  if (loadError) {
    return (
      <div className="card">
        <EmptyState
          icon={UserX}
          title="This application could not be opened"
          message={loadError}
          action={
            <Link className="btn btn--secondary" to="/admissions">
              Back to admissions
            </Link>
          }
        />
      </div>
    );
  }
  if (!admission) {
    return (
      <div className="card">
        <div className="empty-state" role="status">
          Loading…
        </div>
      </div>
    );
  }

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
  const requested = searchParams.get("tab");
  const activeTab =
    requested && tabs.some((t) => t.id === requested) ? requested : "overview";
  const selectTab = (tab: string) =>
    setSearchParams(tab === "overview" ? {} : { tab }, { replace: true });

  const afterAction = async (message: string) => {
    toast.success(message);
    await load();
  };

  const handleDelete = async () => {
    setDialog(null);
    try {
      await admissionService.delete(a.id);
      toast.success(`Application ${a.regNo} was deleted.`);
      navigate("/admissions");
    } catch (err) {
      toast.error(getApiErrors(err, "Could not delete this application.")[0]);
    }
  };

  const actions = (
    <>
      {editable && canEdit && (
        <Link className="btn btn--secondary" to={`/admissions/${a.id}/edit`}>
          <Pencil size={16} aria-hidden="true" />
          Edit
        </Link>
      )}
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
      {canDelete && (
        <button
          type="button"
          className="btn btn--secondary"
          onClick={() => setDialog("delete")}
        >
          <Trash size={16} aria-hidden="true" />
          Delete
        </button>
      )}
    </>
  );

  return (
    <div>
      <DetailHeader
        back={{ to: "/admissions", label: "All applications" }}
        title={name}
        meta={[
          `Reg # ${a.regNo}`,
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
          <div className="notice notice--warn" style={{ marginTop: 14 }}>
            <Ban size={16} aria-hidden="true" />
            <span>
              <strong>Rejected.</strong>{" "}
              {a.rejectionReason || "No reason recorded."} This application is
              closed and can no longer be edited.
            </span>
          </div>
        )}
        {enrolled && (
          <div className="notice" style={{ marginTop: 14 }}>
            <GraduationCap size={16} aria-hidden="true" />
            <span>
              Enrolled into {a.allottedClassSectionName ?? "a class"}. This
              application is closed; changes are made on the student record.
            </span>
          </div>
        )}
      </section>

      <Tabs
        idBase="admission"
        label="Application details"
        tabs={tabs}
        active={activeTab}
        onChange={selectTab}
      />

      {activeTab === "overview" && (
        <TabPanel idBase="admission" id="overview">
          <div className="detail-cards">
            <section className="detail-card" aria-labelledby="ad-applicant">
              <h2 id="ad-applicant">Applicant</h2>
              <KeyValueGrid
                items={[
                  { label: "First name", value: a.firstName },
                  { label: "Middle name", value: a.middleName },
                  { label: "Last name", value: a.lastName },
                  { label: "Gender", value: a.gender },
                  { label: "Date of birth", value: formatDate(a.dateOfBirth) },
                ]}
              />
            </section>
            <section className="detail-card" aria-labelledby="ad-applying">
              <h2 id="ad-applying">Applying for</h2>
              <KeyValueGrid
                items={[
                  { label: "Class", value: a.appliedForClassSectionName },
                  { label: "Academic year", value: a.academicYearName },
                  { label: "Admission type", value: a.admissionType },
                  { label: "Previous school", value: a.previousSchool },
                ]}
              />
            </section>
            <section className="detail-card" aria-labelledby="ad-contact">
              <h2 id="ad-contact">Contact &amp; address</h2>
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
                  {
                    label: "Address",
                    value:
                      [a.addressLine, a.city, a.state, a.pincode]
                        .filter(Boolean)
                        .join(", ") || null,
                    wide: true,
                  },
                ]}
              />
            </section>
            <section className="detail-card" aria-labelledby="ad-admission">
              <h2 id="ad-admission">Admission</h2>
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
            </section>
          </div>
        </TabPanel>
      )}

      {activeTab === "guardians" && (
        <TabPanel idBase="admission" id="guardians">
          <section className="detail-card" aria-labelledby="ad-guardians">
            <h2 id="ad-guardians">Parents / guardians</h2>
            {a.guardians.length === 0 ? (
              <p className="cell-muted" style={{ margin: 0 }}>
                No guardians added yet. Enrolment needs at least one, with a
                mobile number.
              </p>
            ) : (
              <div className="contact-list">
                {a.guardians.map((g) => (
                  <div key={g.id} className="contact-card">
                    <div className="contact-card__name">{g.name}</div>
                    <div className="contact-card__badges">
                      <Badge tone="info">{g.relationType}</Badge>
                      {g.isPrimaryContact && (
                        <Badge tone="warn">Primary contact</Badge>
                      )}
                    </div>
                    {g.mobile ? (
                      <div className="contact-card__line">
                        <Phone size={14} aria-hidden="true" />
                        <a href={`tel:${g.mobile}`}>{g.mobile}</a>
                      </div>
                    ) : (
                      <p className="field__error" style={{ marginTop: 6 }}>
                        No mobile number: needed before enrolment.
                      </p>
                    )}
                    {g.email && (
                      <div className="contact-card__line">
                        <Mail size={14} aria-hidden="true" />
                        <a href={`mailto:${g.email}`}>{g.email}</a>
                      </div>
                    )}
                  </div>
                ))}
              </div>
            )}
          </section>
        </TabPanel>
      )}

      {activeTab === "enrolment" && enrolled && (
        <TabPanel idBase="admission" id="enrolment">
          <section className="detail-card" aria-labelledby="ad-enrolment">
            <h2 id="ad-enrolment">Enrolment</h2>
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
          </section>
        </TabPanel>
      )}

      {activeTab === "documents" && (
        <TabPanel idBase="admission" id="documents">
          <section className="detail-card" aria-labelledby="ad-documents">
            <h2 id="ad-documents">Documents</h2>
            <div className="dropzone dropzone--disabled" aria-disabled="true">
              <FileUp size={28} aria-hidden="true" />
              <strong>Documents are not switched on yet</strong>
              <span>
                Uploading and viewing documents arrives with the documents
                module (G1). They will move to the student record on enrolment.
              </span>
            </div>
          </section>
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
      <ConfirmDialog
        isOpen={dialog === "delete"}
        title="Delete application"
        message={`Delete the application for ${name} (${a.regNo})? This cannot be undone.`}
        confirmLabel="Delete"
        danger
        onConfirm={handleDelete}
        onCancel={() => setDialog(null)}
      />
    </div>
  );
}
