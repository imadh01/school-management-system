import { useId, type ReactNode } from "react";

/** U2: one titled card on a detail page ("Personal", "Contact & address"). */
export function DetailCard({
  title,
  children,
}: {
  title: string;
  children: ReactNode;
}) {
  const id = useId();
  return (
    <section className="detail-card" aria-labelledby={id}>
      <h2 id={id}>{title}</h2>
      {children}
    </section>
  );
}
