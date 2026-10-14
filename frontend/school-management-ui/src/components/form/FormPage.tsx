import {
  useEffect,
  useRef,
  useState,
  type FormEvent,
  type ReactNode,
} from "react";
import { Link } from "react-router-dom";
import { ArrowLeft, CircleAlert } from "lucide-react";

export interface FormSectionLink {
  id: string;
  title: string;
  /** Number of fields in this section that have an error. */
  errorCount?: number;
}

interface Props {
  title: string;
  subtitle?: ReactNode;
  back: { to: string; label: string };
  sections: FormSectionLink[];
  /** Problems that belong to the whole form (not one field), shown above the sections. */
  formErrors?: string[];
  /** Extra content for the error banner, e.g. a "Reload" button after a 409. */
  formErrorAction?: ReactNode;
  onSubmit: () => void;
  isSaving: boolean;
  isDirty: boolean;
  /** Shown in the save bar as "N changes" when the page can count them (the roles matrix). */
  changeCount?: number;
  saveLabel?: string;
  onCancel: () => void;
  children: ReactNode;
}

/**
 * U2: the full-page add/edit layout.
 *   - left: the section list (stays in view; jumps to a section; marks sections with errors);
 *   - right: the section cards;
 *   - bottom: the save bar, always visible.
 * On narrow screens the section list becomes a scrollable row above the form.
 */
export function FormPage({
  title,
  subtitle,
  back,
  sections,
  formErrors = [],
  formErrorAction,
  onSubmit,
  isSaving,
  isDirty,
  changeCount,
  saveLabel = "Save changes",
  onCancel,
  children,
}: Props) {
  const [current, setCurrent] = useState(sections[0]?.id);

  // Highlight the section being read: the last one whose top has scrolled past the sticky top bar.
  useEffect(() => {
    let frame = 0;
    const onScroll = () => {
      cancelAnimationFrame(frame);
      frame = requestAnimationFrame(() => {
        let active = sections[0]?.id;
        for (const s of sections) {
          const el = document.getElementById(s.id);
          if (el && el.getBoundingClientRect().top <= 160) active = s.id;
        }
        // At the very bottom the last sections may never reach the top: pick the last one.
        if (
          window.innerHeight + window.scrollY >=
          document.documentElement.scrollHeight - 4
        )
          active = sections[sections.length - 1]?.id;
        setCurrent(active);
      });
    };
    window.addEventListener("scroll", onScroll, { passive: true });
    return () => {
      window.removeEventListener("scroll", onScroll);
      cancelAnimationFrame(frame);
    };
  }, [sections]);

  // When a save fails, bring the error summary into view (it is also announced: role="alert").
  const alertRef = useRef<HTMLDivElement>(null);
  const totalErrors = sections.reduce((n, s) => n + (s.errorCount ?? 0), 0);
  const hasErrors = totalErrors > 0 || formErrors.length > 0;
  // Changes whenever a new set of errors arrives, so a second failed save scrolls again.
  const errorSignature = `${totalErrors}|${formErrors.join("|")}`;
  useEffect(() => {
    if (!hasErrors) return;
    alertRef.current?.scrollIntoView({ behavior: "smooth", block: "center" });
    // eslint-disable-next-line react-hooks/exhaustive-deps -- hasErrors follows errorSignature
  }, [errorSignature]);

  const jumpTo = (id: string) => {
    const el = document.getElementById(id);
    if (!el) return;
    el.scrollIntoView({ behavior: "smooth", block: "start" });
    el.focus({ preventScroll: true }); // keyboard and screen-reader users land in the section
    setCurrent(id);
  };

  const handleSubmit = (e: FormEvent) => {
    e.preventDefault();
    onSubmit();
  };

  return (
    <div className="form-page">
      <div className="form-page__head">
        <Link to={back.to} className="back-link">
          <ArrowLeft size={16} aria-hidden="true" />
          {back.label}
        </Link>
        <h1>{title}</h1>
        {subtitle && <p className="form-page__subtitle">{subtitle}</p>}
      </div>

      <div className="form-layout">
        <nav className="form-nav" aria-label="Form sections">
          <ol>
            {sections.map((s) => {
              const errors = s.errorCount ?? 0;
              return (
                <li key={s.id}>
                  <a
                    href={`#${s.id}`}
                    aria-current={current === s.id ? "location" : undefined}
                    className={errors > 0 ? "has-errors" : undefined}
                    onClick={(e) => {
                      e.preventDefault();
                      jumpTo(s.id);
                    }}
                  >
                    <span className="form-nav__label">{s.title}</span>
                    {errors > 0 && (
                      <span className="form-nav__errors">
                        <span aria-hidden="true">{errors}</span>
                        <span className="visually-hidden">
                          {` — ${errors} ${errors === 1 ? "error" : "errors"}`}
                        </span>
                      </span>
                    )}
                  </a>
                </li>
              );
            })}
          </ol>
        </nav>

        <form className="form-main" onSubmit={handleSubmit} noValidate>
          {(formErrors.length > 0 || totalErrors > 0) && (
            <div className="form-alert" role="alert" ref={alertRef}>
              <CircleAlert size={18} aria-hidden="true" />
              <div>
                <strong>
                  {totalErrors > 0
                    ? `Please fix ${totalErrors} ${totalErrors === 1 ? "field" : "fields"} before saving.`
                    : "This could not be saved."}
                </strong>
                {formErrors.length > 0 && (
                  <ul>
                    {formErrors.map((m, i) => (
                      <li key={i}>{m}</li>
                    ))}
                  </ul>
                )}
                {formErrorAction}
              </div>
            </div>
          )}

          {children}

          <div className="save-bar">
            <span className="save-bar__status" aria-live="polite">
              {isSaving
                ? "Saving…"
                : isDirty
                  ? changeCount
                  ? `You have unsaved changes (${changeCount} ${changeCount === 1 ? "change" : "changes"})`
                  : "You have unsaved changes"
                  : "No changes yet"}
            </span>
            <div className="save-bar__actions">
              <button
                type="button"
                className="btn btn--secondary"
                onClick={onCancel}
                disabled={isSaving}
              >
                Cancel
              </button>
              <button
                type="submit"
                className="btn btn--primary"
                disabled={isSaving}
              >
                {isSaving ? "Saving…" : saveLabel}
              </button>
            </div>
          </div>
        </form>
      </div>
    </div>
  );
}
