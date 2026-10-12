import { useEffect, useState } from "react";
import {
  Link,
  useNavigate,
  useParams,
  useSearchParams,
} from "react-router-dom";
import {
  BookOpen,
  HeartPulse,
  History,
  Info,
  Lock,
  Mail,
  Pencil,
  Phone,
  ShieldCheck,
  Trash,
  UserRound,
  Users,
  UserX,
} from "lucide-react";
import { studentService } from "../services/studentService";
import {
  isFullStudent,
  type StudentDetail,
  type StudentEnrollmentResponse,
  type StudentResponse,
} from "../types/student.types";
import { DetailHeader } from "@/components/detail/DetailHeader";
import { Tabs, TabPanel, type TabItem } from "@/components/detail/Tabs";
import { KeyValueGrid } from "@/components/detail/KeyValueGrid";
import { Badge, StatusBadge, statusTone } from "@/components/Badge";
import { ConfirmDialog } from "@/components/ConfirmDialog";
import { EmptyState } from "@/components/EmptyState";
import { useToast } from "@/components/Toast";
import { getApiErrors, getReadErrors } from "@/utils/apiError";
import { useAuth } from "@/store/AuthContext";
import { Permission } from "@/routes/permissions";

/** "2015-03-09" → "9 Mar 2015". Dates from the API are calendar dates, not instants. */
function formatDate(iso: string | null | undefined): string | null {
  if (!iso) return null;
  const d = new Date(`${iso}T00:00:00`);
  if (Number.isNaN(d.getTime())) return iso;
  return d.toLocaleDateString("en-GB", {
    day: "numeric",
    month: "short",
    year: "numeric",
  });
}

function ageOn(iso: string, today = new Date()): number | null {
  const d = new Date(`${iso}T00:00:00`);
  if (Number.isNaN(d.getTime())) return null;
  let age = today.getFullYear() - d.getFullYear();
  const beforeBirthday =
    today.getMonth() < d.getMonth() ||
    (today.getMonth() === d.getMonth() && today.getDate() < d.getDate());
  if (beforeBirthday) age -= 1;
  return age;
}

function joinParts(parts: (string | null | undefined)[]): string | null {
  const text = parts.filter((p) => p && p.trim()).join(", ");
  return text || null;
}

const TAB_IDS = [
  "overview",
  "guardians",
  "history",
  "health",
  "safeguarding",
] as const;
type TabId = (typeof TAB_IDS)[number];

/**
 * U2: one student on a page of its own (/students/:id), replacing the old details dialog and
 * the "Parents" and "History" dialogs (now tabs). The tab is kept in the address (?tab=health),
 * so a link or a reload opens the same tab.
 *
 * A teacher (Students.ViewScoped) gets the reduced record from the API (F1): only the fields
 * that record has are shown, read-only, with a note that the rest is for office staff.
 */
export function StudentDetailPage() {
  const { id } = useParams();
  const studentId = Number(id);
  const navigate = useNavigate();
  const toast = useToast();
  const [searchParams, setSearchParams] = useSearchParams();
  const { hasAnyPermission } = useAuth();
  const canEdit = hasAnyPermission([Permission.StudentsEdit]);
  const canDelete = hasAnyPermission([Permission.StudentsDelete]);

  const [student, setStudent] = useState<StudentDetail | null>(null);
  const [loadError, setLoadError] = useState<string | null>(null);
  const [history, setHistory] = useState<StudentEnrollmentResponse[] | null>(
    null,
  );
  const [historyError, setHistoryError] = useState<string | null>(null);
  const [confirmDelete, setConfirmDelete] = useState(false);

  useEffect(() => {
    let cancelled = false;
    studentService
      .getById(studentId)
      .then((s) => {
        if (!cancelled) setStudent(s);
      })
      .catch((err) => {
        if (!cancelled)
          setLoadError(getReadErrors(err, "Could not load this student.")[0]);
      });
    studentService
      .getEnrollments(studentId)
      .then((rows) => {
        if (!cancelled) setHistory(rows);
      })
      .catch((err) => {
        if (!cancelled)
          setHistoryError(getReadErrors(err, "Could not load the history.")[0]);
      });
    return () => {
      cancelled = true;
    };
  }, [studentId]);

  if (loadError) {
    return (
      <div className="card">
        <EmptyState
          icon={UserX}
          title="This student could not be opened"
          message={loadError}
          action={
            <Link className="btn btn--secondary" to="/students">
              Back to students
            </Link>
          }
        />
      </div>
    );
  }
  if (!student) {
    return (
      <div className="card">
        <div className="empty-state" role="status">
          Loading…
        </div>
      </div>
    );
  }

  const full = isFullStudent(student) ? student : null;
  const name = `${student.firstName} ${student.lastName}`;
  const hasAllergies = isFullStudent(student)
    ? student.hasAllergies
    : student.health.hasAllergies;

  const tabs: TabItem[] = [
    { id: "overview", label: "Overview", icon: UserRound },
    {
      id: "guardians",
      label: "Parents / guardians",
      icon: Users,
      count: student.guardians.length,
    },
    { id: "history", label: "Academic history", icon: History },
    { id: "health", label: "Health", icon: HeartPulse },
    ...(full
      ? [{ id: "safeguarding", label: "Safeguarding", icon: ShieldCheck }]
      : []),
  ];
  const requested = searchParams.get("tab") as TabId | null;
  const activeTab =
    requested && tabs.some((t) => t.id === requested) ? requested : "overview";
  const selectTab = (tab: string) =>
    setSearchParams(tab === "overview" ? {} : { tab }, { replace: true });

  const handleDelete = async () => {
    setConfirmDelete(false);
    try {
      await studentService.delete(student.id);
      toast.success(`${name} was deleted.`);
      navigate("/students");
    } catch (err) {
      toast.error(getApiErrors(err, "Could not delete this student.")[0]);
    }
  };

  const age = ageOn(student.dateOfBirth);

  return (
    <div>
      <DetailHeader
        back={{ to: "/students", label: "All students" }}
        title={name}
        photoUrl={student.photoUrl}
        meta={[
          `Adm # ${student.admNo}`,
          student.classSectionName,
          `Roll ${student.rollNumber}`,
        ]}
        badges={
          <>
            <StatusBadge status={student.status} />
            {student.house && (
              <Badge tone="neutral">{student.house} house</Badge>
            )}
            {hasAllergies && <Badge tone="warn">Has allergies</Badge>}
          </>
        }
        actions={
          full && (canEdit || canDelete) ? (
            <>
              {canEdit && (
                <Link
                  className="btn btn--primary"
                  to={`/students/${student.id}/edit`}
                >
                  <Pencil size={16} aria-hidden="true" />
                  Edit
                </Link>
              )}
              {canDelete && (
                <button
                  type="button"
                  className="btn btn--secondary"
                  onClick={() => setConfirmDelete(true)}
                >
                  <Trash size={16} aria-hidden="true" />
                  Delete
                </button>
              )}
            </>
          ) : undefined
        }
      />

      <Tabs
        idBase="student"
        label="Student details"
        tabs={tabs}
        active={activeTab}
        onChange={selectTab}
      />

      {activeTab === "overview" && (
        <TabPanel idBase="student" id="overview">
          {!full && <ScopedNotice />}
          <div className="detail-cards">
            <section className="detail-card" aria-labelledby="ov-personal">
              <h2 id="ov-personal">Personal</h2>
              <KeyValueGrid
                items={[
                  { label: "First name", value: student.firstName },
                  { label: "Middle name", value: student.middleName },
                  { label: "Last name", value: student.lastName },
                  { label: "Preferred name", value: student.preferredName },
                  { label: "Gender", value: student.gender },
                  {
                    label: "Date of birth",
                    value: `${formatDate(student.dateOfBirth)}${age !== null ? ` (${age} yrs)` : ""}`,
                  },
                  ...(full
                    ? [
                        { label: "Blood group", value: full.health.bloodGroup },
                        { label: "Nationality", value: full.nationality },
                        {
                          label: "Second nationality",
                          value: full.secondNationality,
                        },
                        {
                          label: "Country of birth",
                          value: full.countryOfBirth,
                        },
                      ]
                    : []),
                ]}
              />
            </section>

            <section className="detail-card" aria-labelledby="ov-academic">
              <h2 id="ov-academic">Academic</h2>
              <KeyValueGrid
                items={[
                  { label: "Class", value: student.classSectionName },
                  { label: "Roll number", value: student.rollNumber },
                  {
                    label: "Status",
                    value: <StatusBadge status={student.status} />,
                  },
                  { label: "House", value: student.house },
                  ...(full
                    ? [
                        {
                          label: "Admission date",
                          value: formatDate(full.admissionDate),
                        },
                        { label: "Admission type", value: full.admissionType },
                        { label: "From admission", value: full.admissionRegNo },
                        { label: "Curriculum", value: full.curriculumTrack },
                        {
                          label: "English proficiency",
                          value: full.englishProficiency,
                        },
                        { label: "EAL code", value: full.ealCode },
                      ]
                    : []),
                ]}
              />
            </section>

            {full && (
              <section className="detail-card" aria-labelledby="ov-contact">
                <h2 id="ov-contact">Contact &amp; address</h2>
                <KeyValueGrid
                  items={[
                    {
                      label: "Mobile",
                      value: full.mobile && (
                        <a href={`tel:${full.mobile}`}>{full.mobile}</a>
                      ),
                    },
                    {
                      label: "Email",
                      value: full.email && (
                        <a href={`mailto:${full.email}`}>{full.email}</a>
                      ),
                    },
                    {
                      label: "Address",
                      value: joinParts([
                        full.addressLine,
                        full.city,
                        full.state,
                        full.pincode,
                      ]),
                      wide: true,
                    },
                  ]}
                />
              </section>
            )}

            {full && (
              <section className="detail-card" aria-labelledby="ov-background">
                <h2 id="ov-background">Background</h2>
                <KeyValueGrid
                  items={[
                    { label: "Category", value: full.category },
                    { label: "Religion", value: full.religion },
                    { label: "Mother tongue", value: full.motherTongue },
                    { label: "Home language", value: full.homeLanguage },
                    { label: "Previous school", value: full.previousSchool },
                    {
                      label: "Transport",
                      value: full.transportRequired
                        ? `Yes${full.transportRoute ? ` · ${full.transportRoute}` : ""}`
                        : "No",
                    },
                    {
                      label: "Fee concession",
                      value:
                        full.feeConcessionPercent != null
                          ? `${full.feeConcessionPercent}%`
                          : null,
                    },
                  ]}
                />
              </section>
            )}

            {full && <IdentityCard student={full} />}
          </div>
        </TabPanel>
      )}

      {activeTab === "guardians" && (
        <TabPanel idBase="student" id="guardians">
          <section className="detail-card" aria-labelledby="gd-title">
            <h2 id="gd-title">Parents / guardians</h2>
            {student.guardians.length === 0 ? (
              <p className="cell-muted" style={{ margin: 0 }}>
                No parents linked to this student yet.
              </p>
            ) : (
              <div className="contact-list">
                {student.guardians.map((g) => (
                  <div key={g.parentId} className="contact-card">
                    <div className="contact-card__name">{g.parentName}</div>
                    <div className="contact-card__badges">
                      <Badge tone="info">{g.relationType}</Badge>
                      {g.isPrimaryContact && (
                        <Badge tone="warn">Primary contact</Badge>
                      )}
                    </div>
                    <div className="contact-card__line">
                      <Phone size={14} aria-hidden="true" />
                      <a href={`tel:${g.parentMobile}`}>{g.parentMobile}</a>
                    </div>
                    {"parentEmail" in g && g.parentEmail && (
                      <div className="contact-card__line">
                        <Mail size={14} aria-hidden="true" />
                        <a href={`mailto:${g.parentEmail}`}>{g.parentEmail}</a>
                      </div>
                    )}
                  </div>
                ))}
              </div>
            )}
          </section>
          {full && (
            <div className="notice">
              <Info size={16} aria-hidden="true" />
              <span>
                Parent details and links are managed in the{" "}
                <Link to="/parents">Parents</Link> module.
              </span>
            </div>
          )}
        </TabPanel>
      )}

      {activeTab === "history" && (
        <TabPanel idBase="student" id="history">
          <section className="detail-card" aria-labelledby="hi-title">
            <h2 id="hi-title">Academic history</h2>
            <p className="field-note" style={{ margin: "-6px 0 14px" }}>
              A new row appears when the student changes class or is promoted.
            </p>
            {historyError ? (
              <p className="cell-muted">{historyError}</p>
            ) : history === null ? (
              <p role="status">Loading…</p>
            ) : history.length === 0 ? (
              <p className="cell-muted">No academic history yet.</p>
            ) : (
              <div className="table-box">
                <table>
                  <caption className="visually-hidden">
                    Academic history of {name}
                  </caption>
                  <thead>
                    <tr>
                      <th scope="col">Academic year</th>
                      <th scope="col">Class</th>
                      <th scope="col">Roll</th>
                      <th scope="col">From</th>
                      <th scope="col">To</th>
                      <th scope="col">Outcome</th>
                      <th scope="col" className="hide-below-md">
                        Remarks
                      </th>
                    </tr>
                  </thead>
                  <tbody>
                    {history.map((r) => (
                      <tr key={r.id}>
                        <td className="cell-strong">{r.academicYearName}</td>
                        <td>{r.classSectionName}</td>
                        <td>{r.rollNumber}</td>
                        <td>{formatDate(r.startDate)}</td>
                        <td>{formatDate(r.endDate) ?? "—"}</td>
                        <td>
                          <Badge
                            tone={
                              r.status === "Active"
                                ? "ok"
                                : statusTone(r.status)
                            }
                          >
                            {r.status}
                          </Badge>
                        </td>
                        <td className="hide-below-md">{r.remarks ?? ""}</td>
                      </tr>
                    ))}
                  </tbody>
                </table>
              </div>
            )}
          </section>
        </TabPanel>
      )}

      {activeTab === "health" && (
        <TabPanel idBase="student" id="health">
          <section className="detail-card" aria-labelledby="he-allergies">
            <h2 id="he-allergies">Allergies</h2>
            {!hasAllergies ? (
              <p style={{ margin: 0 }}>No allergies recorded.</p>
            ) : full?.health.allergies ? (
              <div className="notice notice--warn">
                <HeartPulse size={16} aria-hidden="true" />
                <span>{full.health.allergies}</span>
              </div>
            ) : (
              <div className="notice notice--warn">
                <HeartPulse size={16} aria-hidden="true" />
                <span>
                  This student has allergies. The details are visible to staff
                  with sensitive-data access; ask the office if you need them.
                </span>
              </div>
            )}
          </section>
          {full ? (
            <section className="detail-card" aria-labelledby="he-more">
              <h2 id="he-more">Health and support</h2>
              <KeyValueGrid
                items={[
                  { label: "Blood group", value: full.health.bloodGroup },
                  {
                    label: "Dietary requirements",
                    value: full.health.dietaryRequirements,
                  },
                  {
                    label: "Insurance provider",
                    value: full.health.insuranceProvider,
                  },
                  {
                    label: "Insurance expiry",
                    value: formatDate(full.health.insurancePolicyExpiry),
                  },
                  {
                    label: "Medical notes",
                    value: full.health.medicalNotes,
                    wide: true,
                  },
                  {
                    label: "Special educational needs (SEN / IEP)",
                    value: full.health.specialEducationalNeeds,
                    wide: true,
                  },
                ]}
              />
            </section>
          ) : (
            <ScopedNotice />
          )}
        </TabPanel>
      )}

      {activeTab === "safeguarding" && full && (
        <TabPanel idBase="student" id="safeguarding">
          <section className="detail-card" aria-labelledby="sg-title">
            <h2 id="sg-title">Safeguarding</h2>
            <KeyValueGrid
              items={[
                {
                  label: "Custody arrangement",
                  value: full.custodyArrangement,
                },
                {
                  label: "Media consent",
                  value: full.mediaConsent
                    ? "Yes: photos and videos allowed"
                    : "No",
                },
              ]}
            />
          </section>
          <section className="detail-card" aria-labelledby="sg-pickup">
            <h2 id="sg-pickup">Authorised pickup persons</h2>
            {full.pickupPersons.length === 0 ? (
              <p className="cell-muted" style={{ margin: 0 }}>
                No pickup persons added.
              </p>
            ) : (
              <div className="contact-list">
                {full.pickupPersons.map((p) => (
                  <div key={p.id} className="contact-card">
                    <div className="contact-card__name">{p.name}</div>
                    <div className="contact-card__badges">
                      <Badge tone="neutral">{p.relation}</Badge>
                    </div>
                    <div className="contact-card__line">
                      <Phone size={14} aria-hidden="true" />
                      <a href={`tel:${p.phone}`}>{p.phone}</a>
                    </div>
                    {p.idNote && (
                      <div className="contact-card__line">ID: {p.idNote}</div>
                    )}
                  </div>
                ))}
              </div>
            )}
          </section>
        </TabPanel>
      )}

      <ConfirmDialog
        isOpen={confirmDelete}
        title="Delete student"
        message={`Delete ${name} (${student.admNo})? This cannot be undone.`}
        confirmLabel="Delete"
        danger
        onConfirm={handleDelete}
        onCancel={() => setConfirmDelete(false)}
      />
    </div>
  );
}

/** Teachers see the reduced record (F1); say so, so missing fields are not mistaken for empty ones. */
function ScopedNotice() {
  return (
    <div className="notice">
      <BookOpen size={16} aria-hidden="true" />
      <span>
        You are seeing the class view of this student. Contact details, identity
        documents and health notes are visible to office staff only.
      </span>
    </div>
  );
}

function IdentityCard({ student }: { student: StudentResponse }) {
  const idn = student.identity;
  return (
    <section className="detail-card" aria-labelledby="ov-identity">
      <h2 id="ov-identity">Identity documents</h2>
      {idn.isMasked ? (
        <div className="notice notice--warn">
          <Lock size={16} aria-hidden="true" />
          <span>
            Identity numbers are hidden for your role. Only staff with
            sensitive-data access can see them.
          </span>
        </div>
      ) : (
        <KeyValueGrid
          items={[
            { label: "Aadhaar", value: idn.aadhaarNumber },
            { label: "Passport", value: idn.passportNumber },
            { label: "Passport expiry", value: formatDate(idn.passportExpiry) },
            { label: "Visa type", value: idn.visaType },
            { label: "Visa expiry", value: formatDate(idn.visaExpiry) },
          ]}
        />
      )}
    </section>
  );
}
