import { useEffect, useState } from "react";
import { CircleCheck } from "lucide-react";
import { admissionService } from "../services/admissionService";
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
  const [fieldErrors, setFieldErrors] = useState<Record<string, string>>({});
  const bind = fieldBinder(form, patch, fieldErrors, "confirm");
  const { isSubmitting, errors, submit } = useDialogSubmit(onClose);

  // Pre-fill the fee set for this class in Masters. It stays editable, and a fee the clerk has
  // already typed is never overwritten. A failed lookup just leaves the box empty.
  useEffect(() => {
    let cancelled = false;
    admissionService
      .getSuggestedFee(admission.id)
      .then((s) => {
        if (!cancelled && s.amount != null)
          setForm((f) => (f.admissionFee == null ? { ...f, admissionFee: s.amount } : f));
      })
      .catch(() => undefined);
    return () => {
      cancelled = true;
    };
  }, [admission.id]);

  // Payment is taken when the admission is confirmed, so both payment fields are required.
  const validate = () => {
    const e: Record<string, string> = {};
    if (form.admissionFee == null) e.admissionFee = "Fee amount is required.";
    if (!form.admissionFeeReference?.trim())
      e.admissionFeeReference = "Payment reference is required.";
    setFieldErrors(e);
    return Object.keys(e).length === 0;
  };

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
            validate() &&
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
          required
          type="number"
          min={0}
          onChange={(v) => patch({ admissionFee: numberOrNull(v) })}
        />
        <TextField
          {...bind("admissionFeeReference")}
          label="Payment reference"
          required
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
