import type { ReactNode } from "react";
import { Plus, Trash } from "lucide-react";

/** U2: one row of a repeating group in a form (guardian 1, pickup person 2) with its Remove button. */
export function RepeatRow({
  title,
  onRemove,
  children,
}: {
  title: string;
  onRemove: () => void;
  children: ReactNode;
}) {
  return (
    <div className="repeat-row">
      <div className="repeat-row__title">{title}</div>
      {children}
      <button
        type="button"
        className="btn btn--secondary"
        aria-label={`Remove ${title.toLowerCase()}`}
        onClick={onRemove}
      >
        <Trash size={16} aria-hidden="true" />
        Remove
      </button>
    </div>
  );
}

/** "Add guardian" under a repeating group, switched off at the limit with a note saying so. */
export function AddRowButton({
  label,
  onClick,
  count,
  max,
  pluralNoun,
}: {
  label: string;
  onClick: () => void;
  count: number;
  max: number;
  /** "guardians", for "Up to 5 guardians." */
  pluralNoun: string;
}) {
  return (
    <>
      <button
        type="button"
        className="btn btn--secondary"
        onClick={onClick}
        disabled={count >= max}
      >
        <Plus size={16} aria-hidden="true" />
        {label}
      </button>
      {count >= max && (
        <p className="field-note">
          Up to {max} {pluralNoun}.
        </p>
      )}
    </>
  );
}
