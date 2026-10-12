import { Funnel } from "lucide-react";
import type { ReactNode } from "react";

/** U1: the shaded row of filters between a panel's header and its table. */
export function FilterBar({ children }: { children: ReactNode }) {
  return (
    <div className="filter-bar" role="group" aria-label="Filters">
      <Funnel className="filter-bar__icon" size={18} aria-hidden="true" />
      {children}
    </div>
  );
}
