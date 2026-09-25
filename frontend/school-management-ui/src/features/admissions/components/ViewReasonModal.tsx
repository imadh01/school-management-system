interface Props {
  reason: string | null;
  onClose: () => void;
}

export function ViewReasonModal({ reason, onClose }: Props) {
  if (reason === null) return null;

  return (
    <div className="modal-backdrop">
      <div className="modal" style={{ maxWidth: 420 }}>
        <div className="modal__header">
          <h2>⚠ Rejection Reason</h2>
          <button className="modal__close" onClick={onClose}>
            ✕
          </button>
        </div>
        <div
          className="modal__body"
          style={{ textAlign: "center", padding: "28px 20px" }}
        >
          <p style={{ fontSize: 14, color: "var(--ink)" }}>
            {reason || "No reason recorded."}
          </p>
        </div>
        <div className="modal__footer" style={{ justifyContent: "center" }}>
          <button className="btn btn--primary" onClick={onClose}>
            OK
          </button>
        </div>
      </div>
    </div>
  );
}
