/**
 * Display formatting shared by every page. Dates from the API are calendar dates ("2015-03-09"),
 * not instants, so they are read as local dates and never shifted by the time zone.
 */

/** "2015-03-09" → "9 Mar 2015". Null/blank → null (so KeyValueGrid shows a dash). */
export function formatDate(iso: string | null | undefined): string | null {
  if (!iso) return null;
  const d = new Date(`${iso}T00:00:00`);
  if (Number.isNaN(d.getTime())) return iso;
  return d.toLocaleDateString("en-GB", {
    day: "numeric",
    month: "short",
    year: "numeric",
  });
}

/** Age in whole years on a given day (default today), or null for an unreadable date. */
export function ageOn(iso: string, today = new Date()): number | null {
  const d = new Date(`${iso}T00:00:00`);
  if (Number.isNaN(d.getTime())) return null;
  let age = today.getFullYear() - d.getFullYear();
  const beforeBirthday =
    today.getMonth() < d.getMonth() ||
    (today.getMonth() === d.getMonth() && today.getDate() < d.getDate());
  if (beforeBirthday) age -= 1;
  return age;
}

/**
 * A moment from the API ("2026-10-13T09:00:00", UTC without a "Z") as a local calendar date:
 * "13 Oct 2026". Unlike formatDate, this IS a point in time, so it is converted to local time.
 */
export function formatUtcDate(iso: string | null | undefined): string | null {
  if (!iso) return null;
  const hasZone = /Z$|[+-]\d{2}:\d{2}$/.test(iso);
  const d = new Date(hasZone ? iso : `${iso}Z`);
  if (Number.isNaN(d.getTime())) return iso;
  return d.toLocaleDateString("en-GB", {
    day: "numeric",
    month: "short",
    year: "numeric",
  });
}

/** A moment from the API as local date and time: "13 Oct 2026, 09:05". Null/blank → null. */
export function formatUtcDateTime(iso: string | null | undefined): string | null {
  if (!iso) return null;
  const hasZone = /Z$|[+-]\d{2}:\d{2}$/.test(iso);
  const d = new Date(hasZone ? iso : `${iso}Z`);
  if (Number.isNaN(d.getTime())) return iso;
  return d.toLocaleString("en-GB", {
    day: "numeric",
    month: "short",
    year: "numeric",
    hour: "2-digit",
    minute: "2-digit",
  });
}
