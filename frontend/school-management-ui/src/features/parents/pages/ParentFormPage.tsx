import { useEffect } from "react";
import { Link, useNavigate, useParams } from "react-router-dom";
import { Lock } from "lucide-react";
import { parentService } from "../services/parentService";
import {
  isFullParent,
  type CreateParentRequest,
  type ParentResponse,
} from "../types/parent.types";
import { FormPage } from "@/components/form/FormPage";
import { FormSection } from "@/components/form/FormSection";
import {
  CheckboxField,
  SelectField,
  TextField,
  ToggleGroup,
} from "@/components/form/Inputs";
import { EmptyState } from "@/components/EmptyState";
import { PageLoadError, PageLoading } from "@/components/PageState";
import { useAsyncData } from "@/hooks/useAsyncData";
import { useRecordForm } from "@/hooks/useRecordForm";
import { CONTACT_METHODS, LANGUAGES } from "@/types/lookups";
import { blankToNull, fieldBinder, type FormSectionDef } from "@/utils/form";

type FormState = CreateParentRequest & { status: "Active" | "Inactive" };
type NotifyKey =
  | "notifyAttendance"
  | "notifyExams"
  | "notifyFees"
  | "notifyNotices"
  | "notifyDiscipline";

/** The sections of the old Add/Edit parent dialog, in the same order. */
const SECTIONS: readonly FormSectionDef[] = [
  {
    id: "basics",
    title: "Basics",
    fields: ["name", "mobile", "email", "status", "occupation"],
  },
  {
    id: "residence",
    title: "Identity & residence",
    fields: ["nationality", "countryOfResidence", "timezone"],
  },
  {
    id: "communication",
    title: "Communication",
    fields: [
      "preferredLanguage",
      "preferredContactMethod",
      "whatsapp",
      "emergencyOnly",
      "notify",
    ],
  },
  {
    id: "work",
    title: "Work & billing",
    fields: [
      "employer",
      "jobTitle",
      "workEmail",
      "workPhone",
      "billingContact",
    ],
  },
  {
    id: "address",
    title: "Address",
    fields: ["addressLine", "city", "state", "pincode"],
  },
];

/** The "Notify me about" toggles: label on screen → field. */
const NOTIFY: Record<string, NotifyKey> = {
  Attendance: "notifyAttendance",
  Exams: "notifyExams",
  Fees: "notifyFees",
  Notices: "notifyNotices",
  Discipline: "notifyDiscipline",
};

const EMPTY_FORM: FormState = {
  name: "",
  email: null,
  mobile: "",
  status: "Active",
  occupation: null,
  nationality: null,
  countryOfResidence: null,
  timezone: null,
  preferredLanguage: "English",
  preferredContactMethod: "Email",
  whatsapp: null,
  emergencyOnly: false,
  notifyAttendance: true,
  notifyExams: true,
  notifyFees: true,
  notifyNotices: true,
  notifyDiscipline: true,
  employer: null,
  jobTitle: null,
  workEmail: null,
  workPhone: null,
  billingContact: false,
  addressLine: null,
  city: null,
  state: null,
  pincode: null,
};

function formFrom(p: ParentResponse): FormState {
  // Every editable field of the response, without id and rowVersion.
  const { id: _id, rowVersion: _rowVersion, ...fields } = p;
  return fields;
}

/**
 * U2: adding a parent (/parents/new) and editing one (/parents/:id/edit) on a full page. Children
 * are linked from the parent's own page, not here.
 */
export function ParentFormPage() {
  const { id } = useParams();
  const isNew = id === undefined;
  const parentId = Number(id);
  const navigate = useNavigate();

  const loaded = useAsyncData(
    () => (isNew ? Promise.resolve(null) : parentService.getById(parentId)),
    [isNew, parentId],
    "Could not load this parent.",
  );
  const record = useRecordForm<FormState>({
    sections: SECTIONS,
    initial: EMPTY_FORM,
    noun: "parent",
    onReload: () => void loaded.reload(),
  });
  const { reset } = record;

  const parent = loaded.data && isFullParent(loaded.data) ? loaded.data : null;
  useEffect(() => {
    if (parent) reset(formFrom(parent));
  }, [parent, reset]);

  if (loaded.error) {
    return (
      <PageLoadError
        title="This parent could not be opened"
        message={loaded.error}
        backTo="/parents"
        backLabel="Back to parents"
      />
    );
  }
  if (loaded.data && !parent) {
    // A teacher (Parents.ViewScoped) got the contact-only record: they can look, not edit.
    return (
      <div className="card">
        <EmptyState
          icon={Lock}
          title="You can view this parent but not edit them"
          action={
            <Link className="btn btn--primary" to={`/parents/${parentId}`}>
              Open the parent
            </Link>
          }
        />
      </div>
    );
  }
  if (loaded.isLoading || (parent && record.form === EMPTY_FORM))
    return <PageLoading />;

  const form = record.form;
  const bind = fieldBinder(form, record.patch, record.fieldErrors, "parent");

  const validate = () => {
    const e: Record<string, string> = {};
    if (!form.name.trim()) e.name = "Full name is required.";
    if (!form.mobile.trim()) e.mobile = "Mobile is required.";
    return e;
  };

  const handleSave = () => {
    const request = {
      ...blankToNull(form),
      name: form.name.trim(),
      mobile: form.mobile.trim(),
    };
    void record.save({
      validate,
      submit: () =>
        parent
          ? parentService.update(parent.id, {
              ...request,
              rowVersion: parent.rowVersion,
            })
          : parentService.create(request),
      fallback: "Could not save this parent.",
      onSaved: (saved) => ({
        form: formFrom(saved),
        message: parent
          ? `${saved.name} was saved.`
          : `${saved.name} was added.`,
        goTo: `/parents/${saved.id}`,
      }),
    });
  };

  const backTo = parent ? `/parents/${parent.id}` : "/parents";

  return (
    <>
      {record.guard}
      <FormPage
        {...record.pageProps}
        title={parent ? `Edit ${parent.name}` : "Add parent"}
        subtitle={
          parent
            ? parent.mobile
            : "Children are linked from the parent's page after saving."
        }
        back={{
          to: backTo,
          label: parent ? "Back to parent" : "Back to parents",
        }}
        onSubmit={handleSave}
        saveLabel={parent ? "Save changes" : "Save parent"}
        onCancel={() => navigate(backTo)}
      >
        <FormSection id="basics" title="Basics">
          <TextField
            {...bind("name")}
            label="Full name"
            required
            autoComplete="off"
          />
          <TextField
            {...bind("mobile")}
            label="Mobile"
            type="tel"
            required
            placeholder="+971 50 0000000"
            hint="Must be unique: it is how guardians are matched at enrolment."
          />
          <TextField
            {...bind("email")}
            label="Email"
            type="email"
            hint="Must be unique when given."
          />
          {parent && (
            <SelectField
              {...bind("status")}
              label="Status"
              options={["Active", "Inactive"]}
            />
          )}
          <TextField {...bind("occupation")} label="Occupation" />
        </FormSection>

        <FormSection id="residence" title="Identity & residence">
          <TextField {...bind("nationality")} label="Nationality" />
          <TextField
            {...bind("countryOfResidence")}
            label="Country of residence"
          />
          <TextField
            {...bind("timezone")}
            label="Time zone"
            placeholder="UTC"
          />
        </FormSection>

        <FormSection id="communication" title="Communication">
          <SelectField
            {...bind("preferredLanguage")}
            label="Preferred language"
            options={LANGUAGES}
            emptyLabel="Not specified"
          />
          <SelectField
            {...bind("preferredContactMethod")}
            label="Preferred contact method"
            options={CONTACT_METHODS}
            emptyLabel="Not specified"
          />
          <TextField {...bind("whatsapp")} label="WhatsApp number" type="tel" />
          <CheckboxField
            id="parent-emergencyOnly"
            label="Contact in emergencies only"
            checked={form.emergencyOnly}
            onChange={(v) => record.patch({ emergencyOnly: v })}
          />
          <div className="field field--full">
            <ToggleGroup
              legend="Notify about"
              options={Object.keys(NOTIFY)}
              isSelected={(label) => form[NOTIFY[label]]}
              onToggle={(label) =>
                record.patch({ [NOTIFY[label]]: !form[NOTIFY[label]] })
              }
            />
          </div>
        </FormSection>

        <FormSection id="work" title="Work & billing">
          <TextField {...bind("employer")} label="Employer" />
          <TextField {...bind("jobTitle")} label="Job title" />
          <TextField {...bind("workEmail")} label="Work email" type="email" />
          <TextField {...bind("workPhone")} label="Work phone" type="tel" />
          <div className="field field--full">
            <CheckboxField
              id="parent-billingContact"
              label="Billing contact: send fee invoices and billing notices to this parent"
              checked={form.billingContact}
              onChange={(v) => record.patch({ billingContact: v })}
            />
          </div>
        </FormSection>

        <FormSection id="address" title="Address">
          <TextField {...bind("addressLine")} label="Street address" span={2} />
          <TextField {...bind("city")} label="City" />
          <TextField {...bind("state")} label="State" />
          <TextField {...bind("pincode")} label="PIN / ZIP code" />
        </FormSection>
      </FormPage>
    </>
  );
}
