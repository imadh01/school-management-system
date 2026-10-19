import { createContext, useContext, type ReactNode } from "react";

/**
 * The section tab that is open. FormPage provides it; a FormSection that is not the open one is
 * hidden (still mounted, so what was typed is kept). null = no tabs around it: always shown.
 */
export const ActiveFormSectionContext = createContext<string | null>(null);

interface Props {
  /** Anchor id: the section list on the left links to it. */
  id: string;
  title: string;
  description?: ReactNode;
  /** false for sections whose content is not a grid of fields (lists, notes). */
  grid?: boolean;
  children: ReactNode;
}

/** U2: one card of a full-page form. */
export function FormSection({
  id,
  title,
  description,
  grid = true,
  children,
}: Props) {
  const activeId = useContext(ActiveFormSectionContext);
  return (
    <section
      id={id}
      hidden={activeId !== null && activeId !== id}
      className="form-section"
      aria-labelledby={`${id}-title`}
      tabIndex={-1}
    >
      <header className="form-section__head">
        <h2 id={`${id}-title`}>{title}</h2>
        {description && <p>{description}</p>}
      </header>
      {grid ? <div className="form-grid">{children}</div> : children}
    </section>
  );
}
