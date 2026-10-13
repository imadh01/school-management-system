import { Download, Eye, Pencil, Plus, Printer, Trash } from "lucide-react";
import { Link, useNavigate } from "react-router-dom";
import type { ReactNode } from "react";
import { IconButton } from "./IconButton";

/** "Export CSV" in a list panel's header. */
export function ExportCsvButton({
  onClick,
  disabled,
}: {
  onClick: () => void;
  disabled?: boolean;
}) {
  return (
    <button
      type="button"
      className="btn btn--secondary"
      onClick={onClick}
      disabled={disabled}
    >
      <Download size={16} aria-hidden="true" />
      Export CSV
    </button>
  );
}

/** "Print" in a page hero. */
export function PrintButton() {
  return (
    <button
      type="button"
      className="btn btn--secondary"
      onClick={() => window.print()}
    >
      <Printer size={16} aria-hidden="true" />
      Print
    </button>
  );
}

/** The primary "New …" button of a list page; it opens the full-page add form. */
export function NewRecordLink({ to, label }: { to: string; label: string }) {
  return (
    <Link className="btn btn--primary" to={to}>
      <Plus size={16} aria-hidden="true" />
      {label}
    </Link>
  );
}

/**
 * The action buttons at the end of a table row: View, Edit, Delete (each only when given), plus
 * any extra buttons. `name` makes every label specific for screen readers ("Edit Aisha Khan").
 */
export function RowActions({
  name,
  viewTo,
  editTo,
  onDelete,
  children,
}: {
  name: string;
  viewTo?: string;
  editTo?: string;
  onDelete?: () => void;
  children?: ReactNode;
}) {
  const navigate = useNavigate();
  return (
    <div className="row-actions">
      {viewTo && (
        <IconButton
          icon={Eye}
          label={`View ${name}`}
          onClick={() => navigate(viewTo)}
        />
      )}
      {editTo && (
        <IconButton
          icon={Pencil}
          tone="info"
          label={`Edit ${name}`}
          onClick={() => navigate(editTo)}
        />
      )}
      {children}
      {onDelete && (
        <IconButton
          icon={Trash}
          tone="bad"
          label={`Delete ${name}`}
          onClick={onDelete}
        />
      )}
    </div>
  );
}
