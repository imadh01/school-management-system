interface Props {
  title: string;
  icon: string;
  requiredModule: string;
  isOpen: boolean;
  onClose: () => void;
}

export function NotYetAvailableModal({
  title,
  icon,
  requiredModule,
  isOpen,
  onClose,
}: Props) {
  if (!isOpen) return null;

  return (
    <div className="modal-backdrop">
      <div className="modal modal--wide">
        <div className="modal__header">
          <h2>
            {icon} {title}
          </h2>
          <button className="modal__close" onClick={onClose}>
            ✕
          </button>
        </div>
        <div className="modal__body">
          <div className="empty-state">
            {title} isn't available yet — it depends on the {requiredModule}{" "}
            module, which hasn't been built.
            <br />
            This will show real data once that module exists, not placeholder
            numbers.
          </div>
        </div>
        <div className="modal__footer">
          <button className="btn btn--secondary" onClick={onClose}>
            Close
          </button>
        </div>
      </div>
    </div>
  );
}
