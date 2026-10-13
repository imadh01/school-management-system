import { useState } from "react";
import { CircleCheck } from "lucide-react";
import type {
  AdmissionResponse,
  ConfirmAdmissionRequest,
} from "../types/admission.types";
import { Dialog } from "@/components/Dialog";
import { DialogFooter } from "@/components/DialogFooter";
import {
  SelectField,
  TextAreaField,
  TextField,
} from "@/components/form/Inputs";
import { useDialogSubmit } from "@/hooks/useDialogSubmit";
import { BLOOD_GROUPS, CATEGORIES, RELIGIONS } from "@/types/lookups";
import { blankToNull, fieldBinder, numberOrNull } from "@/utils/form";

interface Props {
  admission: AdmissionResponse;
  onClose: () => void;
  onSubmit: (id: number, data: ConfirmAdmissionRequest) => Promise<void>;
}

/** Registered → Admitted: the fee is recorded and a few profile details are added. */
export function ConfirmAdmissionModal({ admission, onClose, onSubmit }: Props) {
  // Remarks entered at registration are kept and shown here, because confirming replaces the
  // remarks with whatever is in this box.
  const [form, setForm] = useState<ConfirmAdmissionRequest>({
    admissionFee: null,
    admissionFeeReference: null,
    remarks: admission.remarks,
    bloodGroup: admission.bloodGroup,
    religion: admission.religion,
    category: admission.category,
    medicalNotes: admission.medicalNotes,
  });
  const patch = (changes: Partial<ConfirmAdmissionRequest>) =>
    setForm((f) => ({ ...f, ...changes }));
  const bind = fieldBinder(form, patch, {}, "confirm");
  const { isSubmitting, errors, submit } = useDialogSubmit(onClose);

  return (
    <Dialog
      title="Confirm admission"
      icon={CircleCheck}
      subtitle={`${admission.firstName} ${admission.lastName} · ${admission.regNo}`}
      errors={errors}
      onClose={onClose}
      closable={!isSubmitting}
      footer={
        <DialogFooter
          onCancel={onClose}
          onSubmit={() =>
            submit(
              () => onSubmit(admission.id, blankToNull(form)),
              "Could not confirm this admission. It may have already moved past Registered.",
            )
          }
          label="Confirm admission"
          busyLabel="Confirming…"
          isSubmitting={isSubmitting}
        />
      }
    >
      <h3 className="form-subhead">Admission fee</h3>
      <div className="form-grid" style={{ marginBottom: 18 }}>
        <TextField
          {...bind("admissionFee")}
          label="Fee amount"
          type="number"
          min={0}
          onChange={(v) => patch({ admissionFee: numberOrNull(v) })}
        />
        <TextField
          {...bind("admissionFeeReference")}
          label="Payment reference"
        />
      </div>

      <h3 className="form-subhead">Profile details</h3>
      <div className="form-grid">
        <SelectField
          {...bind("bloodGroup")}
          label="Blood group"
          options={BLOOD_GROUPS}
          emptyLabel="Unknown"
        />
        <SelectField
          {...bind("religion")}
          label="Religion"
          options={RELIGIONS}
          emptyLabel="Not specified"
        />
        <SelectField
          {...bind("category")}
          label="Category"
          options={CATEGORIES}
          emptyLabel="General (default)"
        />
        <TextField {...bind("medicalNotes")} label="Medical notes" />
        <TextAreaField
          {...bind("remarks")}
          label="Remarks"
          rows={2}
          span="full"
        />
      </div>
    </Dialog>
  );
}
