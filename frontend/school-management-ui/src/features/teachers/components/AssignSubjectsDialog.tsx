import { useState } from "react";
import { BookPlus } from "lucide-react";
import { teacherService } from "../services/teacherService";
import type {
  TeacherAssignment,
  TeacherResponse,
} from "../types/teacher.types";
import { classSectionService } from "@/features/class-sections/services/classSectionService";
import { subjectService } from "@/features/subjects/services/subjectService";
import { Dialog } from "@/components/Dialog";
import { DialogFooter } from "@/components/DialogFooter";
import {
  CheckboxField,
  SelectField,
  ToggleGroup,
} from "@/components/form/Inputs";
import { useAsyncData } from "@/hooks/useAsyncData";
import { useDialogSubmit } from "@/hooks/useDialogSubmit";

/**
 * Gives a teacher more subjects in one class, and optionally makes them its class teacher.
 * Subjects already taught by someone else are shown but cannot be picked (one teacher per subject).
 */
export function AssignSubjectsDialog({
  teacher,
  assignments,
  onClose,
  onAssigned,
}: {
  teacher: TeacherResponse;
  assignments: TeacherAssignment[];
  onClose: () => void;
  /** Receives the teacher's refreshed assignments. */
  onAssigned: (assignments: TeacherAssignment[], subjectCount: number) => void;
}) {
  const reference = useAsyncData(
    async () => {
      const [classSections, subjects] = await Promise.all([
        classSectionService.getAll(),
        subjectService.getAll(),
      ]);
      return { classSections, subjects };
    },
    [],
    "Could not load the classes and subjects.",
  );
  const [classId, setClassId] = useState<number | null>(null);
  const [selected, setSelected] = useState<number[]>([]);
  const [makeClassTeacher, setMakeClassTeacher] = useState(false);
  const { isSubmitting, errors, submit } = useDialogSubmit(onClose);

  const classSections = reference.data?.classSections ?? [];
  const section = classSections.find((c) => c.id === classId);
  const iAmClassTeacher = assignments.some(
    (a) => a.classSectionId === classId && a.isClassTeacher,
  );
  const otherClassTeacher =
    section?.classTeacherId != null && section.classTeacherId !== teacher.id
      ? (section.classTeacherName ?? "another teacher")
      : null;

  // Active subjects of the chosen class that this teacher does not teach yet.
  const pickable = (reference.data?.subjects ?? []).filter(
    (s) =>
      s.classSectionId === classId &&
      s.status === "Active" &&
      s.teacherId !== teacher.id,
  );
  const labelOf = (s: (typeof pickable)[number]) =>
    s.teacherId !== null
      ? `${s.name} (${s.code}) · ${s.teacherName ?? "taken"}`
      : `${s.name} (${s.code})`;
  const byLabel = new Map(pickable.map((s) => [labelOf(s), s]));

  const assign = () =>
    submit(async () => {
      if (classId === null) return;
      const result = await teacherService.assignSubjects(teacher.id, {
        classSectionId: classId,
        subjectIds: selected,
        makeClassTeacher:
          makeClassTeacher && !iAmClassTeacher && !otherClassTeacher,
      });
      onAssigned(result, selected.length);
    }, "Could not assign these subjects.");

  return (
    <Dialog
      title="Assign subjects"
      icon={BookPlus}
      subtitle={teacher.name}
      wide
      errors={reference.error ? [reference.error, ...errors] : errors}
      onClose={onClose}
      closable={!isSubmitting}
      footer={
        <DialogFooter
          onCancel={onClose}
          onSubmit={() => void assign()}
          label={
            selected.length > 0
              ? `Assign ${selected.length} subject${selected.length > 1 ? "s" : ""}`
              : "Assign"
          }
          busyLabel="Assigning…"
          isSubmitting={isSubmitting}
          disabled={selected.length === 0}
        />
      }
    >
      <div className="form-grid" style={{ marginBottom: 14 }}>
        <SelectField
          id="assign-class"
          label="Class"
          emptyLabel={reference.isLoading ? "Loading…" : "Select a class…"}
          options={classSections
            .filter((c) => c.status === "Active")
            .map((c) => ({ value: c.id, label: c.displayName }))}
          value={classId ?? ""}
          onChange={(v) => {
            setClassId(v ? Number(v) : null);
            setSelected([]);
            setMakeClassTeacher(false);
          }}
        />
      </div>

      {classId !== null && (
        <>
          {pickable.length === 0 ? (
            <p className="field-note">
              No more subjects to assign in this class.
            </p>
          ) : (
            <ToggleGroup
              legend={`Subjects in this class (${pickable.length})`}
              options={pickable.map(labelOf)}
              isSelected={(label) =>
                selected.includes(byLabel.get(label)?.id ?? -1)
              }
              isDisabled={(label) => byLabel.get(label)?.teacherId != null}
              onToggle={(label) => {
                const id = byLabel.get(label)?.id;
                if (id === undefined) return;
                setSelected((cur) =>
                  cur.includes(id) ? cur.filter((x) => x !== id) : [...cur, id],
                );
              }}
            />
          )}
          <p className="field-note">
            Subjects already taught by another teacher cannot be picked.
          </p>
          <CheckboxField
            id="assign-class-teacher"
            label="Also make them the class teacher of this class"
            checked={makeClassTeacher || iAmClassTeacher}
            disabled={iAmClassTeacher || otherClassTeacher !== null}
            onChange={setMakeClassTeacher}
            hint={
              iAmClassTeacher
                ? "They are already the class teacher here."
                : otherClassTeacher
                  ? `${otherClassTeacher} is the class teacher. Unset them first.`
                  : "One teacher per class can be the class teacher."
            }
          />
        </>
      )}
    </Dialog>
  );
}
