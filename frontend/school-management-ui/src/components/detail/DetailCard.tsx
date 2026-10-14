import { useId, type ReactNode } from "react";

/** U2: one titled card on a detail page ("Personal", "Contact & address"). */
export function DetailCard({
  title,
  children,
  wide = false,
}: {
  title: string;
  children: ReactNode;
  /** Span the whole row of cards (for a table that needs the width). */
  wide?: boolean;
}) {
  const id = useId();
  return (
    <section className={wide ? "detail-card detail-card--wide" : "detail-card"} aria-labelledby={id}>
      <h2 id={id}>{title}</h2>
      {children}
    </section>
  );
}
