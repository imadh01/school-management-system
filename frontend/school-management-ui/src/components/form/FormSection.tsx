import type { ReactNode } from "react";

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
  return (
    <section
      id={id}
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
