export interface DetailRow {
  label: string;
  value: React.ReactNode;
}

export interface DetailSection {
  heading?: string;
  rows: DetailRow[];
}

interface Props {
  isOpen: boolean;
  title: string;
  subtitle?: string;
  sections: DetailSection[];
  onClose: () => void;
}

export function ViewDetailsModal({
  isOpen,
  title,
  subtitle,
  sections,
  onClose,
}: Props) {
  if (!isOpen) return null;

  return (
    <div className="modal-backdrop">
      <div className="modal modal--wide">
        <div className="modal__header">
          <h2>👁 {title}</h2>
          <button className="modal__close" onClick={onClose}>
            ✕
          </button>
        </div>
        <div className="modal__body">
          {subtitle && (
            <p
              style={{
                color: "var(--muted)",
                fontSize: 12.5,
                margin: "0 0 14px",
              }}
            >
              {subtitle}
            </p>
          )}
          {sections.map((section, i) => (
            <div key={i}>
              {section.heading && (
                <div className="modal__section-title">{section.heading}</div>
              )}
              <div
                style={{
                  display: "grid",
                  gridTemplateColumns: "1fr 1fr",
                  gap: "10px 20px",
                  marginBottom: 8,
                }}
              >
                {section.rows.map((row, j) => (
                  <div key={j}>
                    <div
                      style={{
                        fontSize: 11,
                        color: "var(--muted)",
                        textTransform: "uppercase",
                        letterSpacing: "0.03em",
                      }}
                    >
                      {row.label}
                    </div>
                    <div
                      style={{
                        fontSize: 13.5,
                        color: "var(--ink)",
                        marginTop: 2,
                      }}
                    >
                      {row.value ?? (
                        <span style={{ color: "var(--muted)" }}>—</span>
                      )}
                    </div>
                  </div>
                ))}
              </div>
            </div>
          ))}
        </div>
        <div className="modal__footer" style={{ justifyContent: "flex-end" }}>
          <button className="btn btn--secondary" onClick={onClose}>
            Close
          </button>
        </div>
      </div>
    </div>
  );
}
