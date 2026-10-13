import type { LucideIcon } from "lucide-react";
import type { ReactNode } from "react";
import { EmptyState } from "./EmptyState";

/**
 * What a list's table shows when it has no rows. Two cases:
 *   - nothing exists yet → "No parents yet", an optional hint and the "New …" button (`createAction`);
 *   - the filters hide everything → "No parents match these filters" and "Clear filters".
 */
export function ListEmptyState({
  icon,
  noun,
  hasRecords,
  onClearFilters,
  emptyMessage,
  createAction,
}: {
  icon: LucideIcon;
  /** Plural, lower case: "parents". */
  noun: string;
  /** True when the list has records and only the filters hide them. */
  hasRecords: boolean;
  onClearFilters: () => void;
  emptyMessage?: ReactNode;
  createAction?: ReactNode;
}) {
  if (hasRecords) {
    return (
      <EmptyState
        icon={icon}
        title={`No ${noun} match these filters`}
        action={
          <button
            type="button"
            className="btn btn--secondary"
            onClick={onClearFilters}
          >
            Clear filters
          </button>
        }
      />
    );
  }
  return (
    <EmptyState
      icon={icon}
      title={`No ${noun} yet`}
      message={emptyMessage}
      action={createAction}
    />
  );
}
