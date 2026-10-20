import type { BadgeTone } from "@/components/Badge";

/** Life of an academic year: Upcoming, then Current, then Ended (another year took over), then Closed. */
export const YEAR_STATUS_TONE: Readonly<Record<string, BadgeTone>> = {
  Upcoming: "info",
  Current: "ok",
  Ended: "warn",
  Closed: "neutral",
};

/** One line per status, shown in the page's help note. */
export const YEAR_STATUS_HELP: readonly { status: string; text: string }[] = [
  { status: "Upcoming", text: "Being set up. Classes and periods can be added." },
  { status: "Current", text: "The working year. Teachers see its classes. Only one at a time." },
  { status: "Ended", text: "Another year took over. Nothing is deleted; it stays as it was." },
  { status: "Closed", text: "Read-only history. This cannot be undone." },
];

/** "2026-06-01" shown as "1 Jun 2026". */
export function formatYearDate(value: string): string {
  const [y, m, d] = value.split("-").map(Number);
  return new Date(y, m - 1, d).toLocaleDateString("en-GB", {
    day: "numeric",
    month: "short",
    year: "numeric",
  });
}
