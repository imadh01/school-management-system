import { useEffect } from "react";
import { Link, useNavigate } from "react-router-dom";
import { KeyRound } from "lucide-react";
import { accountService } from "../services/accountService";
import { STAFF_GENDERS, type UserResponse } from "@/features/users/types/user.types";
import {
  blank,
  EMAIL_PATTERN,
  PHONE_MESSAGE,
  PHONE_PATTERN,
} from "@/features/users/utils/profileForm";
import { FormPage } from "@/components/form/FormPage";
import { FormSection } from "@/components/form/FormSection";
import { SelectField, TextField } from "@/components/form/Inputs";
import { AddressListEditor } from "@/components/form/AddressListEditor";
import { PageLoadError, PageLoading } from "@/components/PageState";
import { useAsyncData } from "@/hooks/useAsyncData";
import { useCountries } from "@/hooks/useCountries";
import { useRecordForm } from "@/hooks/useRecordForm";
import { useSchoolBasics } from "@/hooks/useSchoolBasics";
import { CHANGE_PASSWORD_PATH } from "@/routes/ProtectedRoute";
import type { AddressFormRow } from "@/types/address";
import { BLOOD_GROUPS } from "@/types/lookups";
import {
  toAddressRequests,
  toAddressRows,
  validateAddresses,
} from "@/utils/address";
import { formatDate } from "@/utils/format";
import { fieldBinder, type FormSectionDef } from "@/utils/form";

interface FormState {
  email: string;
  fullName: string;
  gender: string;
  dateOfBirth: string;
  mobile: string;
  qualification: string;
  bloodGroup: string;
  emergencyContactName: string;
  emergencyContactPhone: string;
  addresses: AddressFormRow[];
}

const SECTIONS: readonly FormSectionDef[] = [
  { id: "account", title: "Login account", fields: ["email"] },
  {
    id: "profile",
    title: "Personal info",
    fields: ["fullName", "gender", "dateOfBirth", "mobile", "qualification"],
  },
  {
    id: "emergency",
    title: "Health & emergency",
    fields: ["bloodGroup", "emergencyContactName", "emergencyContactPhone"],
  },
  { id: "address", title: "Addresses", fields: ["addresses"] },
  { id: "security", title: "Password", fields: [] },
];

const EMPTY_FORM: FormState = {
  email: "",
  fullName: "",
  gender: "",
  dateOfBirth: "",
  mobile: "",
  qualification: "",
  bloodGroup: "",
  emergencyContactName: "",
  emergencyContactPhone: "",
  addresses: [],
};

const formFrom = (u: UserResponse): FormState => ({
  email: u.email,
  fullName: u.profile?.fullName ?? "",
  gender: u.profile?.gender ?? "",
  dateOfBirth: u.profile?.dateOfBirth ?? "",
  mobile: u.profile?.mobile ?? "",
  qualification: u.profile?.qualification ?? "",
  bloodGroup: u.profile?.bloodGroup ?? "",
  emergencyContactName: u.profile?.emergencyContactName ?? "",
  emergencyContactPhone: u.profile?.emergencyContactPhone ?? "",
  addresses: toAddressRows(u.addresses),
});

/**
 * E3b: My Account (/my-account), open to every signed-in user. You change your own e-mail and
 * profile here; the username, role, status, joining date and employee number are shown but only an
 * administrator changes them. The password is changed on its own page, which also signs your other
 * devices out.
 */
export function MyAccountPage() {
  const navigate = useNavigate();
  const loaded = useAsyncData(
    () => accountService.getMine(),
    [],
    "Could not load your account.",
  );
  const countries = useCountries();
  const school = useSchoolBasics();
  const record = useRecordForm<FormState>({
    sections: SECTIONS,
    initial: EMPTY_FORM,
    noun: "account",
    onReload: () => void loaded.reload(),
  });
  const { reset } = record;

  const user = loaded.data ?? null;
  // Kept as the latest saved version: the next save needs its rowVersion.
  useEffect(() => {
    if (user) reset(formFrom(user));
  }, [user, reset]);

  if (loaded.error) {
    return (
      <PageLoadError
        title="Your account could not be opened"
        message={loaded.error}
        backTo="/dashboard"
        backLabel="Back to the dashboard"
      />
    );
  }
  if (!user || record.form === EMPTY_FORM) return <PageLoading />;

  const form = record.form;
  const bind = fieldBinder(form, record.patch, record.fieldErrors, "account");
  const hasProfile = user.profile !== null;

  const validate = () => {
    const e: Record<string, string> = {};
    if (!form.email.trim()) e.email = "Email is required.";
    else if (!EMAIL_PATTERN.test(form.email.trim()))
      e.email = "Enter a valid email address.";
    if (!form.fullName.trim()) e.fullName = "Full name is required.";
    if (!form.gender) e.gender = "Gender is required.";
    if (!form.mobile.trim()) e.mobile = "Mobile is required.";
    else if (!PHONE_PATTERN.test(form.mobile.trim())) e.mobile = PHONE_MESSAGE;
    if (
      form.emergencyContactPhone.trim() &&
      !PHONE_PATTERN.test(form.emergencyContactPhone.trim())
    )
      e.emergencyContactPhone = PHONE_MESSAGE;
    return { ...e, ...validateAddresses(form.addresses, countries.data ?? []) };
  };

  const save = () =>
    void record.save({
      validate,
      submit: () =>
        accountService.updateMine({
          email: form.email.trim(),
          fullName: form.fullName.trim(),
          gender: blank(form.gender),
          dateOfBirth: blank(form.dateOfBirth),
          mobile: blank(form.mobile),
          qualification: blank(form.qualification),
          bloodGroup: blank(form.bloodGroup),
          emergencyContactName: blank(form.emergencyContactName),
          emergencyContactPhone: blank(form.emergencyContactPhone),
          addresses: toAddressRequests(form.addresses),
          rowVersion: user.rowVersion,
        }),
      fallback: "Could not save your account.",
      onSaved: (saved) => ({
        form: formFrom(saved),
        message: "Your account was saved.",
        goTo: "/dashboard",
      }),
    });

  const role = user.roles[0] ?? "";

  return (
    <>
      {record.guard}
      <FormPage
        {...record.pageProps}
        title="My Account"
        subtitle={`@${user.username}${role ? ` · ${role}` : ""}`}
        back={{ to: "/dashboard", label: "Back to the dashboard" }}
        onSubmit={save}
        saveLabel="Save my account"
        onCancel={() => navigate("/dashboard")}
      >
        <FormSection
          id="account"
          title="Login account"
          description="Your username and role are set by an administrator."
        >
          <TextField
            id="account-username"
            label="Username"
            value={user.username}
            onChange={() => undefined}
            disabled
          />
          <TextField
            id="account-role"
            label="Role"
            value={role}
            onChange={() => undefined}
            disabled
          />
          <TextField {...bind("email")} label="Email" type="email" required />
        </FormSection>

        <FormSection id="profile" title="Personal info">
          {hasProfile ? (
            <>
              <TextField {...bind("fullName")} label="Full name" required />
              <SelectField
                {...bind("gender")}
                label="Gender"
                required
                emptyLabel="Choose…"
                options={STAFF_GENDERS}
              />
              <TextField
                {...bind("dateOfBirth")}
                label="Date of birth"
                type="date"
              />
              <TextField
                {...bind("mobile")}
                label="Mobile"
                type="tel"
                required
              />
              <TextField
                {...bind("qualification")}
                label="Qualification"
                maxLength={150}
              />
              <TextField
                id="account-joining"
                label="Joining date"
                value={formatDate(user.profile?.joiningDate ?? null) ?? ""}
                onChange={() => undefined}
                disabled
              />
              <TextField
                id="account-employee"
                label="Employee no."
                value={user.profile?.employeeNumber ?? ""}
                onChange={() => undefined}
                disabled
              />
            </>
          ) : (
            <p className="cell-muted">This account has no staff profile.</p>
          )}
        </FormSection>

        <FormSection id="emergency" title="Health & emergency">
          <SelectField
            {...bind("bloodGroup")}
            label="Blood group"
            emptyLabel="Not recorded"
            options={BLOOD_GROUPS}
          />
          <TextField
            {...bind("emergencyContactName")}
            label="Emergency contact"
            maxLength={150}
          />
          <TextField
            {...bind("emergencyContactPhone")}
            label="Emergency phone"
            type="tel"
          />
        </FormSection>

        <FormSection id="address" title="Addresses" grid={false}>
          <AddressListEditor
            idPrefix="account"
            rows={form.addresses}
            onChange={(addresses) => record.patch({ addresses })}
            countries={countries.data ?? []}
            countriesLoading={countries.isLoading}
            errors={record.fieldErrors}
            defaultCountryId={
              school?.countryId ? String(school.countryId) : undefined
            }
          />
        </FormSection>

        <FormSection
          id="security"
          title="Password"
          description="Changing it signs you out on your other devices."
          grid={false}
        >
          <Link to={CHANGE_PASSWORD_PATH} className="btn btn--secondary">
            <KeyRound size={16} aria-hidden="true" />
            Change password
          </Link>
        </FormSection>
      </FormPage>
    </>
  );
}
