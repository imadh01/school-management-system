import { useEffect } from "react";
import { useNavigate, useParams } from "react-router-dom";
import { teacherService } from "../services/teacherService";
import {
  TEACHER_STATUSES,
  type TeacherResponse,
  type TeacherStatus,
} from "../types/teacher.types";
import { FormPage } from "@/components/form/FormPage";
import { FormSection } from "@/components/form/FormSection";
import { SelectField, TextField } from "@/components/form/Inputs";
import { PasswordRules } from "@/components/form/PasswordRules";
import { PageLoadError, PageLoading } from "@/components/PageState";
import { useAsyncData } from "@/hooks/useAsyncData";
import { useRecordForm } from "@/hooks/useRecordForm";
import { fieldBinder, type FormSectionDef } from "@/utils/form";
import { meetsPasswordRules } from "@/utils/password";

interface FormState {
  name: string;
  specialization: string;
  phone: string;
  status: TeacherStatus;
  username: string;
  email: string;
  /** New teacher: the first password (required). Edit: a new password, blank = unchanged. */
  password: string;
}

const SECTIONS: readonly FormSectionDef[] = [
  {
    id: "profile",
    title: "Profile",
    fields: ["name", "specialization", "phone", "status"],
  },
  {
    id: "login",
    title: "Login account",
    fields: ["username", "email", "password", "newPassword"],
  },
];

const EMPTY_FORM: FormState = {
  name: "",
  specialization: "",
  phone: "",
  status: "Active",
  username: "",
  email: "",
  password: "",
};

const formFrom = (t: TeacherResponse): FormState => ({
  name: t.name,
  specialization: t.specialization ?? "",
  phone: t.phone ?? "",
  status: t.status as TeacherStatus,
  username: t.username,
  email: t.email,
  password: "",
});

/**
 * U2: adding a teacher (/teachers/new) and editing one (/teachers/:id/edit) on a full page. A
 * teacher is a staff member with a login, so the page has two parts: the profile, and the login
 * account (username, email, password). Class and subject assignments are made on the teacher's page.
 */
export function TeacherFormPage() {
  const { id } = useParams();
  const isNew = id === undefined;
  const teacherId = Number(id);
  const navigate = useNavigate();

  const loaded = useAsyncData(
    () => (isNew ? Promise.resolve(null) : teacherService.getById(teacherId)),
    [isNew, teacherId],
    "Could not load this teacher.",
  );
  const record = useRecordForm<FormState>({
    sections: SECTIONS,
    initial: EMPTY_FORM,
    noun: "teacher",
  });
  const { reset } = record;

  const teacher = loaded.data ?? null;
  useEffect(() => {
    if (teacher) reset(formFrom(teacher));
  }, [teacher, reset]);

  if (loaded.error) {
    return (
      <PageLoadError
        title="This teacher could not be opened"
        message={loaded.error}
        backTo="/teachers"
        backLabel="Back to teachers"
      />
    );
  }
  if (loaded.isLoading || (teacher && record.form === EMPTY_FORM))
    return <PageLoading />;

  const form = record.form;
  const bind = fieldBinder(form, record.patch, record.fieldErrors, "teacher");

  const validate = () => {
    const e: Record<string, string> = {};
    if (!form.name.trim()) e.name = "Full name is required.";
    if (!form.username.trim()) e.username = "Username is required.";
    if (!form.email.trim()) e.email = "Email is required.";
    else if (!/^\S+@\S+\.\S+$/.test(form.email.trim()))
      e.email = "Enter a valid email address.";
    if (!teacher && !form.password)
      e.password = "A password is required for the new login.";
    else if (form.password && !meetsPasswordRules(form.password))
      e.password = "The password does not meet all the rules below.";
    return e;
  };

  const handleSave = () => {
    const common = {
      name: form.name.trim(),
      username: form.username.trim(),
      email: form.email.trim(),
      phone: form.phone.trim() || null,
      specialization: form.specialization.trim() || null,
      status: form.status,
    };
    void record.save({
      validate,
      submit: () =>
        teacher
          ? teacherService.update(teacher.id, {
              ...common,
              newPassword: form.password || null,
            })
          : teacherService.create({ ...common, password: form.password }),
      fallback: "Could not save this teacher.",
      // The API calls the edit-page password "newPassword"; it is the same box.
      remap: (fields) =>
        Object.fromEntries(
          Object.entries(fields).map(([k, m]) => [
            k === "newPassword" ? "password" : k,
            m,
          ]),
        ),
      onSaved: (saved) => ({
        form: formFrom(saved),
        message: teacher
          ? `${saved.name} was saved.`
          : `${saved.name} was added.`,
        goTo: `/teachers/${saved.id}`,
      }),
    });
  };

  const backTo = teacher ? `/teachers/${teacher.id}` : "/teachers";

  return (
    <>
      {record.guard}
      <FormPage
        {...record.pageProps}
        title={teacher ? `Edit ${teacher.name}` : "Add teacher"}
        subtitle={
          teacher
            ? teacher.username
            : "Classes and subjects are assigned on the teacher's page after saving."
        }
        back={{
          to: backTo,
          label: teacher ? "Back to teacher" : "Back to teachers",
        }}
        onSubmit={handleSave}
        saveLabel={teacher ? "Save changes" : "Save teacher"}
        onCancel={() => navigate(backTo)}
      >
        <FormSection id="profile" title="Profile">
          <TextField {...bind("name")} label="Full name" required />
          <TextField
            {...bind("specialization")}
            label="Specialization"
            placeholder="e.g. Mathematics"
          />
          <TextField {...bind("phone")} label="Phone" type="tel" />
          <SelectField
            {...bind("status")}
            label="Status"
            options={TEACHER_STATUSES}
            hint="Inactive and suspended teachers cannot sign in."
          />
        </FormSection>

        <FormSection
          id="login"
          title="Login account"
          description="The teacher signs in with this username or email."
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
          <TextField
            {...bind("password")}
            label={teacher ? "New password" : "Password"}
            type="password"
            required={!teacher}
            autoComplete="new-password"
            hint={
              teacher ? "Leave blank to keep the current password." : undefined
            }
          />
          <div className="field">
            <PasswordRules password={form.password} />
          </div>
        </FormSection>
      </FormPage>
    </>
  );
}
