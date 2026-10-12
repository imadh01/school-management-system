import type { LucideIcon } from "lucide-react";
import type { ReactNode } from "react";

export type Tone =
  "ok" | "warn" | "bad" | "info" | "neutral" | "purple" | "accent";

interface StatCardProps {
  label: string;
  value: ReactNode;
  icon: LucideIcon;
  tone?: Tone;
}

/** One number in the row under a PageHero. */
export function StatCard({
  label,
  value,
  icon: Icon,
  tone = "info",
}: StatCardProps) {
  return (
    <div className="stat">
      <div className={`stat__icon tone-${tone}`}>
        <Icon size={20} aria-hidden="true" />
      </div>
      <div className="stat__value">{value}</div>
      <div className="stat__label">{label}</div>
    </div>
  );
}

export function StatGrid({ children }: { children: ReactNode }) {
  return <div className="stat-grid">{children}</div>;
}
