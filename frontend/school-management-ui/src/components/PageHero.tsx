import type { ReactNode } from "react";

interface Props {
  title: string;
  subtitle?: string;
  /** Buttons on the right, e.g. "Add student". */
  actions?: ReactNode;
  /** Stat cards that overlap the bottom of the banner (use <StatGrid>). */
  stats?: ReactNode;
}

/** U1: the gradient banner at the top of every module page, with optional overlapping stat cards. */
export function PageHero({ title, subtitle, actions, stats }: Props) {
  return (
    <>
      <section className={`hero${stats ? " hero--with-stats" : ""}`}>
        <div>
          <h1 className="hero__title">{title}</h1>
          {subtitle && <p className="hero__subtitle">{subtitle}</p>}
        </div>
        {actions && <div className="hero__actions">{actions}</div>}
      </section>
      {stats}
    </>
  );
}
