import { useState } from "react";
import { Link, useNavigate, useParams } from "react-router-dom";
import { Baby, Link2, Lock, UserRound } from "lucide-react";
import { parentService } from "../services/parentService";
import {
  isFullParent,
  type LinkedStudentResponse,
  type ParentDetail,
} from "../types/parent.types";
import { LinkChildDialog } from "../components/LinkChildDialog";
import { DetailHeader } from "@/components/detail/DetailHeader";
import { DetailCard } from "@/components/detail/DetailCard";
import { ContactCard, ContactList } from "@/components/detail/ContactCard";
import { DeleteButton, EditButton } from "@/components/detail/HeaderButtons";
import { Tabs, TabPanel, type TabItem } from "@/components/detail/Tabs";
import { KeyValueGrid } from "@/components/detail/KeyValueGrid";
import { Badge, StatusBadge } from "@/components/Badge";
import { Notice } from "@/components/Notice";
import { PageLoadError, PageLoading } from "@/components/PageState";
import { useToast } from "@/components/Toast";
import { useAsyncData } from "@/hooks/useAsyncData";
import { useCan } from "@/hooks/useCan";
import { useConfirmAction } from "@/hooks/useConfirmAction";
import { useTabParam } from "@/hooks/useTabParam";
import { PAGE_PERMISSIONS, Permission } from "@/routes/permissions";
import { joinParts } from "@/utils/format";

const TAB_IDS = ["overview", "children"] as const;
const yesNo = (value: boolean) => (value ? "Yes" : "No");

/**
 * U2: one parent on a page of their own (/parents/:id). Tabs: Overview and Children. Children are
 * linked, unlinked and made "primary contact" here (Parents.Edit), each a small pop-up.
 *
 * A teacher (Parents.ViewScoped) gets name and mobile only, and only their own students among the
 * children (F1); nothing on the page changes data for them.
 */
export function ParentDetailPage() {
  const parentId = Number(useParams().id);
  const navigate = useNavigate();
  const toast = useToast();
  const can = useCan();
  const canEdit = can(Permission.ParentsEdit);
  const canDelete = can(Permission.ParentsDelete);
  const canOpenStudents = can(...PAGE_PERMISSIONS["/students"]);

  const { data: parent, error } = useAsyncData(
    () => parentService.getById(parentId),
    [parentId],
    "Could not load this parent.",
  );
  const children = useAsyncData(
    () => parentService.getLinkedStudents(parentId),
    [parentId],
    "Could not load the children.",
  );
  const [activeTab, selectTab] = useTabParam(TAB_IDS);
  const [linking, setLinking] = useState(false);

  const remove = useConfirmAction<ParentDetail>({
    title: "Delete parent",
    message: (p) => `Delete ${p.name}? This cannot be undone.`,
    confirmLabel: "Delete",
    danger: true,
    run: async (p) => {
      await parentService.delete(p.id);
      navigate("/parents");
    },
    success: (p) => `${p.name} was deleted.`,
    failure:
      "Could not delete this parent. They may still be linked to students.",
  });
  const unlink = useConfirmAction<LinkedStudentResponse>({
    title: "Unlink child",
    message: (c) =>
      `Unlink ${c.studentName} from ${parent?.name ?? "this parent"}? The student record is kept.`,
    confirmLabel: "Unlink",
    danger: true,
    run: async (c) => {
      await parentService.unlinkStudent(parentId, c.studentId);
      await children.reload();
    },
    success: (c) => `${c.studentName} was unlinked.`,
    failure: "Could not unlink this student.",
  });
  const makePrimary = useConfirmAction<LinkedStudentResponse>({
    title: "Make primary contact",
    message: (c) =>
      `Make ${parent?.name ?? "this parent"} the primary contact for ${c.studentName}? The student's current primary contact stops being primary.`,
    confirmLabel: "Make primary",
    run: async (c) => {
      await parentService.setPrimaryStudent(parentId, c.studentId);
      await children.reload();
    },
    success: (c) =>
      `${parent?.name ?? "This parent"} is now the primary contact for ${c.studentName}.`,
    failure: "Could not change the primary contact.",
  });

  if (error) {
    return (
      <PageLoadError
        title="This parent could not be opened"
        message={error}
        backTo="/parents"
        backLabel="Back to parents"
      />
    );
  }
  if (!parent) return <PageLoading />;

  const full = isFullParent(parent) ? parent : null;
  const linked = children.data ?? [];
  const canChangeChildren = canEdit && full !== null;

  const tabs: TabItem[] = [
    { id: "overview", label: "Overview", icon: UserRound },
    {
      id: "children",
      label: "Children",
      icon: Baby,
      count: children.data?.length,
    },
  ];

  return (
    <div>
      <DetailHeader
        back={{ to: "/parents", label: "All parents" }}
        title={parent.name}
        meta={[parent.mobile, ...(full?.email ? [full.email] : [])]}
        badges={
          full ? (
            <>
              <StatusBadge status={full.status} />
              {full.preferredContactMethod && (
                <Badge tone="info">
                  Contact by {full.preferredContactMethod}
                </Badge>
              )}
              {full.billingContact && (
                <Badge tone="neutral">Billing contact</Badge>
              )}
            </>
          ) : undefined
        }
        actions={
          full && (canEdit || canDelete) ? (
            <>
              {canEdit && (
                <EditButton primary to={`/parents/${parent.id}/edit`} />
              )}
              {canDelete && <DeleteButton onClick={() => remove.ask(parent)} />}
            </>
          ) : undefined
        }
      />

      <Tabs
        idBase="parent"
        label="Parent details"
        tabs={tabs}
        active={activeTab}
        onChange={selectTab}
      />

      {activeTab === "overview" && (
        <TabPanel idBase="parent" id="overview">
          {!full && (
            <Notice icon={Lock} style={{ marginBottom: 16 }}>
              You see the contact details of the parents of your students. The
              full record is for office staff.
            </Notice>
          )}
          <div className="detail-cards">
            <DetailCard title="Contact">
              <KeyValueGrid
                items={[
                  {
                    label: "Mobile",
                    value: <a href={`tel:${parent.mobile}`}>{parent.mobile}</a>,
                  },
                  ...(full
                    ? [
                        {
                          label: "Email",
                          value: full.email && (
                            <a href={`mailto:${full.email}`}>{full.email}</a>
                          ),
                        },
                        { label: "WhatsApp", value: full.whatsapp },
                        {
                          label: "Preferred contact",
                          value: full.preferredContactMethod,
                        },
                        {
                          label: "Preferred language",
                          value: full.preferredLanguage,
                        },
                        {
                          label: "Emergencies only",
                          value: yesNo(full.emergencyOnly),
                        },
                      ]
                    : []),
                ]}
              />
            </DetailCard>
            {full && (
              <>
                <DetailCard title="Notifications">
                  <KeyValueGrid
                    items={[
                      {
                        label: "Attendance",
                        value: yesNo(full.notifyAttendance),
                      },
                      { label: "Exams", value: yesNo(full.notifyExams) },
                      { label: "Fees", value: yesNo(full.notifyFees) },
                      { label: "Notices", value: yesNo(full.notifyNotices) },
                      {
                        label: "Discipline",
                        value: yesNo(full.notifyDiscipline),
                      },
                    ]}
                  />
                </DetailCard>
                <DetailCard title="Identity & residence">
                  <KeyValueGrid
                    items={[
                      { label: "Occupation", value: full.occupation },
                      { label: "Nationality", value: full.nationality },
                      {
                        label: "Country of residence",
                        value: full.countryOfResidence,
                      },
                      { label: "Time zone", value: full.timezone },
                    ]}
                  />
                </DetailCard>
                <DetailCard title="Work & billing">
                  <KeyValueGrid
                    items={[
                      { label: "Employer", value: full.employer },
                      { label: "Job title", value: full.jobTitle },
                      { label: "Work email", value: full.workEmail },
                      { label: "Work phone", value: full.workPhone },
                      {
                        label: "Billing contact",
                        value: yesNo(full.billingContact),
                      },
                    ]}
                  />
                </DetailCard>
                <DetailCard title="Address">
                  <KeyValueGrid
                    items={[
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
              </>
            )}
          </div>
        </TabPanel>
      )}

      {activeTab === "children" && (
        <TabPanel idBase="parent" id="children">
          <DetailCard title="Children">
            {children.error && (
              <Notice tone="warn" style={{ marginBottom: 12 }}>
                {children.error}
              </Notice>
            )}
            {children.isLoading && !children.data && (
              <p role="status">Loading…</p>
            )}
            {children.data && linked.length === 0 && (
              <p className="cell-muted" style={{ marginTop: 0 }}>
                No children linked yet.
              </p>
            )}
            <ContactList>
              {linked.map((c) => (
                <ContactCard
                  key={c.studentId}
                  name={
                    canOpenStudents ? (
                      <Link to={`/students/${c.studentId}`}>
                        {c.studentName}
                      </Link>
                    ) : (
                      c.studentName
                    )
                  }
                  badges={
                    <>
                      <Badge tone="info">{c.relationType}</Badge>
                      {c.isPrimaryContact && (
                        <Badge tone="warn">Primary contact</Badge>
                      )}
                    </>
                  }
                  actions={
                    canChangeChildren ? (
                      <>
                        {!c.isPrimaryContact && (
                          <button
                            type="button"
                            className="btn btn--secondary btn--sm"
                            onClick={() => makePrimary.ask(c)}
                          >
                            Make primary
                          </button>
                        )}
                        <button
                          type="button"
                          className="btn btn--secondary btn--sm"
                          onClick={() => unlink.ask(c)}
                        >
                          Unlink
                        </button>
                      </>
                    ) : undefined
                  }
                >
                  <div className="contact-card__line">
                    Adm # {c.admNo} · {c.classSectionName} · Roll {c.rollNumber}
                  </div>
                </ContactCard>
              ))}
            </ContactList>
            {canChangeChildren && (
              <button
                type="button"
                className="btn btn--secondary"
                style={{ marginTop: 14 }}
                onClick={() => setLinking(true)}
              >
                <Link2 size={16} aria-hidden="true" />
                Link a child
              </button>
            )}
          </DetailCard>
        </TabPanel>
      )}

      {linking && full && (
        <LinkChildDialog
          parent={full}
          linked={linked}
          onClose={() => setLinking(false)}
          onLinked={async (child) => {
            toast.success(`${child.studentName} was linked to ${full.name}.`);
            await children.reload();
          }}
        />
      )}
      {remove.dialog}
      {unlink.dialog}
      {makePrimary.dialog}
    </div>
  );
}
