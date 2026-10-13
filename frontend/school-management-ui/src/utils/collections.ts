/** List helpers used by the list pages (filter drop-downs, status counts). */

/** The distinct, non-empty values of a list, sorted. Used to fill filter drop-downs. */
export function uniqueSorted(values: (string | null | undefined)[]): string[] {
  return [...new Set(values.filter((v): v is string => !!v))].sort();
}

/** How many items have each value of `key`, e.g. { Active: 10, Inactive: 2 }. */
export function countBy<T>(
  items: readonly T[],
  key: (item: T) => string | null | undefined,
): Record<string, number> {
  const counts: Record<string, number> = {};
  for (const item of items) {
    const k = key(item);
    if (k) counts[k] = (counts[k] ?? 0) + 1;
  }
  return counts;
}
