import { useEffect } from "react";
import { useNavigate, useParams } from "react-router-dom";
import { classSectionService } from "../services/classSectionService";
import {
  CLASS_MEDIUMS,
  CLASS_STAGES,
  CLASS_STREAMS,
  type ClassSectionFields,
  type ClassSectionResponse,
} from "../types/classSection.types";
import { previewClassCode } from "../types/classSectionFormat";
import { FormPage } from "@/components/form/FormPage";
import { FormSection } from "@/components/form/FormSection";
import {
  CheckboxField,
  SelectField,
  TextField,
} from "@/components/form/Inputs";
import { PageLoadError, PageLoading } from "@/components/PageState";
import { useAsyncData } from "@/hooks/useAsyncData";
import { useRecordForm } from "@/hooks/useRecordForm";
import { fieldBinder, numberOrNull, type FormSectionDef } from "@/utils/form";

/** Numbers are kept as typed (strings) until saving, so a half-typed value is not lost. */
interface FormState {
  name: string;
  section: string;
  grade: string;
  stage: string;
  medium: string;
  stream: string;
  capacity: string;
  building: string;
  floor: string;
  room: string;
  isActive: boolean;
}

const SECTIONS: readonly FormSectionDef[] = [
  {
    id: "class",
    title: "Class",
    fields: ["name", "section", "grade", "isActive", "academicYearId"],
  },
  {
    id: "academic",
    title: "Academic",
    fields: ["stage", "medium", "stream", "capacity"],
  },
  { id: "location", title: "Location", fields: ["building", "floor", "room"] },
];

const EMPTY_FORM: FormState = {
  name: "",
  section: "",
  grade: "",
  stage: "Primary",
  medium: "English",
  stream: "General",
  capacity: "40",
  building: "",
  floor: "",
  room: "",
  isActive: true,
};

const formFrom = (c: ClassSectionResponse): FormState => ({
  name: c.name,
  section: c.section,
  grade: c.grade?.toString() ?? "",
  stage: c.stage,
  medium: c.medium,
  stream: c.stream,
  capacity: c.capacity?.toString() ?? "",
  building: c.building ?? "",
  floor: c.floor?.toString() ?? "",
  room: c.room ?? "",
  isActive: c.status === "Active",
});

const isWholeNumber = (value: string) => Number.isInteger(Number(value));

/** U2: adding a class (/classes-sections/new) and editing one (/classes-sections/:id/edit) on a full page. */
export function ClassSectionFormPage() {
  const { id } = useParams();
  const isNew = id === undefined;
  const classId = Number(id);
  const navigate = useNavigate();

  const loaded = useAsyncData(
    () =>
      isNew ? Promise.resolve(null) : classSectionService.getById(classId),
    [isNew, classId],
    "Could not load this class.",
  );
  const record = useRecordForm<FormState>({
    sections: SECTIONS,
    initial: EMPTY_FORM,
    noun: "class",
    onReload: () => void loaded.reload(),
  });
  const { reset } = record;

  const existing = loaded.data ?? null;
  useEffect(() => {
    if (existing) reset(formFrom(existing));
  }, [existing, reset]);

  if (loaded.error) {
    return (
      <PageLoadError
        title="This class could not be opened"
        message={loaded.error}
        backTo="/classes-sections"
        backLabel="Back to classes"
      />
    );
  }
  if (loaded.isLoading || (existing && record.form === EMPTY_FORM))
    return <PageLoading />;

  const form = record.form;
  const bind = fieldBinder(form, record.patch, record.fieldErrors, "class");
  const code =
    previewClassCode(form.name, form.section) || existing?.code || "";

  const validate = () => {
    const e: Record<string, string> = {};
    if (!form.name.trim()) e.name = "Class name is required.";
    if (!form.section.trim()) e.section = "Section is required.";
    if (
      !form.capacity ||
      !isWholeNumber(form.capacity) ||
      Number(form.capacity) < 1
    )
      e.capacity = "Capacity must be a whole number of at least 1.";
    if (form.grade && !isWholeNumber(form.grade))
      e.grade = "Grade must be a whole number.";
    if (form.floor && !isWholeNumber(form.floor))
      e.floor = "Floor must be a whole number.";
    return e;
  };

  const handleSave = () => {
    const request: ClassSectionFields = {
      name: form.name.trim(),
      section: form.section.trim().toUpperCase(),
      grade: numberOrNull(form.grade),
      stage: form.stage,
      medium: form.medium,
      stream: form.stream,
      capacity: Number(form.capacity),
      building: form.building.trim() || null,
      floor: numberOrNull(form.floor),
      room: form.room.trim() || null,
      isActive: form.isActive,
    };
    void record.save({
      validate,
      submit: () =>
        existing
          ? classSectionService.update(existing.id, {
              ...request,
              rowVersion: existing.rowVersion,
            })
          : classSectionService.create(request),
      fallback: "Could not save this class.",
      onSaved: (saved) => ({
        form: formFrom(saved),
        message: existing
          ? `${saved.displayName} was saved.`
          : `${saved.displayName} was added.`,
        goTo: `/classes-sections/${saved.id}`,
      }),
    });
  };

  const backTo = existing
    ? `/classes-sections/${existing.id}`
    : "/classes-sections";

  return (
    <>
      {record.guard}
      <FormPage
        {...record.pageProps}
        title={existing ? `Edit ${existing.displayName}` : "Add class"}
        subtitle={
          existing
            ? existing.academicYearName
            : "The class is added to the current academic year."
        }
        back={{
          to: backTo,
          label: existing ? "Back to class" : "Back to classes",
        }}
        onSubmit={handleSave}
        saveLabel={existing ? "Save changes" : "Save class"}
        onCancel={() => navigate(backTo)}
      >
        <FormSection id="class" title="Class">
          <TextField
            {...bind("name")}
            label="Class name"
            required
            placeholder="e.g. Class 5, LKG"
          />
          <TextField
            {...bind("section")}
            label="Section"
            required
            maxLength={5}
            placeholder="A"
            onChange={(v) => record.patch({ section: v.toUpperCase() })}
          />
          <TextField
            {...bind("grade")}
            label="Grade"
            type="number"
            min={1}
            hint="A number; leave blank for LKG / UKG."
          />
          <TextField
            id="class-code"
            label="Code"
            value={code}
            onChange={() => {}}
            disabled
            hint="Made from the name and section."
          />
          <TextField
            id="class-academicYear"
            label="Academic year"
            value={
              existing ? existing.academicYearName : "Current academic year"
            }
            onChange={() => {}}
            disabled
          />
          <CheckboxField
            id="class-isActive"
            label="Active (offered for admissions and enrolment)"
            checked={form.isActive}
            onChange={(v) => record.patch({ isActive: v })}
          />
        </FormSection>

        <FormSection id="academic" title="Academic">
          <SelectField
            {...bind("stage")}
            label="Stage"
            options={CLASS_STAGES}
          />
          <SelectField
            {...bind("medium")}
            label="Medium"
            options={CLASS_MEDIUMS}
          />
          <SelectField
            {...bind("stream")}
            label="Stream"
            options={CLASS_STREAMS}
          />
          <TextField
            {...bind("capacity")}
            label="Capacity"
            type="number"
            min={1}
            required
          />
        </FormSection>

        <FormSection id="location" title="Location">
          <TextField {...bind("building")} label="Building" />
          <TextField {...bind("floor")} label="Floor" type="number" />
          <TextField {...bind("room")} label="Room" />
        </FormSection>
      </FormPage>
    </>
  );
}
