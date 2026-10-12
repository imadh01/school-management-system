import type { LucideIcon } from "lucide-react";
import { Inbox } from "lucide-react";
import type { ReactNode } from "react";

interface Props {
  title: string;
  message?: ReactNode;
  icon?: LucideIcon;
  action?: ReactNode;
}

export function EmptyState({
  title,
  message,
  icon: Icon = Inbox,
  action,
}: Props) {
  return (
    <div className="empty-state">
      <div className="empty-state__icon">
        <Icon size={24} aria-hidden="true" />
      </div>
      <div className="empty-state__title">{title}</div>
      {message && <div>{message}</div>}
      {action && <div style={{ marginTop: 14 }}>{action}</div>}
    </div>
  );
}
