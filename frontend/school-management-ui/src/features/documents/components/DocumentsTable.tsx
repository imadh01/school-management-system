import { useState, type ReactNode } from "react";
import { Link } from "react-router-dom";
import {
  CircleCheck,
  CircleX,
  Download,
  Lock,
  Trash,
  Undo2,
} from "lucide-react";
import { DataTable, type Column } from "@/components/DataTable";
import { Badge, StatusBadge } from "@/components/Badge";
import { IconButton } from "@/components/IconButton";
import { useToast } from "@/components/Toast";
import { useCan } from "@/hooks/useCan";
import { useConfirmAction } from "@/hooks/useConfirmAction";
import { Permission } from "@/routes/permissions";
import { getApiErrors } from "@/utils/apiError";
import { shortSize } from "@/utils/file";
import { formatDate, formatUtcDate } from "@/utils/format";
import { documentService } from "../services/documentService";
import { RejectDocumentDialog } from "./RejectDocumentDialog";
import {
  EXPIRY_LABEL,
  EXPIRY_TONE,
  fileKind,
  type DocumentResponse,
} from "../types/document.types";

const ownerLink = (d: DocumentResponse) =>
  d.ownerType === "Student"
    ? `/students/${d.ownerId}`
    : `/admissions/${d.ownerId}`;

/**
 * G1b: the list of documents with their review actions. Used by the Documents tab of a student
 * or an application (without the owner column) and by the Documents page (with it).
 *
 * Buttons follow the API's rules so nobody is offered an action that will be refused: Verify and
 * Reject only on a pending document someone ELSE uploaded, Unverify only on a verified one, Delete
 * only when not verified, Download only when the caller may open the file.
 */
export function DocumentsTable({
  rows,
  showOwner = false,
  loading,
  empty,
  resetPageKey,
  onChanged,
}: {
  rows: DocumentResponse[];
  showOwner?: boolean;
  loading?: boolean;
  empty: ReactNode;
  resetPageKey?: unknown;
  /** Reload after a change (verify, reject, unverify, delete). */
  onChanged: () => Promise<unknown>;
}) {
  const can = useCan();
  const toast = useToast();
  const canVerify = can(Permission.DocumentsVerify);
  const canDelete = can(Permission.DocumentsDelete);
  const [rejecting, setRejecting] = useState<DocumentResponse | null>(null);
  const [busyId, setBusyId] = useState<number | null>(null);

  const label = (d: DocumentResponse) =>
    `${d.documentTypeName} of ${d.ownerName}`;

  const run = async (
    d: DocumentResponse,
    action: () => Promise<unknown>,
    success: string,
    failure: string,
  ) => {
    setBusyId(d.id);
    try {
      await action();
      toast.success(success);
      await onChanged();
    } catch (err) {
      toast.error(getApiErrors(err, failure)[0]);
    } finally {
      setBusyId(null);
    }
  };

  const unverify = useConfirmAction<DocumentResponse>({
    title: "Unverify document",
    message: (d) =>
      `Take back the verification of the ${d.documentTypeName.toLowerCase()} of ${d.ownerName}? It goes back to Pending, and the change is recorded in the audit log.`,
    confirmLabel: "Unverify",
    run: async (d) => {
      await documentService.unverify(d);
      await onChanged();
    },
    success: (d) => `The ${d.documentTypeName.toLowerCase()} is pending again.`,
    failure: "Could not unverify this document.",
  });

  const remove = useConfirmAction<DocumentResponse>({
    title: "Delete document",
    message: (d) =>
      `Delete the ${d.documentTypeName.toLowerCase()} of ${d.ownerName} (${d.originalFileName})? It disappears from the lists; the file is kept for the retention period and then removed.`,
    confirmLabel: "Delete",
    danger: true,
    run: async (d) => {
      await documentService.delete(d);
      await onChanged();
    },
    success: (d) => `The ${d.documentTypeName.toLowerCase()} was deleted.`,
    failure: "Could not delete this document.",
  });

  const columns: Column<DocumentResponse>[] = [
    {
      key: "document",
      header: "Document",
      // Newest first is the API's order; sorting here groups by type.
      sortValue: (d) => d.documentTypeName,
      render: (d) => (
        <div>
          <span className="cell-strong">
            {d.documentTypeName}
            {d.isSensitive && (
              <Lock
                size={13}
                role="img"
                aria-label="Sensitive"
                style={{ marginLeft: 6, verticalAlign: "-1px" }}
              />
            )}
          </span>
          {d.title && <div>{d.title}</div>}
          <div className="cell-muted cell-wrap">
            {d.originalFileName} · {fileKind(d.contentType)} ·{" "}
            {shortSize(d.sizeBytes)}
          </div>
          {showOwner && (
            <div className="show-below-md">
              <Link to={`${ownerLink(d)}?tab=documents`}>{d.ownerName}</Link> ·{" "}
              {d.ownerNumber}
            </div>
          )}
          <div className="cell-muted">
            Uploaded {formatUtcDate(d.uploadedAt)}
            {d.uploadedBy && ` by ${d.uploadedBy}`}
          </div>
        </div>
      ),
    },
    ...(showOwner
      ? [
          {
            key: "owner",
            header: "Belongs to",
            hideBelow: "md" as const,
            sortValue: (d: DocumentResponse) => d.ownerName,
            render: (d: DocumentResponse) => (
              <div>
                <Link to={`${ownerLink(d)}?tab=documents`}>{d.ownerName}</Link>
                <div className="cell-muted">
                  {d.ownerType === "Student" ? "Student" : "Applicant"} ·{" "}
                  {d.ownerNumber}
                </div>
              </div>
            ),
          },
        ]
      : []),
    {
      key: "expiry",
      header: "Expiry",
      hideBelow: "md",
      sortValue: (d) => d.expiryDate,
      render: (d) =>
        d.expiryDate ? (
          <div>
            <div>{formatDate(d.expiryDate)}</div>
            {d.expiryState && (
              <Badge tone={EXPIRY_TONE[d.expiryState]}>
                {EXPIRY_LABEL[d.expiryState]}
              </Badge>
            )}
          </div>
        ) : (
          <span className="cell-muted">—</span>
        ),
    },
    {
      key: "status",
      header: "Status",
      sortValue: (d) => d.status,
      render: (d) => (
        <div>
          <StatusBadge status={d.status} />
          {d.reviewedBy && (
            <div className="cell-muted">
              by {d.reviewedBy}, {formatUtcDate(d.reviewedAt)}
            </div>
          )}
          {d.rejectionReason && (
            <div className="cell-wrap">{d.rejectionReason}</div>
          )}
          {canVerify && d.isOwnUpload && d.status === "Pending" && (
            <div className="cell-muted cell-wrap">
              Your upload: someone else reviews it
            </div>
          )}
        </div>
      ),
    },
    {
      key: "actions",
      header: "Actions",
      render: (d) => {
        const busy = busyId === d.id;
        const pending = d.status === "Pending";
        return (
          <div className="row-actions">
            <IconButton
              icon={Download}
              label={
                d.canDownload
                  ? `Download ${label(d)}`
                  : "Identity or medical document: you need the permission to see sensitive details to open it"
              }
              disabled={!d.canDownload || busy}
              onClick={() =>
                void documentService
                  .download(d)
                  .catch((err) =>
                    toast.error(
                      getApiErrors(err, "Could not download this document.")[0],
                    ),
                  )
              }
            />
            {canVerify && pending && (
              <>
                <IconButton
                  icon={CircleCheck}
                  tone="ok"
                  label={
                    d.isOwnUpload
                      ? "You uploaded this document, so someone else must verify it"
                      : `Verify ${label(d)}`
                  }
                  disabled={d.isOwnUpload || busy}
                  onClick={() =>
                    void run(
                      d,
                      () => documentService.verify(d),
                      `The ${d.documentTypeName.toLowerCase()} was verified.`,
                      "Could not verify this document.",
                    )
                  }
                />
                <IconButton
                  icon={CircleX}
                  tone="bad"
                  label={
                    d.isOwnUpload
                      ? "You uploaded this document, so someone else must review it"
                      : `Reject ${label(d)}`
                  }
                  disabled={d.isOwnUpload || busy}
                  onClick={() => setRejecting(d)}
                />
              </>
            )}
            {canVerify && d.status === "Verified" && (
              <IconButton
                icon={Undo2}
                label={`Unverify ${label(d)}`}
                disabled={busy}
                onClick={() => unverify.ask(d)}
              />
            )}
            {canDelete && (
              <IconButton
                icon={Trash}
                tone="bad"
                label={
                  d.status === "Verified"
                    ? "A verified document must be unverified before it can be deleted"
                    : `Delete ${label(d)}`
                }
                disabled={d.status === "Verified" || busy}
                onClick={() => remove.ask(d)}
              />
            )}
          </div>
        );
      },
    },
  ];

  return (
    <>
      <DataTable
        caption="Documents"
        columns={columns}
        rows={rows}
        rowKey={(d) => d.id}
        loading={loading}
        empty={empty}
        resetPageKey={resetPageKey}
      />
      {unverify.dialog}
      {remove.dialog}
      {rejecting && (
        <RejectDocumentDialog
          document={rejecting}
          onClose={() => setRejecting(null)}
          onSubmit={async (reason) => {
            await documentService.reject(rejecting, reason);
            toast.success(
              `The ${rejecting.documentTypeName.toLowerCase()} was rejected.`,
            );
            await onChanged();
          }}
        />
      )}
    </>
  );
}
