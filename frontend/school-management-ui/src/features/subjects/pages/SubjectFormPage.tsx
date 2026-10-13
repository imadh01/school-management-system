import { useEffect } from "react";
import { useNavigate, useParams } from "react-router-dom";
import { subjectService } from "../services/subjectService";
import {
  SUBJECT_TYPES,
  type SubjectRequest,
  type SubjectResponse,
  type SubjectType,
} from "../types/subject.types";
import { classSectionService } from "@/features/class-sections/services/classSectionService";
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

/** Marks are kept as typed (strings) until saving. */
interface FormState {
  name: string;
  code: string;
  classSectionId: string;
  type: SubjectType;
  maxMarks: string;
  passMarks: string;
  theoryMax: string;
  theoryPass: string;
  practicalMax: string;
  practicalPass: string;
  isActive: boolean;
}

const SECTIONS: readonly FormSectionDef[] = [
  {
    id: "subject",
    title: "Subject",
    fields: ["name", "code", "classSectionId", "isActive"],
  },
  {
    id: "marks",
    title: "Marks",
    fields: [
      "type",
      "maxMarks",
      "passMarks",
      "theoryMax",
      "theoryPass",
      "practicalMax",
      "practicalPass",
    ],
  },
];

const EMPTY_FORM: FormState = {
  name: "",
  code: "",
  classSectionId: "",
  type: "Theory",
  maxMarks: "100",
  passMarks: "33",
  theoryMax: "80",
  theoryPass: "27",
  practicalMax: "20",
  practicalPass: "6",
  isActive: true,
};

// Blank marks of a stored subject fall back to the usual defaults, so switching type starts sensibly.
const formFrom = (s: SubjectResponse): FormState => ({
  name: s.name,
  code: s.code,
  classSectionId: String(s.classSectionId),
  type: s.type as SubjectType,
  maxMarks: s.maxMarks?.toString() ?? EMPTY_FORM.maxMarks,
  passMarks: s.passMarks?.toString() ?? EMPTY_FORM.passMarks,
  theoryMax: s.theoryMax?.toString() ?? EMPTY_FORM.theoryMax,
  theoryPass: s.theoryPass?.toString() ?? EMPTY_FORM.theoryPass,
  practicalMax: s.practicalMax?.toString() ?? EMPTY_FORM.practicalMax,
  practicalPass: s.practicalPass?.toString() ?? EMPTY_FORM.practicalPass,
  isActive: s.status === "Active",
});

/** Checks one max/pass pair; adds the problems under the two fields. */
function checkMarks(
  e: Record<string, string>,
  maxKey: string,
  passKey: string,
  max: string,
  pass: string,
) {
  const m = numberOrNull(max);
  const p = numberOrNull(pass);
  if (m === null || Number.isNaN(m) || m < 0)
    e[maxKey] = "Enter a number of 0 or more.";
  if (p === null || Number.isNaN(p) || p < 0)
    e[passKey] = "Enter a number of 0 or more.";
  else if (m !== null && !Number.isNaN(m) && p > m)
    e[passKey] = "Pass marks cannot be more than max marks.";
}

/**
 * U2: adding a subject (/subjects/new) and editing one (/subjects/:id/edit) on a full page. A
 * subject belongs to one class; "Both" subjects have separate theory and practical marks.
 */
export function SubjectFormPage() {
  const { id } = useParams();
  const isNew = id === undefined;
  const subjectId = Number(id);
  const navigate = useNavigate();

  const loaded = useAsyncData(
    async () => {
      const [classSections, subject] = await Promise.all([
        classSectionService.getAll(),
        isNew ? Promise.resolve(null) : subjectService.getById(subjectId),
      ]);
      return { classSections, subject };
    },
    [isNew, subjectId],
    "Could not load this subject.",
  );
  const record = useRecordForm<FormState>({
    sections: SECTIONS,
    initial: EMPTY_FORM,
    noun: "subject",
  });
  const { reset } = record;

  const subject = loaded.data?.subject ?? null;
  useEffect(() => {
    if (subject) reset(formFrom(subject));
  }, [subject, reset]);

  if (loaded.error) {
    return (
      <PageLoadError
        title="This subject could not be opened"
        message={loaded.error}
        backTo="/subjects"
        backLabel="Back to subjects"
      />
    );
  }
  if (!loaded.data || (subject && record.form === EMPTY_FORM))
    return <PageLoading />;

  const form = record.form;
  const bind = fieldBinder(form, record.patch, record.fieldErrors, "subject");
  const isBoth = form.type === "Both";
  // Active classes, plus the subject's own class when editing (even if inactive).
  const classOptions = loaded.data.classSections
    .filter(
      (c) => c.status === "Active" || String(c.id) === form.classSectionId,
    )
    .map((c) => ({ value: c.id, label: c.displayName }));

  const validate = () => {
    const e: Record<string, string> = {};
    if (!form.name.trim()) e.name = "Subject name is required.";
    if (!form.classSectionId) e.classSectionId = "Choose a class.";
    if (isBoth) {
      checkMarks(e, "theoryMax", "theoryPass", form.theoryMax, form.theoryPass);
      checkMarks(
        e,
        "practicalMax",
        "practicalPass",
        form.practicalMax,
        form.practicalPass,
      );
    } else {
      checkMarks(e, "maxMarks", "passMarks", form.maxMarks, form.passMarks);
    }
    return e;
  };

  const handleSave = () => {
    const request: SubjectRequest = {
      name: form.name.trim(),
      code: form.code.trim() || null,
      classSectionId: Number(form.classSectionId),
      type: form.type,
      maxMarks: isBoth ? null : numberOrNull(form.maxMarks),
      passMarks: isBoth ? null : numberOrNull(form.passMarks),
      theoryMax: isBoth ? numberOrNull(form.theoryMax) : null,
      theoryPass: isBoth ? numberOrNull(form.theoryPass) : null,
      practicalMax: isBoth ? numberOrNull(form.practicalMax) : null,
      practicalPass: isBoth ? numberOrNull(form.practicalPass) : null,
      isActive: form.isActive,
    };
    void record.save({
      validate,
      submit: () =>
        subject
          ? subjectService.update(subject.id, request)
          : subjectService.create(request),
      fallback: "Could not save this subject.",
      onSaved: (saved) => ({
        form: formFrom(saved),
        message: subject
          ? `${saved.name} was saved.`
          : `${saved.name} (${saved.code}) was added.`,
        goTo: "/subjects",
      }),
    });
  };

  return (
    <>
      {record.guard}
      <FormPage
        {...record.pageProps}
        title={subject ? `Edit ${subject.name}` : "Add subject"}
        subtitle={
          subject
            ? `${subject.code} · ${subject.classSectionName}`
            : "Teachers are assigned from the teacher's page."
        }
        back={{ to: "/subjects", label: "Back to subjects" }}
        onSubmit={handleSave}
        saveLabel={subject ? "Save changes" : "Save subject"}
        onCancel={() => navigate("/subjects")}
      >
        <FormSection id="subject" title="Subject">
          <TextField
            {...bind("name")}
            label="Subject name"
            required
            placeholder="e.g. Mathematics"
          />
          <TextField
            {...bind("code")}
            label="Code"
            hint="Leave blank to make one from the name."
            maxLength={20}
          />
          <SelectField
            {...bind("classSectionId")}
            label="Class"
            required
            emptyLabel="Select a class…"
            options={classOptions}
          />
          <CheckboxField
            id="subject-isActive"
            label="Active"
            checked={form.isActive}
            onChange={(v) => record.patch({ isActive: v })}
          />
        </FormSection>

        <FormSection id="marks" title="Marks">
          <SelectField
            {...bind("type")}
            label="Type"
            options={SUBJECT_TYPES}
            hint="“Both” has separate theory and practical marks."
          />
          {isBoth ? (
            <>
              <TextField
                {...bind("theoryMax")}
                label="Theory max marks"
                type="number"
                min={0}
                required
              />
              <TextField
                {...bind("theoryPass")}
                label="Theory pass marks"
                type="number"
                min={0}
                required
              />
              <TextField
                {...bind("practicalMax")}
                label="Practical max marks"
                type="number"
                min={0}
                required
              />
              <TextField
                {...bind("practicalPass")}
                label="Practical pass marks"
                type="number"
                min={0}
                required
              />
            </>
          ) : (
            <>
              <TextField
                {...bind("maxMarks")}
                label="Max marks"
                type="number"
                min={0}
                required
              />
              <TextField
                {...bind("passMarks")}
                label="Pass marks"
                type="number"
                min={0}
                required
              />
            </>
          )}
        </FormSection>
      </FormPage>
    </>
  );
}
