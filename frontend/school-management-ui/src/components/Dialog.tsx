import { useEffect, useId, useRef, type ReactNode } from "react";
import type { LucideIcon } from "lucide-react";
import { CircleAlert, X } from "lucide-react";

interface Props {
  title: string;
  icon?: LucideIcon;
  /** One line under the title, e.g. the applicant's name and reg no. */
  subtitle?: ReactNode;
  wide?: boolean;
  /** Problems to show at the top of the body (instead of a second pop-up). */
  errors?: string[];
  onClose: () => void;
  /** Close on Escape / the X. False while saving, so a request is not abandoned half-way. */
  closable?: boolean;
  footer: ReactNode;
  children: ReactNode;
}

/**
 * U2: the pop-up for small decisions (confirm, reject, enrol). Accessible dialog: labelled by its
 * title, Escape closes it, focus moves into it when it opens and back when it closes.
 */
export function Dialog({
  title,
  icon: Icon,
  subtitle,
  wide,
  errors = [],
  onClose,
  closable = true,
  footer,
  children,
}: Props) {
  const titleId = useId();
  const panelRef = useRef<HTMLDivElement>(null);

  useEffect(() => {
    const previous = document.activeElement as HTMLElement | null;
    panelRef.current?.focus();
    return () => previous?.focus?.();
  }, []);

  useEffect(() => {
    if (!closable) return;
    const onKey = (e: KeyboardEvent) => {
      if (e.key === "Escape") onClose();
    };
    window.addEventListener("keydown", onKey);
    return () => window.removeEventListener("keydown", onKey);
  }, [closable, onClose]);

  return (
    <div className="modal-backdrop open">
      <div
        ref={panelRef}
        className={wide ? "modal modal--wide" : "modal"}
        role="dialog"
        aria-modal="true"
        aria-labelledby={titleId}
        tabIndex={-1}
      >
        <div className="modal__header">
          <h2 id={titleId}>
            {Icon && <Icon size={20} aria-hidden="true" />}
            {title}
          </h2>
          <button
            type="button"
            className="modal__close"
            aria-label="Close"
            onClick={onClose}
            disabled={!closable}
          >
            <X size={18} aria-hidden="true" />
          </button>
        </div>
        <div className="modal__body">
          {subtitle && <p className="modal__subtitle">{subtitle}</p>}
          {errors.length > 0 && (
            <div
              className="form-alert"
              role="alert"
              style={{ marginBottom: 16 }}
            >
              <CircleAlert size={18} aria-hidden="true" />
              {errors.length === 1 ? (
                <span>{errors[0]}</span>
              ) : (
                <ul style={{ margin: 0 }}>
                  {errors.map((m, i) => (
                    <li key={i}>{m}</li>
                  ))}
                </ul>
              )}
            </div>
          )}
          {children}
        </div>
        <div className="modal__footer">{footer}</div>
      </div>
    </div>
  );
}
