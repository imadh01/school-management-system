import { useEffect, useRef } from "react";
import type { TickState } from "../utils/permissionMatrix";

/**
 * E3c: a checkbox for "all of these": ticked when all are, a dash when only some are. A locked one
 * stays focusable (aria-disabled) so a keyboard user hears why it cannot be changed.
 */
export function TriStateCheckbox({
  state,
  label,
  onToggle,
  locked,
  title,
  col,
}: {
  state: TickState;
  /** The accessible name, e.g. "Students group, all Edit". */
  label: string;
  onToggle: () => void;
  locked?: boolean;
  title?: string;
  /** Column key for arrow-key movement in the matrix. */
  col: string;
}) {
  const ref = useRef<HTMLInputElement>(null);
  useEffect(() => {
    if (ref.current) ref.current.indeterminate = state === "some";
  }, [state]);

  return (
    <input
      ref={ref}
      type="checkbox"
      className="perm-check"
      data-col={col}
      checked={state === "all"}
      aria-label={label}
      aria-disabled={locked || undefined}
      title={title}
      onChange={() => {
        if (!locked) onToggle();
      }}
    />
  );
}
