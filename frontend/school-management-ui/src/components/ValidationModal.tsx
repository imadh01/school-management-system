interface Props {
  errors: string[] | null;
  onClose: () => void;
}

export function ValidationModal({ errors, onClose }: Props) {
  if (!errors || errors.length === 0) return null;

  return (
    <div className="modal-backdrop">
      <div className="modal" style={{ maxWidth: 440 }}>
        <div className="modal__header">
          <h2>⚠ Please fix the following</h2>
          <button className="modal__close" onClick={onClose}>
            ✕
          </button>
        </div>
        <div className="modal__body" style={{ padding: "18px 20px" }}>
          {errors.length === 1 ? (
            <p style={{ fontSize: 13.5, color: "var(--ink)", margin: 0 }}>
              {errors[0]}
            </p>
          ) : (
            <ul
              style={{
                margin: 0,
                paddingLeft: 18,
                fontSize: 13.5,
                color: "var(--ink)",
                lineHeight: 1.7,
              }}
            >
              {errors.map((e, i) => (
                <li key={i}>{e}</li>
              ))}
            </ul>
          )}
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
