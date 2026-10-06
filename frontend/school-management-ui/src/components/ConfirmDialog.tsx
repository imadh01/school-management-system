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
  if (!isOpen) return null;

  return (
    <div className="modal-backdrop">
      <div className="modal" style={{ maxWidth: 420 }}>
        <div className="modal__header">
          <h2>
            {danger ? "🗑 " : "❓ "}
            {title}
          </h2>
          <button className="modal__close" onClick={onCancel}>
            ✕
          </button>
        </div>
        <div className="modal__body" style={{ padding: "20px" }}>
          <p
            style={{
              fontSize: 13.5,
              color: "var(--ink)",
              lineHeight: 1.5,
              margin: 0,
            }}
          >
            {message}
          </p>
        </div>
        <div className="modal__footer">
          <button
            className={danger ? "btn btn--danger" : "btn btn--primary"}
            onClick={onConfirm}
          >
            {confirmLabel}
          </button>
          <button className="btn btn--secondary" onClick={onCancel}>
            {cancelLabel}
          </button>
        </div>
      </div>
    </div>
  );
}
