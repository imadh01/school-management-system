import type { CSSProperties, ReactNode } from "react";
import { Info, type LucideIcon } from "lucide-react";

/** U2: an inline note with an icon. "warn" for locks, restrictions and irreversible steps. */
export function Notice({
  children,
  icon: Icon = Info,
  tone = "info",
  style,
}: {
  children: ReactNode;
  icon?: LucideIcon;
  tone?: "info" | "warn";
  style?: CSSProperties;
}) {
  return (
    <div
      className={tone === "warn" ? "notice notice--warn" : "notice"}
      style={style}
    >
      <Icon size={16} aria-hidden="true" />
      <span>{children}</span>
    </div>
  );
}
