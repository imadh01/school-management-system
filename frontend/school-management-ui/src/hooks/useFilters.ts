import { useState } from "react";

type FilterValues = Record<string, string | boolean>;
/** `false` → boolean, `""` → string, so a filter can be changed to any value of its kind. */
type Widen<T> = { [K in keyof T]: T[K] extends boolean ? boolean : string };

/**
 * The filters of a list page in one object:
 *
 *   const filters = useFilters({ status: "", search: "", allergiesOnly: false });
 *   filters.values.status · filters.set("status", "Active") · filters.clear()
 *   filters.isActive  → show "Clear"
 *   filters.key       → pass to DataTable's resetPageKey (back to page 1 when a filter changes)
 */
export function useFilters<I extends FilterValues>(initial: I) {
  type T = Widen<I>;
  const [values, setValues] = useState<T>(initial as T);
  const set = <K extends keyof T>(key: K, value: T[K]) =>
    setValues((v) => ({ ...v, [key]: value }));
  const clear = () => setValues(initial as T);
  const isActive = Object.keys(initial).some(
    (k) => values[k as keyof T] !== initial[k],
  );
  const key = JSON.stringify(values);
  return { values, set, clear, isActive, key };
}
