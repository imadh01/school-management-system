import { useId, useState, type DragEvent } from "react";
import { CircleAlert, FileUp, Trash } from "lucide-react";
import { SelectField, TextField } from "@/components/form/Inputs";
import { describeSize, shortSize } from "@/utils/file";
import type { DocumentOptionsResponse } from "../types/document.types";
import type { PendingDocument } from "../types/pendingDocument";
import { MAX_PENDING_DOCUMENTS, toPendingDocument } from "../utils/pendingDocument";

/**
 * G1c: the Documents section of the New registration form. Pick files, say what each one is, and
 * they are uploaded after the application is saved. `errors` are keyed "documents.<row>.<field>".
 */
export function PendingDocumentsEditor({
  items,
  onChange,
  options,
  disabled,
  errors,
}: {
  items: PendingDocument[];
  onChange: (items: PendingDocument[]) => void;
  options: DocumentOptionsResponse;
  disabled?: boolean;
  errors: Record<string, string>;
}) {
  const inputId = useId();
  const [dragging, setDragging] = useState(false);
  // The type is chosen BEFORE the files, like in the Documents module: every file added next gets it
  // (still changeable per file below).
  const [pickType, setPickType] = useState("");
  const pickedType = options.types.find((t) => String(t.id) === pickType);
  const room = MAX_PENDING_DOCUMENTS - items.length;

  const add = async (files: FileList | File[] | null) => {
    if (!files || files.length === 0 || room <= 0) return;
    const picked = await Promise.all(
      Array.from(files)
        .slice(0, room)
        .map((f) => toPendingDocument(f, options.maxFileSizeBytes)),
    );
    onChange([...items, ...picked.map((p) => ({ ...p, typeId: pickType }))]);
  };

  const update = (key: string, changes: Partial<PendingDocument>) =>
    onChange(items.map((d) => (d.key === key ? { ...d, ...changes } : d)));

  const onDrop = (e: DragEvent<HTMLLabelElement>) => {
    e.preventDefault();
    setDragging(false);
    if (!disabled && pickType) void add(e.dataTransfer.files);
  };

  return (
    <div>
      <div className="form-grid" style={{ marginBottom: 12 }}>
        <SelectField
          id={`${inputId}-type`}
          label="What are you uploading?"
          emptyLabel="Choose a document type…"
          options={options.types.map((t) => ({ value: t.id, label: t.name }))}
          value={pickType}
          onChange={setPickType}
          hint={
            pickedType?.isSensitive
              ? "Identity / medical: only staff allowed to see sensitive details can open it."
              : "Choose the type first, then add the file(s). You can change a file's type below."
          }
        />
      </div>
      <label
        htmlFor={inputId}
        className={`dropzone dropzone--input${dragging ? " dropzone--active" : ""}${!pickType ? " dropzone--disabled" : ""}`}
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
          multiple
          className="visually-hidden"
          accept=".pdf,.jpg,.jpeg,.png,application/pdf,image/jpeg,image/png"
          disabled={disabled || room <= 0 || !pickType}
          onChange={(e) => {
            void add(e.target.files);
            e.target.value = ""; // so the same file can be chosen again after removing it
          }}
        />
        <FileUp size={28} aria-hidden="true" />
        <strong>{pickType ? "Choose files or drop them here" : "Choose a document type first"}</strong>
        <span>
          PDF, JPG or PNG, up to {describeSize(options.maxFileSizeBytes)} each.
          They are uploaded right after the application is saved.
          {room <= 0 ? ` Up to ${MAX_PENDING_DOCUMENTS} files.` : ""}
        </span>
      </label>

      {items.map((d, i) => {
        const type = options.types.find((t) => String(t.id) === d.typeId);
        const err = (f: string) => errors[`documents.${i}.${f}`];
        const fileProblem = d.fileError ?? err("file");
        return (
          <div className="repeat-row" key={d.key}>
            <div className="repeat-row__title" style={{ overflowWrap: "anywhere" }}>
              {d.file.name}
              <span className="cell-muted" style={{ fontWeight: 400 }}>
                {" "}
                · {shortSize(d.file.size)}
                {d.kind ? ` · ${d.kind}` : ""}
              </span>
            </div>
            {fileProblem && (
              <p className="field__error" style={{ gridColumn: "1 / -1", margin: 0 }}>
                <CircleAlert size={14} aria-hidden="true" />
                {fileProblem} Remove it and choose another file.
              </p>
            )}
            <SelectField
              id={`pending-type-${d.key}`}
              label="Document type"
              required
              emptyLabel="Choose…"
              options={options.types.map((t) => ({ value: t.id, label: t.name }))}
              value={d.typeId}
              onChange={(v) => update(d.key, { typeId: v })}
              error={err("documentTypeId")}
              hint={
                type?.isSensitive
                  ? "Identity / medical: only staff allowed to see sensitive details can open it."
                  : undefined
              }
            />
            {type?.hasExpiry && (
              <TextField
                id={`pending-expiry-${d.key}`}
                type="date"
                label="Expiry date"
                required
                value={d.expiryDate}
                onChange={(v) => update(d.key, { expiryDate: v })}
                error={err("expiryDate")}
              />
            )}
            <TextField
              id={`pending-title-${d.key}`}
              label="Label"
              maxLength={120}
              placeholder="Optional, e.g. Passport (old)"
              value={d.title}
              onChange={(v) => update(d.key, { title: v })}
              error={err("title")}
            />
            <button
              type="button"
              className="btn btn--secondary"
              aria-label={`Remove ${d.file.name}`}
              onClick={() => onChange(items.filter((x) => x.key !== d.key))}
              disabled={disabled}
            >
              <Trash size={16} aria-hidden="true" />
              Remove
            </button>
          </div>
        );
      })}
    </div>
  );
}
