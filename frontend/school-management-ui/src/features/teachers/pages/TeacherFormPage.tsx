import { useEffect } from "react";
import { useNavigate, useParams } from "react-router-dom";
import { teacherService } from "../services/teacherService";
import type { TeacherResponse } from "../types/teacher.types";
import { FormPage } from "@/components/form/FormPage";
import { FormSection } from "@/components/form/FormSection";
import { TextField } from "@/components/form/Inputs";
import { PageLoadError, PageLoading } from "@/components/PageState";
import { useAsyncData } from "@/hooks/useAsyncData";
import { useRecordForm } from "@/hooks/useRecordForm";
import { fieldBinder, type FormSectionDef } from "@/utils/form";

interface FormState {
  name: string;
  specialization: string;
  phone: string;
}

const SECTIONS: readonly FormSectionDef[] = [
  {
    id: "profile",
    title: "Teaching profile",
    fields: ["name", "specialization", "phone"],
  },
];

const EMPTY_FORM: FormState = { name: "", specialization: "", phone: "" };

const formFrom = (t: TeacherResponse): FormState => ({
  name: t.name,
  specialization: t.specialization ?? "",
  phone: t.phone ?? "",
});

/**
 * E3a: edit a teacher's teaching profile (/teachers/:id/edit): name, phone and specialization.
 * The login (username, e-mail, status, password, role) belongs to the Users page, and so does
 * adding a teacher: /teachers/new sends you to the Users form with the role Teacher.
 */
export function TeacherFormPage() {
  const teacherId = Number(useParams().id);
  const navigate = useNavigate();

  const loaded = useAsyncData(
    () => teacherService.getById(teacherId),
    [teacherId],
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
  if (!teacher || record.form === EMPTY_FORM) return <PageLoading />;

  const form = record.form;
  const bind = fieldBinder(form, record.patch, record.fieldErrors, "teacher");
  const backTo = `/teachers/${teacher.id}`;

  const validate = () => {
    const e: Record<string, string> = {};
    if (!form.name.trim()) e.name = "Full name is required.";
    return e;
  };

  const handleSave = () =>
    void record.save({
      validate,
      submit: () =>
        teacherService.update(teacher.id, {
          name: form.name.trim(),
          phone: form.phone.trim() || null,
          specialization: form.specialization.trim() || null,
        }),
      fallback: "Could not save this teacher.",
      onSaved: (saved) => ({
        form: formFrom(saved),
        message: `${saved.name} was saved.`,
        goTo: backTo,
      }),
    });

  return (
    <>
      {record.guard}
      <FormPage
        {...record.pageProps}
        title={`Edit ${teacher.name}`}
        subtitle={`${teacher.username} · the login is managed on the Users page`}
        back={{ to: backTo, label: "Back to teacher" }}
        onSubmit={handleSave}
        saveLabel="Save changes"
        onCancel={() => navigate(backTo)}
      >
        <FormSection id="profile" title="Teaching profile">
          <TextField {...bind("name")} label="Full name" required />
          <TextField
            {...bind("specialization")}
            label="Specialization"
            placeholder="e.g. Mathematics"
          />
          <TextField {...bind("phone")} label="Phone" type="tel" />
        </FormSection>
      </FormPage>
    </>
  );
}
