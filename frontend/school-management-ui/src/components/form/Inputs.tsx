import type { ReactNode } from "react";
import { Field } from "./Field";
import { SearchableSelect } from "./SearchableSelect";

/**
 * U2: the standard form controls, each a <Field> (label, required mark, error, hint) around one
 * input. Pages pass `{...bind("firstName")}` (see utils/form `fieldBinder`) for id, value, onChange
 * and error, plus the label.
 */
interface CommonProps {
  id: string;
  label: string;
  error?: string;
  required?: boolean;
  hint?: ReactNode;
  span?: 1 | 2 | "full";
  disabled?: boolean;
}

interface ValueProps {
  value: string | number | null | undefined;
  onChange: (value: string) => void;
}

export function TextField({
  type = "text",
  placeholder,
  autoComplete,
  min,
  max,
  maxLength,
  autoFocus,
  value,
  onChange,
  ...field
}: CommonProps &
  ValueProps & {
    type?: "text" | "email" | "tel" | "date" | "number" | "password" | "url";
    placeholder?: string;
    autoComplete?: string;
    min?: number;
    max?: number;
    maxLength?: number;
    autoFocus?: boolean;
  }) {
  return (
    <Field {...field}>
      {(c) => (
        <input
          {...c}
          type={type}
          value={value ?? ""}
          placeholder={placeholder}
          autoComplete={autoComplete}
          min={min}
          max={max}
          maxLength={maxLength}
          autoFocus={autoFocus}
          disabled={field.disabled}
          onChange={(e) => onChange(e.target.value)}
        />
      )}
    </Field>
  );
}

export function TextAreaField({
  rows = 3,
  placeholder,
  value,
  onChange,
  ...field
}: CommonProps & ValueProps & { rows?: number; placeholder?: string }) {
  return (
    <Field {...field}>
      {(c) => (
        <textarea
          {...c}
          rows={rows}
          value={value ?? ""}
          placeholder={placeholder}
          disabled={field.disabled}
          onChange={(e) => onChange(e.target.value)}
        />
      )}
    </Field>
  );
}

export interface SelectOption {
  value: string | number;
  label: string;
}

export function SelectField({
  options,
  emptyLabel,
  value,
  onChange,
  ...field
}: CommonProps &
  ValueProps & {
    /** Plain strings (value = label) or { value, label } pairs. */
    options: readonly (string | SelectOption)[];
    /** First option with value "" (e.g. "Not specified"). Omit when a choice is required. */
    emptyLabel?: string;
  }) {
  return (
    <Field {...field}>
      {(c) => (
        <SearchableSelect
          {...c}
          options={options.map((o) =>
            typeof o === "string" ? { value: o, label: o } : o,
          )}
          emptyLabel={emptyLabel}
          value={value}
          disabled={field.disabled}
          onChange={onChange}
        />
      )}
    </Field>
  );
}

/** A checkbox with its label to the right, sized as a 44 px target. */
export function CheckboxField({
  id,
  label,
  checked,
  onChange,
  disabled,
  hint,
}: {
  id: string;
  label: ReactNode;
  checked: boolean;
  onChange: (checked: boolean) => void;
  disabled?: boolean;
  hint?: ReactNode;
}) {
  return (
    <div className="field field--checkbox">
      <label className="checkbox-row" htmlFor={id}>
        <input
          id={id}
          type="checkbox"
          checked={checked}
          disabled={disabled}
          onChange={(e) => onChange(e.target.checked)}
        />
        {label}
      </label>
      {hint && <p className="field-note">{hint}</p>}
    </div>
  );
}

/** Toggle buttons for picking several values (dietary needs, notifications) or one (house). */
export function ToggleGroup({
  legend,
  options,
  isSelected,
  onToggle,
  isDisabled,
  hideLegend,
  extra,
}: {
  legend: string;
  options: readonly string[];
  isSelected: (option: string) => boolean;
  onToggle: (option: string) => void;
  /** Options shown but not pickable (e.g. a subject another teacher already teaches). */
  isDisabled?: (option: string) => boolean;
  hideLegend?: boolean;
  /** Extra buttons after the options (e.g. "No restrictions"). */
  extra?: ReactNode;
}) {
  return (
    <fieldset className="field field--plain">
      <legend className={hideLegend ? "visually-hidden" : undefined}>
        {legend}
      </legend>
      <div className="tag-group">
        {options.map((option) => (
          <button
            key={option}
            type="button"
            aria-pressed={isSelected(option)}
            disabled={isDisabled?.(option)}
            className={`tag-btn tag-btn--${option.replace(/\W+/g, "")} ${isSelected(option) ? "selected" : ""}`}
            onClick={() => onToggle(option)}
          >
            {option}
          </button>
        ))}
        {extra}
      </div>
    </fieldset>
  );
}
