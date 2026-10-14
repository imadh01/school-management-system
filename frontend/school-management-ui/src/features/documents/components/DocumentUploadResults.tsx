import { useState } from "react";
import { Link } from "react-router-dom";
import { CircleAlert, CircleCheck, RotateCw } from "lucide-react";
import { Notice } from "@/components/Notice";
import { getApiErrors } from "@/utils/apiError";
import { shortSize } from "@/utils/file";
import { documentService } from "../services/documentService";
import type { DocumentOwner } from "../types/document.types";
import type { PendingDocument } from "../types/pendingDocument";

/**
 * G1c: shown after an application was saved but some of its documents did not upload. The
 * application stays saved (this page never saves it again, so it cannot be registered twice);
 * each failed file can be retried, or added later from the application's Documents tab.
 */
export function DocumentUploadResults({
  owner,
  title,
  failed,
  uploadedCount,
  onFinished,
}: {
  owner: Extract<DocumentOwner, { type: "admission" }>;
  /** "Application ADM-2026-0042 was registered" */
  title: string;
  failed: { item: PendingDocument; message: string }[];
  uploadedCount: number;
  /** Called when nothing is left to retry. */
  onFinished: () => void;
}) {
  const [rows, setRows] = useState(
    failed.map((f) => ({ ...f, busy: false })),
  );
  const [uploaded, setUploaded] = useState(uploadedCount);

  const retry = async (key: string) => {
    const row = rows.find((r) => r.item.key === key);
    if (!row) return;
    setRows((rs) => rs.map((r) => (r.item.key === key ? { ...r, busy: true } : r)));
    try {
      await documentService.upload(owner, {
        documentTypeId: Number(row.item.typeId),
        title: row.item.title,
        expiryDate: row.item.expiryDate,
        file: row.item.file,
      });
      const left = rows.filter((r) => r.item.key !== key);
      setRows(left);
      setUploaded((n) => n + 1);
      if (left.length === 0) onFinished();
    } catch (err) {
      const message = getApiErrors(err, "Could not upload this file.")[0];
      setRows((rs) =>
        rs.map((r) => (r.item.key === key ? { ...r, busy: false, message } : r)),
      );
    }
  };

  const busy = rows.some((r) => r.busy);

  return (
    <div className="card" style={{ maxWidth: 720, margin: "32px auto", padding: 24 }}>
      <h1 style={{ fontSize: 20, margin: "0 0 6px", display: "flex", gap: 8, alignItems: "center" }}>
        <CircleCheck size={22} aria-hidden="true" />
        {title}
      </h1>
      <p className="cell-muted" style={{ margin: "0 0 16px" }}>
        {uploaded} document{uploaded === 1 ? "" : "s"} uploaded.{" "}
        {rows.length} could not be uploaded. The application itself is saved.
      </p>
      <div role="status">
        {rows.length > 0 && (
          <Notice tone="warn" style={{ marginBottom: 12 }}>
            Retry the files below, or open the application and add them from its Documents tab.
          </Notice>
        )}
      </div>
      <ul style={{ listStyle: "none", margin: 0, padding: 0, display: "grid", gap: 10 }}>
        {rows.map((r) => (
          <li
            key={r.item.key}
            style={{ display: "flex", gap: 12, alignItems: "center", justifyContent: "space-between", flexWrap: "wrap" }}
          >
            <div style={{ minWidth: 0 }}>
              <strong style={{ overflowWrap: "anywhere" }}>{r.item.file.name}</strong>
              <span className="cell-muted"> · {shortSize(r.item.file.size)}</span>
              <p className="field__error" style={{ margin: "2px 0 0" }}>
                <CircleAlert size={14} aria-hidden="true" />
                {r.message}
              </p>
            </div>
            <button
              type="button"
              className="btn btn--secondary"
              disabled={busy}
              onClick={() => void retry(r.item.key)}
            >
              <RotateCw size={16} aria-hidden="true" />
              {r.busy ? "Uploading…" : "Retry"}
            </button>
          </li>
        ))}
      </ul>
      <div style={{ marginTop: 20 }}>
        <Link className="btn btn--primary" to={`/admissions/${owner.id}`}>
          Open the application
        </Link>
      </div>
    </div>
  );
}
