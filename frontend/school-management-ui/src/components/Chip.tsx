import type { ReactNode } from "react";
import { X } from "lucide-react";

/** A small pill, with an × button when it can be removed (a teacher's subject in a class). */
export function Chip({
  label,
  onRemove,
  removeLabel,
  disabled,
}: {
  label: string;
  onRemove?: () => void;
  /** For screen readers: "Remove Mathematics from Class 5 A". */
  removeLabel?: string;
  disabled?: boolean;
}) {
  return (
    <li className="chip">
      {label}
      {onRemove && (
        <button
          type="button"
          className="chip__remove"
          aria-label={removeLabel ?? `Remove ${label}`}
          disabled={disabled}
          onClick={onRemove}
        >
          <X size={14} aria-hidden="true" />
        </button>
      )}
    </li>
  );
}

/** The list that holds Chips. */
export function ChipList({
  label,
  children,
}: {
  label: string;
  children: ReactNode;
}) {
  return (
    <ul className="chip-list" aria-label={label}>
      {children}
    </ul>
  );
}
