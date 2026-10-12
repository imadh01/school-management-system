import type { ReactNode } from "react";

export type BadgeTone = "ok" | "warn" | "bad" | "info" | "neutral" | "purple";

/** A pill with a dot. The dot is decoration: the text always says what the status is. */
export function Badge({
  tone = "neutral",
  children,
}: {
  tone?: BadgeTone;
  children: ReactNode;
}) {
  return <span className={`badge badge--${tone}`}>{children}</span>;
}

/** The usual record statuses mapped to tones, so every page colours them the same way. */
export function statusTone(status: string | null | undefined): BadgeTone {
  switch ((status ?? "").toLowerCase()) {
    case "active":
    case "enrolled":
    case "verified":
    case "present":
      return "ok";
    case "pending":
    case "admitted":
    case "late":
    case "suspended":
      return "warn";
    case "rejected":
    case "absent":
    case "locked":
      return "bad";
    case "left":
    case "registered":
      return "info";
    default:
      return "neutral";
  }
}

export function StatusBadge({ status }: { status: string }) {
  return <Badge tone={statusTone(status)}>{status}</Badge>;
}
