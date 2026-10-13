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
