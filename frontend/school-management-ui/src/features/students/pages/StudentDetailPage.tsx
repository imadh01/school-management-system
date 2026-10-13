import { Link, useNavigate, useParams } from "react-router-dom";
import {
  BookOpen,
  HeartPulse,
  History,
  Lock,
  ShieldCheck,
  UserRound,
  Users,
} from "lucide-react";
import { studentService } from "../services/studentService";
import {
  isFullStudent,
  type StudentDetail,
  type StudentResponse,
} from "../types/student.types";
import { DetailHeader } from "@/components/detail/DetailHeader";
import { DetailCard } from "@/components/detail/DetailCard";
import { DeleteButton, EditButton } from "@/components/detail/HeaderButtons";
import { ContactCard, ContactList } from "@/components/detail/ContactCard";
import { Tabs, TabPanel, type TabItem } from "@/components/detail/Tabs";
import { KeyValueGrid } from "@/components/detail/KeyValueGrid";
import { Badge, StatusBadge, statusTone } from "@/components/Badge";
import { Notice } from "@/components/Notice";
import { PageLoadError, PageLoading } from "@/components/PageState";
import { useAsyncData } from "@/hooks/useAsyncData";
import { useCan } from "@/hooks/useCan";
import { useConfirmAction } from "@/hooks/useConfirmAction";
import { useTabParam } from "@/hooks/useTabParam";
import { Permission } from "@/routes/permissions";
import { ageOn, formatDate, joinParts } from "@/utils/format";

/**
 * U2: one student on a page of its own (/students/:id). Tabs: Overview, Parents / guardians,
 * Academic history, Health, and Safeguarding (office staff only).
 *
 * A teacher (Students.ViewScoped) gets the reduced record from the API (F1): only the fields that
 * record has are shown, read-only, with a note that the rest is for office staff.
 */
export function StudentDetailPage() {
  const studentId = Number(useParams().id);
  const navigate = useNavigate();
  const can = useCan();

  const { data: student, error } = useAsyncData(
    () => studentService.getById(studentId),
    [studentId],
    "Could not load this student.",
  );
  const history = useAsyncData(
    () => studentService.getEnrollments(studentId),
    [studentId],
    "Could not load the history.",
  );

  const remove = useConfirmAction<StudentDetail>({
    title: "Delete student",
    message: (s) =>
      `Delete ${s.firstName} ${s.lastName} (${s.admNo})? This cannot be undone.`,
    confirmLabel: "Delete",
    danger: true,
    run: async (s) => {
      await studentService.delete(s.id);
      navigate("/students");
    },
    success: (s) => `${s.firstName} ${s.lastName} was deleted.`,
    failure: "Could not delete this student.",
  });

  const full = student && isFullStudent(student) ? student : null;
  const [activeTab, selectTab] = useTabParam(
    full
      ? ["overview", "guardians", "history", "health", "safeguarding"]
      : ["overview", "guardians", "history", "health"],
  );

  if (error) {
    return (
      <PageLoadError
        title="This student could not be opened"
        message={error}
        backTo="/students"
        backLabel="Back to students"
      />
    );
  }
  if (!student) return <PageLoading />;

  const name = `${student.firstName} ${student.lastName}`;
  const hasAllergies = full
    ? full.hasAllergies
    : !isFullStudent(student) && student.health.hasAllergies;
  const age = ageOn(student.dateOfBirth);
  const canEdit = can(Permission.StudentsEdit);
  const canDelete = can(Permission.StudentsDelete);

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
                <EditButton primary to={`/students/${student.id}/edit`} />
              )}
              {canDelete && (
                <DeleteButton onClick={() => remove.ask(student)} />
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
            <DetailCard title="Personal">
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
            </DetailCard>

            <DetailCard title="Academic">
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
                        {
                          label: "From admission",
                          value: full.admissionId ? (
                            <Link to={`/admissions/${full.admissionId}`}>
                              {full.admissionRegNo}
                            </Link>
                          ) : (
                            full.admissionRegNo
                          ),
                        },
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
            </DetailCard>

            {full && (
              <DetailCard title="Contact & address">
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
              </DetailCard>
            )}

            {full && (
              <DetailCard title="Background">
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
              </DetailCard>
            )}

            {full && <IdentityCard student={full} />}
          </div>
        </TabPanel>
      )}

      {activeTab === "guardians" && (
        <TabPanel idBase="student" id="guardians">
          <DetailCard title="Parents / guardians">
            {student.guardians.length === 0 ? (
              <p className="cell-muted" style={{ margin: 0 }}>
                No parents linked to this student yet.
              </p>
            ) : (
              <ContactList>
                {student.guardians.map((g) => (
                  <ContactCard
                    key={g.parentId}
                    name={
                      can(
                        Permission.ParentsView,
                        Permission.ParentsViewScoped,
                      ) ? (
                        <Link to={`/parents/${g.parentId}`}>
                          {g.parentName}
                        </Link>
                      ) : (
                        g.parentName
                      )
                    }
                    phone={g.parentMobile}
                    email={"parentEmail" in g ? g.parentEmail : null}
                    badges={
                      <>
                        <Badge tone="info">{g.relationType}</Badge>
                        {g.isPrimaryContact && (
                          <Badge tone="warn">Primary contact</Badge>
                        )}
                      </>
                    }
                  />
                ))}
              </ContactList>
            )}
          </DetailCard>
          {full && (
            <Notice>
              Parent details and links are managed in the{" "}
              <Link to="/parents">Parents</Link> module.
            </Notice>
          )}
        </TabPanel>
      )}

      {activeTab === "history" && (
        <TabPanel idBase="student" id="history">
          <DetailCard title="Academic history">
            <p className="field-note" style={{ margin: "-6px 0 14px" }}>
              A new row appears when the student changes class or is promoted.
            </p>
            {history.error ? (
              <p className="cell-muted">{history.error}</p>
            ) : !history.data ? (
              <p role="status">Loading…</p>
            ) : history.data.length === 0 ? (
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
                    {history.data.map((r) => (
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
          </DetailCard>
        </TabPanel>
      )}

      {activeTab === "health" && (
        <TabPanel idBase="student" id="health">
          <DetailCard title="Allergies">
            {!hasAllergies ? (
              <p style={{ margin: 0 }}>No allergies recorded.</p>
            ) : (
              <Notice tone="warn" icon={HeartPulse}>
                {full?.health.allergies ??
                  "This student has allergies. The details are visible to staff with sensitive-data access; ask the office if you need them."}
              </Notice>
            )}
          </DetailCard>
          {full ? (
            <DetailCard title="Health and support">
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
            </DetailCard>
          ) : (
            <ScopedNotice />
          )}
        </TabPanel>
      )}

      {activeTab === "safeguarding" && full && (
        <TabPanel idBase="student" id="safeguarding">
          <DetailCard title="Safeguarding">
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
          </DetailCard>
          <DetailCard title="Authorised pickup persons">
            {full.pickupPersons.length === 0 ? (
              <p className="cell-muted" style={{ margin: 0 }}>
                No pickup persons added.
              </p>
            ) : (
              <ContactList>
                {full.pickupPersons.map((p) => (
                  <ContactCard
                    key={p.id}
                    name={p.name}
                    phone={p.phone}
                    badges={<Badge tone="neutral">{p.relation}</Badge>}
                  >
                    {p.idNote && (
                      <div className="contact-card__line">ID: {p.idNote}</div>
                    )}
                  </ContactCard>
                ))}
              </ContactList>
            )}
          </DetailCard>
        </TabPanel>
      )}

      {remove.dialog}
    </div>
  );
}

/** Teachers see the reduced record (F1); say so, so missing fields are not mistaken for empty ones. */
function ScopedNotice() {
  return (
    <Notice icon={BookOpen}>
      You are seeing the class view of this student. Contact details, identity
      documents and health notes are visible to office staff only.
    </Notice>
  );
}

function IdentityCard({ student }: { student: StudentResponse }) {
  const idn = student.identity;
  return (
    <DetailCard title="Identity documents">
      {idn.isMasked ? (
        <Notice tone="warn" icon={Lock}>
          Identity numbers are hidden for your role. Only staff with
          sensitive-data access can see them.
        </Notice>
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
    </DetailCard>
  );
}
