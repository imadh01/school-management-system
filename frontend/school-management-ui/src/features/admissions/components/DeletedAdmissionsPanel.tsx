import { useState } from "react";
import { RotateCcw, Trash2 } from "lucide-react";
import { admissionService } from "../services/admissionService";
import {
  ADMISSION_STATUS_TONE,
  type DeletedAdmissionResponse,
} from "../types/admission.types";
import { Panel } from "@/components/Panel";
import { DataTable, type Column } from "@/components/DataTable";
import { Badge } from "@/components/Badge";
import { IconButton } from "@/components/IconButton";
import { Notice } from "@/components/Notice";
import { useAsyncData } from "@/hooks/useAsyncData";
import { useConfirmAction } from "@/hooks/useConfirmAction";
import { useAuth } from "@/store/AuthContext";
import { formatUtcDate } from "@/utils/format";

const columns: Column<DeletedAdmissionResponse>[] = [
  {
    key: "regNo",
    header: "Reg No",
    sortValue: (a) => a.regNo,
    render: (a) => <span className="cell-strong">{a.regNo}</span>,
  },
  {
    key: "applicant",
    header: "Applicant",
    sortValue: (a) => a.applicantName,
    render: (a) => (
      <div>
        <div className="person__name">{a.applicantName}</div>
        <div className="person__sub">{a.phone}</div>
      </div>
    ),
  },
  {
    key: "class",
    header: "Applied for",
    sortValue: (a) => a.appliedForClassSectionName,
    render: (a) => a.appliedForClassSectionName,
  },
  {
    key: "status",
    header: "Status when deleted",
    sortValue: (a) => a.status,
    render: (a) => (
      <Badge tone={ADMISSION_STATUS_TONE[a.status]}>{a.status}</Badge>
    ),
  },
  {
    key: "deletedAt",
    header: "Deleted on",
    sortValue: (a) => a.deletedAt,
    render: (a) => formatUtcDate(a.deletedAt),
  },
  {
    key: "deletedBy",
    header: "Deleted by",
    sortValue: (a) => a.deletedBy ?? "",
    render: (a) => a.deletedBy ?? "Unknown",
  },
];

function DeletedTable({ onRestored }: { onRestored: () => void }) {
  // Restore is an Admin action (the API refuses anyone else), so only Admins see the button.
  const { user } = useAuth();
  const isAdmin = user?.roles.includes("Admin") ?? false;

  const list = useAsyncData(
    () => admissionService.getDeleted(),
    [],
    "Could not load the deleted applications.",
  );

  const restore = useConfirmAction<DeletedAdmissionResponse>({
    title: "Restore application",
    message: (a) =>
      `Restore the application for ${a.applicantName} (${a.regNo})? It goes back to the applications list as it was.`,
    confirmLabel: "Restore",
    danger: false,
    run: async (a) => {
      await admissionService.restore(a.id);
      await list.reload();
      onRestored();
    },
    success: (a) => `Application ${a.regNo} was restored.`,
    failure: "Could not restore this application.",
  });

  const allColumns: Column<DeletedAdmissionResponse>[] = isAdmin
    ? [
        ...columns,
        {
          key: "actions",
          header: "",
          render: (a) => (
            <IconButton
              icon={RotateCcw}
              label={`Restore ${a.regNo}`}
              tone="info"
              onClick={() => restore.ask(a)}
            />
          ),
        },
      ]
    : columns;

  return (
    <>
      {list.error && (
        <Notice tone="warn" style={{ margin: 16 }}>
          {list.error}
        </Notice>
      )}
      <DataTable
        caption="Deleted applications"
        columns={allColumns}
        rows={list.data ?? []}
        rowKey={(a) => a.id}
        loading={list.isLoading && !list.data}
        empty={<p className="cell-muted">No application has been deleted.</p>}
      />
      {restore.dialog}
    </>
  );
}

/**
 * Deleted applications are not erased: they stay in the database and here, with who deleted them
 * and when. Only for users who may delete applications. Opened on demand so the list is not loaded
 * for everyone. `version` changes after a delete so an open list shows the new row.
 */
export function DeletedAdmissionsPanel({
  version,
  onRestored,
}: {
  version: number;
  onRestored: () => void;
}) {
  const [open, setOpen] = useState(false);
  return (
    <Panel
      title="Deleted applications"
      subtitle="Removed from the list above but kept for the record."
      icon={Trash2}
      actions={
        <button
          type="button"
          className="btn btn--secondary btn--sm"
          aria-expanded={open}
          onClick={() => setOpen((o) => !o)}
        >
          {open ? "Hide" : "Show deleted"}
        </button>
      }
    >
      {open && <DeletedTable key={version} onRestored={onRestored} />}
    </Panel>
  );
}
