import type { LucideIcon } from "lucide-react";
import type { ReactNode } from "react";

interface Props {
  title: string;
  subtitle?: ReactNode;
  icon?: LucideIcon;
  actions?: ReactNode;
  children: ReactNode;
  /** Wrap the children in the standard padding (off for tables that run edge to edge). */
  padded?: boolean;
}

/** U1: a card with an icon tile, a title, a count line and actions, e.g. "All students · 6 shown". */
export function Panel({
  title,
  subtitle,
  icon: Icon,
  actions,
  children,
  padded = false,
}: Props) {
  return (
    <section className="panel" aria-label={title}>
      <div className="panel__head">
        {Icon && (
          <div className="panel__tile">
            <Icon size={20} aria-hidden="true" />
          </div>
        )}
        <div>
          <h2 className="panel__title">{title}</h2>
          {subtitle && <div className="panel__subtitle">{subtitle}</div>}
        </div>
        {actions && <div className="panel__actions">{actions}</div>}
      </div>
      {padded ? <div className="panel__body">{children}</div> : children}
    </section>
  );
}
