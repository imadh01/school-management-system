import { useId } from "react";
import { TriangleAlert, X } from "lucide-react";

interface Props {
  errors: string[] | null;
  onClose: () => void;
}

export function ValidationModal({ errors, onClose }: Props) {
  const titleId = useId();
  if (!errors || errors.length === 0) return null;

  return (
    <div className="modal-backdrop">
      <div
        className="modal"
        style={{ maxWidth: 460 }}
        role="alertdialog"
        aria-modal="true"
        aria-labelledby={titleId}
      >
        <div className="modal__header">
          <h2 id={titleId}>
            <TriangleAlert size={18} aria-hidden="true" />
            Please fix the following
          </h2>
          <button
            type="button"
            className="modal__close"
            aria-label="Close"
            onClick={onClose}
          >
            <X size={16} aria-hidden="true" />
          </button>
        </div>
        <div className="modal__body">
          {errors.length === 1 ? (
            <p style={{ fontSize: 14, margin: 0, lineHeight: 1.6 }}>
              {errors[0]}
            </p>
          ) : (
            <ul
              style={{
                margin: 0,
                paddingLeft: 18,
                fontSize: 14,
                lineHeight: 1.7,
              }}
            >
              {errors.map((e, i) => (
                <li key={i}>{e}</li>
              ))}
            </ul>
          )}
        </div>
        <div className="modal__footer">
          <button
            type="button"
            className="btn btn--primary"
            onClick={onClose}
            autoFocus
          >
            OK
          </button>
        </div>
      </div>
    </div>
  );
}
