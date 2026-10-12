import { useEffect, useId } from "react";
import { CircleHelp, Trash, X } from "lucide-react";

interface Props {
  isOpen: boolean;
  title: string;
  message: string;
  confirmLabel?: string;
  cancelLabel?: string;
  danger?: boolean;
  onConfirm: () => void;
  onCancel: () => void;
}

export function ConfirmDialog({
  isOpen,
  title,
  message,
  confirmLabel = "Confirm",
  cancelLabel = "Cancel",
  danger = true,
  onConfirm,
  onCancel,
}: Props) {
  const titleId = useId();

  // Escape cancels, as in any dialog.
  useEffect(() => {
    if (!isOpen) return;
    const onKey = (e: KeyboardEvent) => e.key === "Escape" && onCancel();
    window.addEventListener("keydown", onKey);
    return () => window.removeEventListener("keydown", onKey);
  }, [isOpen, onCancel]);

  if (!isOpen) return null;
  const Icon = danger ? Trash : CircleHelp;

  return (
    <div className="modal-backdrop">
      <div
        className="modal"
        style={{ maxWidth: 440 }}
        role="alertdialog"
        aria-modal="true"
        aria-labelledby={titleId}
      >
        <div className="modal__header">
          <h2 id={titleId}>
            <Icon size={18} aria-hidden="true" />
            {title}
          </h2>
          <button
            type="button"
            className="modal__close"
            aria-label="Close"
            onClick={onCancel}
          >
            <X size={16} aria-hidden="true" />
          </button>
        </div>
        <div className="modal__body">
          <p style={{ fontSize: 14, lineHeight: 1.6, margin: 0 }}>{message}</p>
        </div>
        <div className="modal__footer">
          <button
            type="button"
            className="btn btn--secondary"
            onClick={onCancel}
          >
            {cancelLabel}
          </button>
          <button
            type="button"
            className={danger ? "btn btn--danger" : "btn btn--primary"}
            onClick={onConfirm}
            autoFocus
          >
            {confirmLabel}
          </button>
        </div>
      </div>
    </div>
  );
}
