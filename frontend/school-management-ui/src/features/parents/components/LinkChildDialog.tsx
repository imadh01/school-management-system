import { useState } from "react";
import { Link2 } from "lucide-react";
import { parentService } from "../services/parentService";
import type {
  LinkedStudentResponse,
  ParentResponse,
  RelationType,
} from "../types/parent.types";
import { studentService } from "@/features/students/services/studentService";
import type { StudentSummaryResponse } from "@/features/students/types/student.types";
import { Dialog } from "@/components/Dialog";
import { DialogFooter } from "@/components/DialogFooter";
import { CheckboxField, SelectField } from "@/components/form/Inputs";
import { SearchPicker } from "@/components/form/SearchPicker";
import { useAsyncData } from "@/hooks/useAsyncData";
import { useDialogSubmit } from "@/hooks/useDialogSubmit";
import { GUARDIAN_RELATIONS } from "@/types/lookups";

const studentLabel = (s: StudentSummaryResponse) =>
  `${s.firstName} ${s.lastName} · ${s.admNo} · ${s.classSectionName}`;

/** Links one more student to a parent: search the student, pick the relation, optionally primary contact. */
export function LinkChildDialog({
  parent,
  linked,
  onClose,
  onLinked,
}: {
  parent: ParentResponse;
  /** Already linked: left out of the search. */
  linked: LinkedStudentResponse[];
  onClose: () => void;
  onLinked: (child: LinkedStudentResponse) => Promise<void>;
}) {
  const students = useAsyncData(
    () => studentService.getAll(),
    [],
    "Could not load students.",
  );
  const [student, setStudent] = useState<StudentSummaryResponse | null>(null);
  const [relation, setRelation] = useState<RelationType>("Father");
  const [primary, setPrimary] = useState(false);
  const { isSubmitting, errors, submit } = useDialogSubmit(onClose);

  const linkedIds = new Set(linked.map((c) => c.studentId));
  const available = students.data?.filter((s) => !linkedIds.has(s.id)) ?? null;

  const link = (picked: StudentSummaryResponse) =>
    submit(async () => {
      const child = await parentService.linkStudent(parent.id, {
        studentId: picked.id,
        relationType: relation,
        isPrimaryContact: primary,
      });
      await onLinked(child);
    }, "Could not link this student.");

  return (
    <Dialog
      title="Link a child"
      icon={Link2}
      subtitle={`${parent.name} · ${parent.mobile}`}
      errors={students.error ? [students.error, ...errors] : errors}
      onClose={onClose}
      closable={!isSubmitting}
      footer={
        <DialogFooter
          onCancel={onClose}
          onSubmit={() => student && void link(student)}
          label="Link student"
          busyLabel="Linking…"
          isSubmitting={isSubmitting}
          disabled={!student}
        />
      }
    >
      {student ? (
        <div className="choice-option" style={{ marginBottom: 14 }}>
          <span>
            <b>{studentLabel(student)}</b>
          </span>
          <button
            type="button"
            className="btn btn--secondary btn--sm"
            onClick={() => setStudent(null)}
          >
            Change
          </button>
        </div>
      ) : (
        <div style={{ marginBottom: 14 }}>
          <SearchPicker
            id="link-child-search"
            label="Find the student by name or admission number"
            items={available}
            itemKey={(s) => s.id}
            itemLabel={studentLabel}
            onPick={setStudent}
          />
        </div>
      )}
      <div className="form-grid">
        <SelectField
          id="link-child-relation"
          label="Relation"
          options={GUARDIAN_RELATIONS}
          value={relation}
          onChange={(v) => setRelation(v as RelationType)}
        />
        <CheckboxField
          id="link-child-primary"
          label="Primary contact for this student"
          checked={primary}
          onChange={setPrimary}
        />
      </div>
    </Dialog>
  );
}
