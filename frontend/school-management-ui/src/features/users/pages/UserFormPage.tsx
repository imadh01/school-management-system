import { useEffect, useMemo, useState } from "react";
import { useNavigate, useParams, useSearchParams } from "react-router-dom";
import { userService } from "../services/userService";
import {
  PORTAL_ROLES,
  STAFF_GENDERS,
  TEACHER_ROLE,
  type StaffProfileRequest,
  type UserResponse,
} from "../types/user.types";
import { SIGNS_OUT_EVERYWHERE } from "../components/AccountActionDialogs";
import { ConfirmDialog } from "@/components/ConfirmDialog";
import { FormPage } from "@/components/form/FormPage";
import { FormSection } from "@/components/form/FormSection";
import { SelectField, TextField } from "@/components/form/Inputs";
import { PasswordRules } from "@/components/form/PasswordRules";
import { AddressListEditor } from "@/components/form/AddressListEditor";
import { PageLoadError, PageLoading } from "@/components/PageState";
import { useAsyncData } from "@/hooks/useAsyncData";
import { useCan } from "@/hooks/useCan";
import { useCountries } from "@/hooks/useCountries";
import { useRecordForm } from "@/hooks/useRecordForm";
import { Permission } from "@/routes/permissions";
import { useAuth } from "@/store/AuthContext";
import type { AddressFormRow } from "@/types/address";
import { BLOOD_GROUPS } from "@/types/lookups";
import {
  toAddressRequests,
  toAddressRows,
  validateAddresses,
} from "@/utils/address";
import { fieldBinder, type FormSectionDef } from "@/utils/form";
import { meetsPasswordRules } from "@/utils/password";
import { useSchoolBasics } from "@/hooks/useSchoolBasics";
import {
  blank,
  EMAIL_PATTERN,
  PHONE_MESSAGE,
  PHONE_PATTERN,
  remapProfileErrors,
} from "../utils/profileForm";

/** The built-in staff roles, offered when the user may not read the roles list (Roles.View). */
const BUILT_IN_ROLES = ["Admin", "Supervisor", "Clerk", TEACHER_ROLE];

interface FormState {
  username: string;
  email: string;
  /** New account only: the temporary password. Cleared after saving, never shown again. */
  password: string;
  roleName: string;
  specialization: string;
  fullName: string;
  gender: string;
  dateOfBirth: string;
  mobile: string;
  joiningDate: string;
  qualification: string;
  employeeNumber: string;
  bloodGroup: string;
  emergencyContactName: string;
  emergencyContactPhone: string;
  addresses: AddressFormRow[];
}

const PROFILE_FIELDS = [
  "fullName",
  "gender",
  "dateOfBirth",
  "mobile",
  "joiningDate",
  "qualification",
  "employeeNumber",
] as const;

// Field keys as the API names them after the "profile." part is removed (see remapProfileErrors).
const SECTIONS: readonly FormSectionDef[] = [
  {
    id: "account",
    title: "Login account",
    fields: ["username", "email", "password", "roleName", "specialization"],
  },
  { id: "profile", title: "Profile", fields: PROFILE_FIELDS },
  {
    id: "emergency",
    title: "Health & emergency",
    fields: ["bloodGroup", "emergencyContactName", "emergencyContactPhone"],
  },
  { id: "address", title: "Addresses", fields: ["addresses"] },
];

const EMPTY_FORM: FormState = {
  username: "",
  email: "",
  password: "",
  roleName: "",
  specialization: "",
  fullName: "",
  gender: "",
  dateOfBirth: "",
  mobile: "",
  joiningDate: "",
  qualification: "",
  employeeNumber: "",
  bloodGroup: "",
  emergencyContactName: "",
  emergencyContactPhone: "",
  addresses: [],
};

const formFrom = (u: UserResponse): FormState => ({
  ...EMPTY_FORM,
  username: u.username,
  email: u.email,
  roleName: u.roles[0] ?? "",
  fullName: u.profile?.fullName ?? "",
  gender: u.profile?.gender ?? "",
  dateOfBirth: u.profile?.dateOfBirth ?? "",
  mobile: u.profile?.mobile ?? "",
  joiningDate: u.profile?.joiningDate ?? "",
  qualification: u.profile?.qualification ?? "",
  employeeNumber: u.profile?.employeeNumber ?? "",
  bloodGroup: u.profile?.bloodGroup ?? "",
  emergencyContactName: u.profile?.emergencyContactName ?? "",
  emergencyContactPhone: u.profile?.emergencyContactPhone ?? "",
  addresses: toAddressRows(u.addresses),
});

const profileOf = (f: FormState): StaffProfileRequest => ({
  fullName: f.fullName.trim(),
  gender: blank(f.gender),
  dateOfBirth: blank(f.dateOfBirth),
  mobile: blank(f.mobile),
  joiningDate: blank(f.joiningDate),
  qualification: blank(f.qualification),
  employeeNumber: blank(f.employeeNumber),
  bloodGroup: blank(f.bloodGroup),
  emergencyContactName: blank(f.emergencyContactName),
  emergencyContactPhone: blank(f.emergencyContactPhone),
});

/**
 * E3a: adding a staff account (/users-staff/new) and editing one (/users-staff/:id/edit).
 * `?role=Teacher` pre-selects the role (the Teachers page's "Add teacher" comes here).
 *
 * The temporary password exists only in this form while it is typed: after saving the form is
 * reset from the API's answer, which never contains it, and the next page does not show it.
 * A different role on an existing account asks first: it signs the user out everywhere.
 */
export function UserFormPage() {
  const { id } = useParams();
  const isNew = id === undefined;
  const userId = Number(id);
  const navigate = useNavigate();
  const [params] = useSearchParams();
  const can = useCan();
  const { user: me } = useAuth();
  const isMe = !isNew && userId === me?.userId;

  const loaded = useAsyncData(
    () => (isNew ? Promise.resolve(null) : userService.getById(userId)),
    [isNew, userId],
    "Could not load this user.",
  );
  const roles = useAsyncData(
    () =>
      can(Permission.RolesView)
        ? userService.getRoles().then((list) =>
            list.filter((r) => r.isActive).map((r) => r.name),
          )
        : Promise.resolve(BUILT_IN_ROLES),
    [],
    "Could not load the roles.",
  );
  const countries = useCountries();
  const school = useSchoolBasics();
  const record = useRecordForm<FormState>({
    sections: SECTIONS,
    initial: EMPTY_FORM,
    noun: "user",
    onReload: () => void loaded.reload(),
  });
  const { reset, patch } = record;
  const [confirmRole, setConfirmRole] = useState(false);

  const user = loaded.data ?? null;
  useEffect(() => {
    if (user) reset(formFrom(user));
  }, [user, reset]);

  // New account: the role from ?role= (Teachers page), once.
  const presetRole = params.get("role");
  useEffect(() => {
    if (isNew && presetRole && !PORTAL_ROLES.includes(presetRole))
      reset({ ...EMPTY_FORM, roleName: presetRole });
  }, [isNew, presetRole, reset]);

  // Student/Parent are never offered (decision 1). The account's current role stays listed.
  const roleOptions = useMemo(() => {
    const names = (roles.data ?? BUILT_IN_ROLES).filter(
      (r) => !PORTAL_ROLES.includes(r),
    );
    const current = user?.roles[0];
    return current && !names.includes(current) ? [current, ...names] : names;
  }, [roles.data, user]);

  if (loaded.error) {
    return (
      <PageLoadError
        title="This user could not be opened"
        message={loaded.error}
        backTo="/users-staff"
        backLabel="Back to users"
      />
    );
  }
  if (loaded.isLoading || (user && record.form === EMPTY_FORM))
    return <PageLoading />;

  const form = record.form;
  const bind = fieldBinder(form, patch, record.fieldErrors, "user");
  const originalRole = user?.roles[0] ?? "";
  const roleChanged = !!user && form.roleName !== originalRole;
  const isTeacher = form.roleName === TEACHER_ROLE;

  const validate = () => {
    const e: Record<string, string> = {};
    if (!form.username.trim()) e.username = "Username is required.";
    else if (!/^[a-zA-Z0-9._-]+$/.test(form.username.trim()))
      e.username =
        "Username can only contain letters, digits, dots, underscores and hyphens.";
    if (!form.email.trim()) e.email = "Email is required.";
    else if (!EMAIL_PATTERN.test(form.email.trim()))
      e.email = "Enter a valid email address.";
    if (!user && !meetsPasswordRules(form.password))
      e.password = "The password does not meet all the rules below.";
    if (!form.roleName) e.roleName = "Choose a role.";
    if (!form.fullName.trim()) e.fullName = "Full name is required.";
    if (!form.gender) e.gender = "Gender is required.";
    if (!form.mobile.trim()) e.mobile = "Mobile is required.";
    else if (!PHONE_PATTERN.test(form.mobile.trim()))
      e.mobile = PHONE_MESSAGE;
    if (!form.joiningDate) e.joiningDate = "Joining date is required.";
    if (
      form.emergencyContactPhone.trim() &&
      !PHONE_PATTERN.test(form.emergencyContactPhone.trim())
    )
      e.emergencyContactPhone = PHONE_MESSAGE;
    return { ...e, ...validateAddresses(form.addresses, countries.data ?? []) };
  };

  const save = () => {
    const common = {
      username: form.username.trim(),
      email: form.email.trim(),
      roleName: form.roleName,
      profile: profileOf(form),
      addresses: toAddressRequests(form.addresses),
    };
    void record.save({
      validate,
      submit: () =>
        user
          ? userService.update(user.id, { ...common, rowVersion: user.rowVersion })
          : userService.create({
              ...common,
              password: form.password,
              specialization: isTeacher ? blank(form.specialization) : null,
            }),
      fallback: "Could not save this user.",
      remap: remapProfileErrors,
      onSaved: (saved) => ({
        // From the API's answer: the temporary password is gone from the form.
        form: formFrom(saved),
        message: user
          ? roleChanged
            ? `${saved.profile?.fullName ?? saved.username} was saved and signed out everywhere (new role: ${saved.roles[0]}).`
            : `${saved.profile?.fullName ?? saved.username} was saved.`
          : `${saved.profile?.fullName ?? saved.username} was added. They must choose their own password at the first sign-in.`,
        goTo: `/users-staff/${saved.id}`,
      }),
    });
  };

  // A different role signs the user out everywhere: ask first (only when the browser checks pass).
  const handleSave = () => {
    if (roleChanged && Object.keys(validate()).length === 0) setConfirmRole(true);
    else save();
  };

  const backTo = user ? `/users-staff/${user.id}` : "/users-staff";
  const roleHint = isMe
    ? "You cannot change your own role. Ask another administrator."
    : user
      ? "A different role signs the user out everywhere."
      : isTeacher
        ? "Also creates the teacher record; classes and subjects are assigned on the Teachers page."
        : undefined;

  return (
    <>
      {record.guard}
      <FormPage
        {...record.pageProps}
        title={
          user
            ? `Edit ${user.profile?.fullName ?? user.username}`
            : isTeacher
              ? "Add teacher"
              : "Add user"
        }
        subtitle={
          user
            ? `@${user.username}`
            : "The password you type is temporary: the user chooses their own at the first sign-in."
        }
        back={{
          to: backTo,
          label: user ? "Back to user" : "Back to users",
        }}
        onSubmit={handleSave}
        saveLabel={user ? "Save changes" : "Save user"}
        onCancel={() => navigate(backTo)}
      >
        <FormSection
          id="account"
          title="Login account"
          description="The user signs in with this username or email."
        >
          <TextField
            {...bind("username")}
            label="Username"
            required
            autoComplete="off"
          />
          <TextField
            {...bind("email")}
            label="Email"
            type="email"
            required
            autoComplete="off"
          />
          <SelectField
            {...bind("roleName")}
            label="Role"
            required
            disabled={isMe}
            emptyLabel={
              roles.isLoading && !roles.data ? "Loading…" : "Choose a role…"
            }
            options={roleOptions}
            hint={roleHint}
          />
          {!user && isTeacher && (
            <TextField
              {...bind("specialization")}
              label="Specialization"
              placeholder="e.g. Mathematics"
            />
          )}
          {!user && (
            <>
              <TextField
                {...bind("password")}
                label="Temporary password"
                type="password"
                required
                autoComplete="new-password"
                hint="Give it to the user in person. It is not shown again after saving."
              />
              <div className="field">
                <PasswordRules password={form.password} />
              </div>
            </>
          )}
        </FormSection>

        <FormSection id="profile" title="Profile">
          <TextField {...bind("fullName")} label="Full name" required />
          <SelectField
            {...bind("gender")}
            label="Gender"
            required
            emptyLabel="Choose…"
            options={STAFF_GENDERS}
          />
          <TextField {...bind("dateOfBirth")} label="Date of birth" type="date" />
          <TextField
            {...bind("mobile")}
            label="Mobile"
            type="tel"
            required
            placeholder="+971 50 0000000"
          />
          <TextField
            {...bind("joiningDate")}
            label="Joining date"
            type="date"
            required
          />
          <TextField
            {...bind("employeeNumber")}
            label="Employee no."
            maxLength={30}
            placeholder="Automatic"
            hint="Leave blank to generate one (EMP-0001, EMP-0002 ...). Must be unique."
          />
          <TextField
            {...bind("qualification")}
            label="Qualification"
            placeholder="e.g. B.Ed"
            maxLength={150}
          />
        </FormSection>

        <FormSection
          id="emergency"
          title="Health & emergency"
          description="Kept out of the audit log's readable values."
        >
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

        <FormSection
          id="address"
          title="Addresses"
          grid={false}
          description="Home, Permanent and Mailing: one of each, one of them primary."
        >
          <AddressListEditor
            idPrefix="user"
            rows={form.addresses}
            onChange={(addresses) => patch({ addresses })}
            countries={countries.data ?? []}
            countriesLoading={countries.isLoading}
            defaultCountryId={
              school?.countryId ? String(school.countryId) : undefined
            }
            errors={record.fieldErrors}
          />
        </FormSection>
      </FormPage>

      <ConfirmDialog
        isOpen={confirmRole}
        title="Change role"
        message={`Change the role from ${originalRole || "none"} to ${form.roleName}? ${SIGNS_OUT_EVERYWHERE}${
          originalRole === TEACHER_ROLE
            ? " The teacher record is removed too (refused while they still teach a subject or class)."
            : isTeacher
              ? " A teacher record is created for them."
              : ""
        }`}
        confirmLabel="Change role and save"
        danger
        onConfirm={() => {
          setConfirmRole(false);
          save();
        }}
        onCancel={() => setConfirmRole(false)}
      />
    </>
  );
}
