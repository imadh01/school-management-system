/** Cancel + the main button of a pop-up. Both are disabled while the request runs. */
export function DialogFooter({
  onCancel,
  onSubmit,
  label,
  busyLabel,
  isSubmitting,
  danger = false,
  disabled = false,
  cancelLabel = "Cancel",
}: {
  onCancel: () => void;
  onSubmit: () => void;
  /** "Reject application" */
  label: string;
  /** "Rejecting…" */
  busyLabel: string;
  isSubmitting: boolean;
  /** Red main button (reject, unlink). */
  danger?: boolean;
  /** Main button off (e.g. nothing chosen yet). */
  disabled?: boolean;
  cancelLabel?: string;
}) {
  return (
    <>
      <button
        type="button"
        className="btn btn--secondary"
        onClick={onCancel}
        disabled={isSubmitting}
      >
        {cancelLabel}
      </button>
      <button
        type="button"
        className={danger ? "btn btn--danger" : "btn btn--primary"}
        onClick={onSubmit}
        disabled={isSubmitting || disabled}
      >
        {isSubmitting ? busyLabel : label}
      </button>
    </>
  );
}
