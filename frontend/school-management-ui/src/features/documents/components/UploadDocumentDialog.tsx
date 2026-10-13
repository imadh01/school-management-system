import { useId, useState, type DragEvent } from "react";
import { CircleAlert, FileUp, Upload } from "lucide-react";
import { Dialog } from "@/components/Dialog";
import { DialogFooter } from "@/components/DialogFooter";
import { SelectField, TextField } from "@/components/form/Inputs";
import { getSaveErrors } from "@/utils/apiError";
import {
  NOT_ACCEPTED_MESSAGE,
  describeSize,
  detectFileType,
  tooLargeMessage,
} from "@/utils/file";
import { documentService } from "../services/documentService";
import {
  fileKind,
  type DocumentOptionsResponse,
  type DocumentOwner,
  type DocumentResponse,
} from "../types/document.types";

type FieldKey = "documentTypeId" | "title" | "expiryDate" | "file";

interface ChosenFile {
  file: File;
  /** From the content, or null when it is not a PDF/JPG/PNG. */
  kind: string | null;
}

/**
 * G1b: upload one document for a student or an application. The file is checked here first
 * (size against the server's limit, real type from its first bytes) so the user hears at once;
 * the API checks everything again and its answer is shown under the same fields.
 */
export function UploadDocumentDialog({
  owner,
  ownerLabel,
  options,
  onClose,
  onUploaded,
}: {
  owner: DocumentOwner;
  /** "Aisha Khan (A1024)" for the subtitle. */
  ownerLabel: string;
  options: DocumentOptionsResponse;
  onClose: () => void;
  onUploaded: (document: DocumentResponse) => void;
}) {
  const inputId = useId();
  const [typeId, setTypeId] = useState("");
  const [title, setTitle] = useState("");
  const [expiryDate, setExpiryDate] = useState("");
  const [chosen, setChosen] = useState<ChosenFile | null>(null);
  const [dragging, setDragging] = useState(false);
  const [errors, setErrors] = useState<Partial<Record<FieldKey, string>>>({});
  const [formErrors, setFormErrors] = useState<string[]>([]);
  const [isSubmitting, setIsSubmitting] = useState(false);

  const type = options.types.find((t) => String(t.id) === typeId);
  const limit = options.maxFileSizeBytes;

  const setError = (key: FieldKey, message: string | undefined) =>
    setErrors((current) => ({ ...current, [key]: message }));

  const choose = async (file: File | undefined) => {
    if (!file) return;
    const kind = await detectFileType(file);
    setChosen({ file, kind: kind && fileKind(kind) });
    setError(
      "file",
      file.size > limit
        ? tooLargeMessage(file.size, limit)
        : file.size === 0
          ? "The file is empty."
          : kind === null
            ? NOT_ACCEPTED_MESSAGE
            : undefined,
    );
  };

  const onDrop = (e: DragEvent<HTMLLabelElement>) => {
    e.preventDefault();
    setDragging(false);
    if (!isSubmitting) void choose(e.dataTransfer.files[0]);
  };

  const submit = async () => {
    const found: Partial<Record<FieldKey, string>> = { file: errors.file };
    if (!type) found.documentTypeId = "Choose a document type.";
    if (type?.hasExpiry && !expiryDate)
      found.expiryDate = `${type.name} needs an expiry date.`;
    if (!chosen) found.file = "Choose a file to upload.";
    setErrors(found);
    setFormErrors([]);
    if (Object.values(found).some(Boolean) || !chosen || !type) return;

    setIsSubmitting(true);
    try {
      const document = await documentService.upload(owner, {
        documentTypeId: type.id,
        title,
        expiryDate: type.hasExpiry ? expiryDate : "",
        file: chosen.file,
      });
      onUploaded(document);
    } catch (err) {
      const saveErrors = getSaveErrors(err, "Could not upload the document.");
      setErrors(saveErrors.fields as Partial<Record<FieldKey, string>>);
      setFormErrors(saveErrors.form);
      setIsSubmitting(false);
    }
  };

  return (
    <Dialog
      title="Upload document"
      icon={Upload}
      subtitle={ownerLabel}
      errors={formErrors}
      onClose={onClose}
      closable={!isSubmitting}
      footer={
        <DialogFooter
          onCancel={onClose}
          onSubmit={submit}
          label="Upload"
          busyLabel="Uploading…"
          isSubmitting={isSubmitting}
        />
      }
    >
      <div className="form-grid">
        <SelectField
          id="document-type"
          label="Document type"
          required
          span="full"
          emptyLabel="Choose…"
          options={options.types.map((t) => ({ value: t.id, label: t.name }))}
          value={typeId}
          onChange={(v) => {
            setTypeId(v);
            setError("documentTypeId", undefined);
          }}
          error={errors.documentTypeId}
          hint={
            type?.isSensitive
              ? "Identity / medical document: only staff allowed to see sensitive details can open it."
              : undefined
          }
        />
        {type?.hasExpiry && (
          <TextField
            id="document-expiry"
            type="date"
            label="Expiry date"
            required
            value={expiryDate}
            onChange={(v) => {
              setExpiryDate(v);
              setError("expiryDate", undefined);
            }}
            error={errors.expiryDate}
          />
        )}
        <TextField
          id="document-title"
          label="Label"
          span={type?.hasExpiry ? 1 : "full"}
          maxLength={120}
          placeholder="Optional, e.g. Passport (old)"
          value={title}
          onChange={setTitle}
          error={errors.title}
        />

        <div
          className={`field field--full${errors.file ? " field--invalid" : ""}`}
        >
          <span className="field__label" id={`${inputId}-label`}>
            File
            <span className="required" aria-hidden="true">
              *
            </span>
          </span>
          <label
            htmlFor={inputId}
            className={`dropzone dropzone--input${dragging ? " dropzone--active" : ""}`}
            onDragOver={(e) => {
              e.preventDefault();
              setDragging(true);
            }}
            onDragLeave={() => setDragging(false)}
            onDrop={onDrop}
          >
            <input
              id={inputId}
              type="file"
              className="visually-hidden"
              accept=".pdf,.jpg,.jpeg,.png,application/pdf,image/jpeg,image/png"
              aria-labelledby={`${inputId}-label`}
              aria-describedby={`${inputId}-help${errors.file ? ` ${inputId}-error` : ""}`}
              aria-invalid={errors.file ? true : undefined}
              aria-required
              disabled={isSubmitting}
              onChange={(e) => void choose(e.target.files?.[0])}
            />
            <FileUp size={28} aria-hidden="true" />
            {chosen ? (
              <>
                <strong className="dropzone__file">{chosen.file.name}</strong>
                <span>
                  {describeSize(chosen.file.size)}
                  {chosen.kind ? ` · ${chosen.kind}` : ""} · choose another
                </span>
              </>
            ) : (
              <>
                <strong>Choose a file or drop it here</strong>
                <span id={`${inputId}-help`}>
                  PDF, JPG or PNG, up to {describeSize(limit)}.
                </span>
              </>
            )}
          </label>
          {chosen && (
            <span id={`${inputId}-help`} className="visually-hidden">
              PDF, JPG or PNG, up to {describeSize(limit)}.
            </span>
          )}
          {errors.file && (
            <p className="field__error" id={`${inputId}-error`}>
              <CircleAlert size={14} aria-hidden="true" />
              {errors.file}
            </p>
          )}
        </div>
      </div>
    </Dialog>
  );
}
