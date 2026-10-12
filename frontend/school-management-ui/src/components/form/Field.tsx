import type { ReactNode } from "react";
import { CircleAlert } from "lucide-react";

/** What a Field hands to its control so the label, hint and error are wired up for screen readers. */
export interface ControlProps {
  id: string;
  "aria-invalid"?: true;
  "aria-describedby"?: string;
  "aria-required"?: true;
}

interface Props {
  id: string;
  label: string;
  required?: boolean;
  /** The message for this field, from the browser-side check or the API. */
  error?: string;
  hint?: ReactNode;
  /** How many grid columns the field takes: 1 (default), 2, or the whole row. */
  span?: 1 | 2 | "full";
  /** Renders the control. Spread the props onto the input/select/textarea. */
  children: (control: ControlProps) => ReactNode;
}

/**
 * U2: one labelled form field. The error is text under the field with an icon (never colour
 * alone), and the control is linked to it with aria-describedby, so a screen reader reads the
 * label, the problem and the hint together.
 */
export function Field({
  id,
  label,
  required,
  error,
  hint,
  span = 1,
  children,
}: Props) {
  const errorId = `${id}-error`;
  const hintId = `${id}-hint`;
  const describedBy =
    [error ? errorId : null, hint ? hintId : null].filter(Boolean).join(" ") ||
    undefined;

  const spanClass =
    span === "full" ? " field--full" : span === 2 ? " field--span2" : "";

  return (
    <div className={`field${error ? " field--invalid" : ""}${spanClass}`}>
      <label htmlFor={id}>
        {label}
        {required && (
          <>
            <span className="required" aria-hidden="true">
              *
            </span>
            <span className="visually-hidden"> (required)</span>
          </>
        )}
      </label>
      {children({
        id,
        "aria-invalid": error ? true : undefined,
        "aria-describedby": describedBy,
        "aria-required": required ? true : undefined,
      })}
      {error && (
        <p className="field__error" id={errorId}>
          <CircleAlert size={14} aria-hidden="true" />
          {error}
        </p>
      )}
      {hint && (
        <p className="field-note" id={hintId}>
          {hint}
        </p>
      )}
    </div>
  );
}
