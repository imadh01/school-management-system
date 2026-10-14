import { useEffect, useMemo } from "react";
import { useNavigate } from "react-router-dom";
import { settingsService } from "../services/settingsService";
import {
  WEEK_DAYS,
  type SchoolSettings,
  type TimeZoneOption,
} from "../types/settings.types";
import { FormPage } from "@/components/form/FormPage";
import { FormSection } from "@/components/form/FormSection";
import {
  SelectField,
  TextAreaField,
  TextField,
  ToggleGroup,
} from "@/components/form/Inputs";
import { Notice } from "@/components/Notice";
import { PageLoadError, PageLoading } from "@/components/PageState";
import { useAsyncData } from "@/hooks/useAsyncData";
import { useCan } from "@/hooks/useCan";
import { useCountries } from "@/hooks/useCountries";
import { useRecordForm } from "@/hooks/useRecordForm";
import { Permission } from "@/routes/permissions";
import {
  blank,
  EMAIL_PATTERN,
  PHONE_MESSAGE,
  PHONE_PATTERN,
} from "@/features/users/utils/profileForm";
import { fieldBinder, type FormSectionDef } from "@/utils/form";

interface FormState {
  schoolName: string;
  shortName: string;
  email: string;
  phone: string;
  website: string;
  addressText: string;
  countryId: string;
  timeZoneId: string;
  currencyCode: string;
  workingDays: string[];
  aboutTagline: string;
  rowVersion: string;
}

const SECTIONS: readonly FormSectionDef[] = [
  { id: "identity", title: "Identity", fields: ["schoolName", "shortName"] },
  {
    id: "contact",
    title: "Contact",
    fields: ["email", "phone", "website", "addressText", "countryId"],
  },
  {
    id: "defaults",
    title: "Time zone & currency",
    fields: ["timeZoneId", "currencyCode"],
  },
  { id: "calendar", title: "Working days", fields: ["workingDays"] },
  { id: "about", title: "About", fields: ["aboutTagline"] },
];

const EMPTY_FORM: FormState = {
  schoolName: "",
  shortName: "",
  email: "",
  phone: "",
  website: "",
  addressText: "",
  countryId: "",
  timeZoneId: "",
  currencyCode: "",
  workingDays: [],
  aboutTagline: "",
  rowVersion: "",
};

const formFrom = (s: SchoolSettings): FormState => ({
  schoolName: s.schoolName,
  shortName: s.shortName ?? "",
  email: s.email ?? "",
  phone: s.phone ?? "",
  website: s.website ?? "",
  addressText: s.addressText ?? "",
  countryId: s.countryId === null ? "" : String(s.countryId),
  timeZoneId: s.timeZoneId,
  currencyCode: s.currencyCode,
  workingDays: s.workingDays,
  aboutTagline: s.aboutTagline ?? "",
  rowVersion: s.rowVersion,
});

const offsetLabel = (minutes: number) => {
  const sign = minutes < 0 ? "-" : "+";
  const abs = Math.abs(minutes);
  const hh = String(Math.floor(abs / 60)).padStart(2, "0");
  const mm = String(abs % 60).padStart(2, "0");
  return `UTC${sign}${hh}:${mm}`;
};

const optionOf = (z: TimeZoneOption) => ({
  value: z.id,
  label: `${z.displayName} (${offsetLabel(z.utcOffsetMinutes)})`,
});

/**
 * E3b: Settings → School settings (/settings). The school's own record: name, contact details,
 * time zone, currency, working days and the About tagline. Only the School settings part of the
 * prototype's Settings page is built (Personal, Theme and Maintenance come later). The time zone is
 * an IANA id picked from the API's list; the working days are saved Monday first.
 */
export function SchoolSettingsPage() {
  const navigate = useNavigate();
  const canEdit = useCan()(Permission.SettingsEdit);

  const loaded = useAsyncData(
    () => settingsService.getSchool(),
    [],
    "Could not load the school settings.",
  );
  const zones = useAsyncData(
    () => settingsService.getTimeZones(),
    [],
    "Could not load the list of time zones.",
  );
  const countries = useCountries();
  const record = useRecordForm<FormState>({
    sections: SECTIONS,
    initial: EMPTY_FORM,
    noun: "school record",
    onReload: () => void loaded.reload(),
  });
  const { reset } = record;

  const settings = loaded.data ?? null;
  useEffect(() => {
    if (settings) reset(formFrom(settings));
  }, [settings, reset]);

  const form = record.form;
  const zoneOptions = useMemo(() => {
    const list = (zones.data ?? []).map(optionOf);
    // A stored zone that is not in the list stays selectable, so it is not silently changed.
    return form.timeZoneId && !list.some((o) => o.value === form.timeZoneId)
      ? [{ value: form.timeZoneId, label: form.timeZoneId }, ...list]
      : list;
  }, [zones.data, form.timeZoneId]);

  if (loaded.error) {
    return (
      <PageLoadError
        title="The school settings could not be opened"
        message={loaded.error}
        backTo="/dashboard"
        backLabel="Back to the dashboard"
      />
    );
  }
  if (!settings || form === EMPTY_FORM) return <PageLoading />;

  const bind = fieldBinder(form, record.patch, record.fieldErrors, "school");
  const disabled = !canEdit;

  const toggleDay = (day: string) =>
    record.patch({
      workingDays: form.workingDays.includes(day)
        ? form.workingDays.filter((d) => d !== day)
        : [...form.workingDays, day],
    });

  const validate = () => {
    const e: Record<string, string> = {};
    if (!form.schoolName.trim()) e.schoolName = "School name is required.";
    if (form.email.trim() && !EMAIL_PATTERN.test(form.email.trim()))
      e.email = "Enter a valid email address.";
    if (form.phone.trim() && !PHONE_PATTERN.test(form.phone.trim()))
      e.phone = PHONE_MESSAGE;
    if (form.website.trim() && !/^https?:\/\/\S+$/i.test(form.website.trim()))
      e.website = "Website must be a full address starting with http:// or https://.";
    if (!form.timeZoneId) e.timeZoneId = "Choose a time zone.";
    if (!/^[A-Z]{3}$/.test(form.currencyCode))
      e.currencyCode =
        "Currency must be a three-letter code in capitals, for example AED or INR.";
    if (form.workingDays.length === 0)
      e.workingDays = "Choose at least one working day.";
    return e;
  };

  const save = () =>
    void record.save({
      validate,
      submit: () =>
        settingsService.updateSchool({
          schoolName: form.schoolName.trim(),
          shortName: blank(form.shortName),
          email: blank(form.email),
          phone: blank(form.phone),
          website: blank(form.website),
          addressText: blank(form.addressText),
          countryId: form.countryId ? Number(form.countryId) : null,
          timeZoneId: form.timeZoneId,
          currencyCode: form.currencyCode,
          // Monday first, whatever order they were clicked in.
          workingDays: WEEK_DAYS.filter((d) => form.workingDays.includes(d)),
          aboutTagline: blank(form.aboutTagline),
          rowVersion: form.rowVersion,
        }),
      fallback: "Could not save the school settings.",
      onSaved: (saved) => ({
        form: formFrom(saved),
        message: "The school settings were saved.",
        goTo: "/settings",
      }),
    });

  return (
    <>
      {record.guard}
      <FormPage
        {...record.pageProps}
        title="Settings"
        subtitle="School settings: shown on the top bar, receipts and exports, and used for dates and attendance."
        back={{ to: "/dashboard", label: "Back to the dashboard" }}
        onSubmit={save}
        saveLabel="Save school settings"
        onCancel={() => navigate("/dashboard")}
      >
        {disabled && (
          <Notice tone="info" style={{ marginBottom: 16 }}>
            You can view the school settings but not change them.
          </Notice>
        )}
        <FormSection id="identity" title="Identity">
          <TextField
            {...bind("schoolName")}
            label="School name"
            required
            maxLength={150}
            disabled={disabled}
          />
          <TextField
            {...bind("shortName")}
            label="Short name"
            maxLength={30}
            hint="The abbreviation shown in the top bar."
            disabled={disabled}
          />
        </FormSection>

        <FormSection id="contact" title="Contact">
          <TextField
            {...bind("email")}
            label="School email"
            type="email"
            disabled={disabled}
          />
          <TextField
            {...bind("phone")}
            label="Contact number"
            type="tel"
            disabled={disabled}
          />
          <TextField
            {...bind("website")}
            label="Website"
            type="url"
            placeholder="https://"
            span={2}
            disabled={disabled}
          />
          <TextAreaField
            {...bind("addressText")}
            label="Address"
            rows={2}
            span={2}
            disabled={disabled}
          />
          <SelectField
            {...bind("countryId")}
            label="Country"
            emptyLabel={countries.isLoading ? "Loading…" : "Not set"}
            options={(countries.data ?? []).map((c) => ({
              value: c.id,
              label: c.name,
            }))}
            hint="Address forms suggest this country."
            disabled={disabled}
          />
        </FormSection>

        <FormSection
          id="defaults"
          title="Time zone & currency"
          description="The time zone decides what 'today' means for attendance and dates."
        >
          <SelectField
            {...bind("timeZoneId")}
            label="Time zone"
            required
            emptyLabel={zones.isLoading ? "Loading…" : "Choose a time zone…"}
            options={zoneOptions}
            disabled={disabled}
          />
          <TextField
            {...bind("currencyCode")}
            label="Currency code"
            required
            maxLength={3}
            placeholder="AED"
            hint="Three capital letters (ISO 4217)."
            disabled={disabled}
            // Typed in lower case is turned into capitals as they type.
            onChange={(v) =>
              record.patch({ currencyCode: v.toUpperCase().replace(/[^A-Z]/g, "") })
            }
          />
        </FormSection>

        <FormSection
          id="calendar"
          title="Working days"
          description="Days the school is open: drives attendance percentages and timetable defaults."
          grid={false}
        >
          <ToggleGroup
            legend="Working days"
            hideLegend
            options={WEEK_DAYS}
            isSelected={(d) => form.workingDays.includes(d)}
            onToggle={toggleDay}
            isDisabled={() => disabled}
          />
          {record.fieldErrors.workingDays && (
            <p className="field__error">{record.fieldErrors.workingDays}</p>
          )}
        </FormSection>

        <FormSection id="about" title="About">
          <TextAreaField
            {...bind("aboutTagline")}
            label="Tagline shown on the About App page"
            rows={2}
            span="full"
            disabled={disabled}
          />
        </FormSection>
      </FormPage>
    </>
  );
}
