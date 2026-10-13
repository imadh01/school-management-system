import { useState, type ReactNode } from "react";
import { FileText, Upload } from "lucide-react";
import { Panel } from "@/components/Panel";
import { EmptyState } from "@/components/EmptyState";
import { Notice } from "@/components/Notice";
import { useToast } from "@/components/Toast";
import { useAsyncData } from "@/hooks/useAsyncData";
import { useCan } from "@/hooks/useCan";
import { Permission } from "@/routes/permissions";
import { documentService } from "../services/documentService";
import { DocumentsTable } from "./DocumentsTable";
import { UploadDocumentDialog } from "./UploadDocumentDialog";
import type { DocumentOwner } from "../types/document.types";

/**
 * G1b: the Documents tab of a student or an application: the list, the review actions and the
 * Upload button. `uploadBlockedNote` replaces the Upload button with an explanation (an enrolled
 * application: its documents have moved to the student).
 */
export function DocumentsPanel({
  owner,
  ownerLabel,
  uploadBlockedNote,
}: {
  owner: DocumentOwner;
  /** "Aisha Khan (A1024)" */
  ownerLabel: string;
  uploadBlockedNote?: ReactNode;
}) {
  const can = useCan();
  const toast = useToast();
  const canUpload = can(Permission.DocumentsUpload) && !uploadBlockedNote;
  const [uploading, setUploading] = useState(false);

  const list = useAsyncData(
    () => documentService.getForOwner(owner),
    [owner.type, owner.id],
    "Could not load the documents.",
  );
  const options = useAsyncData(
    () => (canUpload ? documentService.getOptions() : Promise.resolve(null)),
    [canUpload],
    "Could not load the document types.",
  );
  const documents = list.data ?? [];

  const uploadButton = canUpload && (
    <button
      type="button"
      className="btn btn--primary"
      onClick={() => setUploading(true)}
      disabled={!options.data}
    >
      <Upload size={16} aria-hidden="true" />
      Upload
    </button>
  );

  return (
    <>
      {uploadBlockedNote && (
        <Notice style={{ marginBottom: 16 }}>{uploadBlockedNote}</Notice>
      )}
      <Panel
        title="Documents"
        subtitle={`${documents.length} document${documents.length === 1 ? "" : "s"}`}
        icon={FileText}
        actions={uploadButton || undefined}
      >
        {(list.error || options.error) && (
          <Notice tone="warn" style={{ margin: 16 }}>
            {list.error ?? options.error}
          </Notice>
        )}
        <DocumentsTable
          rows={documents}
          loading={list.isLoading && !list.data}
          onChanged={list.reload}
          empty={
            <EmptyState
              icon={FileText}
              title="No documents yet"
              message={
                canUpload
                  ? "Upload birth certificates, passports, report cards and other documents here. PDF, JPG or PNG."
                  : undefined
              }
              action={uploadButton || undefined}
            />
          }
        />
      </Panel>

      {uploading && options.data && (
        <UploadDocumentDialog
          owner={owner}
          ownerLabel={ownerLabel}
          options={options.data}
          onClose={() => setUploading(false)}
          onUploaded={(document) => {
            setUploading(false);
            toast.success(
              `The ${document.documentTypeName.toLowerCase()} was uploaded. Someone else verifies it.`,
            );
            void list.reload();
          }}
        />
      )}
    </>
  );
}
