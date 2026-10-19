import { Funnel } from "lucide-react";
import type { ReactNode } from "react";
import { SearchableSelect } from "./form/SearchableSelect";

/** U1: the shaded row of filters between a panel's header and its table. */
export function FilterBar({ children }: { children: ReactNode }) {
  return (
    <div className="filter-bar" role="group" aria-label="Filters">
      <Funnel className="filter-bar__icon" size={18} aria-hidden="true" />
      {children}
    </div>
  );
}

/** One drop-down filter in a FilterBar. The first option ("All classes") means "no filter" (""). */
export function FilterSelect({
  label,
  value,
  onChange,
  allLabel,
  options,
}: {
  /** Read by screen readers ("Class"). */
  label: string;
  value: string;
  onChange: (value: string) => void;
  allLabel: string;
  options: readonly string[];
}) {
  return (
    <div className="filter-select">
      <SearchableSelect
        className="select"
        ariaLabel={label}
        options={options.map((o) => ({ value: o, label: o }))}
        emptyLabel={allLabel}
        value={value}
        onChange={onChange}
      />
    </div>
  );
}

/** A yes/no filter ("Has allergies"). */
export function FilterCheckbox({
  label,
  checked,
  onChange,
}: {
  label: string;
  checked: boolean;
  onChange: (checked: boolean) => void;
}) {
  return (
    <label className="checkbox-row" style={{ marginTop: 0 }}>
      <input
        type="checkbox"
        checked={checked}
        onChange={(e) => onChange(e.target.checked)}
      />
      {label}
    </label>
  );
}

/** "Clear" at the end of a FilterBar, shown only while a filter is set. */
export function ClearFiltersButton({
  visible,
  onClear,
}: {
  visible: boolean;
  onClear: () => void;
}) {
  if (!visible) return null;
  return (
    <button
      type="button"
      className="btn btn--secondary btn--sm"
      onClick={onClear}
    >
      Clear
    </button>
  );
}

/** A "From" / "To" date filter (yyyy-mm-dd, "" = no limit). */
export function FilterDate({
  label,
  value,
  onChange,
}: {
  label: string;
  value: string;
  onChange: (value: string) => void;
}) {
  return (
    <label className="filter-date">
      <span>{label}</span>
      <input
        className="select"
        type="date"
        value={value}
        onChange={(e) => onChange(e.target.value)}
      />
    </label>
  );
}
