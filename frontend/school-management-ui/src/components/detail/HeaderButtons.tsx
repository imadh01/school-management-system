import { Link } from "react-router-dom";
import { LogOut, Pencil, Trash } from "lucide-react";

/** "Edit" in a detail page's header; opens the full-page edit form. */
export function EditButton({
  to,
  primary = false,
}: {
  to: string;
  primary?: boolean;
}) {
  return (
    <Link
      className={primary ? "btn btn--primary" : "btn btn--secondary"}
      to={to}
    >
      <Pencil size={16} aria-hidden="true" />
      Edit
    </Link>
  );
}

/** "Mark as left" in a student's header; pair it with useConfirmAction. */
export function MarkLeftButton({ onClick }: { onClick: () => void }) {
  return (
    <button type="button" className="btn btn--secondary" onClick={onClick}>
      <LogOut size={16} aria-hidden="true" />
      Mark as left
    </button>
  );
}

/** "Delete" in a detail page's header; pair it with useConfirmAction. */
export function DeleteButton({ onClick }: { onClick: () => void }) {
  return (
    <button type="button" className="btn btn--secondary" onClick={onClick}>
      <Trash size={16} aria-hidden="true" />
      Delete
    </button>
  );
}
