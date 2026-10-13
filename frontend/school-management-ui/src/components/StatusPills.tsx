interface Props<T extends string> {
  /** Read by screen readers: what the pills filter. */
  label: string;
  options: readonly T[];
  value: T;
  onChange: (value: T) => void;
  /** How many records each option matches, shown in brackets. */
  counts?: Partial<Record<T, number>>;
}

/** U2: the row of status pills above a list ("All (12)", "Active (10)"...). Each pill is a toggle button. */
export function StatusPills<T extends string>({
  label,
  options,
  value,
  onChange,
  counts,
}: Props<T>) {
  return (
    <div className="pipeline" role="group" aria-label={label}>
      {options.map((option) => (
        <button
          key={option}
          type="button"
          aria-pressed={value === option}
          className={`pipeline__step${value === option ? " pipeline__step--active" : ""}`}
          onClick={() => onChange(option)}
        >
          {option}
          {counts && ` (${counts[option] ?? 0})`}
        </button>
      ))}
    </div>
  );
}
