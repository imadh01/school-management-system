/**
 * Small helpers for turning what a form holds into what the API expects, and for placing the
 * API's errors on the right fields and sections. Used by every add/edit page.
 */

/** Blank strings become null, so optional API fields stay clean. Other values are kept. */
export function blankToNull<T extends object>(obj: T): T {
  return Object.fromEntries(
    Object.entries(obj).map(([k, v]) => [
      k,
      typeof v === "string" && v.trim() === "" ? null : v,
    ]),
  ) as T;
}

/** "" → null, otherwise the number (NaN when not a number, so validation can catch it). */
export function numberOrNull(value: string): number | null {
  return value.trim() === "" ? null : Number(value);
}

/** One section of a full-page form and the field keys that live in it. */
export interface FormSectionDef {
  id: string;
  title: string;
  /** Field keys as the API names them in camelCase; a key also matches its children ("guardians" → "guardians.0.name"). */
  fields: readonly string[];
}

/** The section a field key belongs to, or undefined (then its message goes to the banner at the top). */
export function sectionOfField(
  sections: readonly FormSectionDef[],
  key: string,
): string | undefined {
  return sections.find((s) =>
    s.fields.some((f) => key === f || key.startsWith(f + ".")),
  )?.id;
}

/**
 * Rows of repeating fields (guardians, pickup persons): blank rows are not sent, so the API's
 * "Guardians[1]" is the second row SENT, not the second on screen. This maps the API's index back
 * to the row the user sees. `prefix` is the field key of the list ("guardians").
 */
export function remapRowErrors(
  fields: Record<string, string>,
  prefix: string,
  sentIndexes: number[],
): Record<string, string> {
  const pattern = new RegExp(`^${prefix}\\.(\\d+)(.*)$`);
  return Object.fromEntries(
    Object.entries(fields).map(([key, message]) => {
      const m = pattern.exec(key);
      if (!m) return [key, message];
      const onScreen = sentIndexes[Number(m[1])] ?? Number(m[1]);
      return [`${prefix}.${onScreen}${m[2]}`, message];
    }),
  );
}

/**
 * Connects a form object to the input components:
 *
 *   const bind = fieldBinder(form, patch, fieldErrors, "parent");
 *   <TextField {...bind("name")} label="Full name" required />
 *
 * gives id "parent-name", the value, an onChange that patches `name`, and the error for "name".
 * `errorPrefix` is for nested parts of a form ("health." → errors on "health.allergies").
 */
export function fieldBinder<T extends object>(
  values: T,
  patch: (changes: Partial<T>) => void,
  errors: Record<string, string>,
  idPrefix: string,
  errorPrefix = "",
) {
  return <K extends keyof T & string>(key: K) => ({
    id: `${idPrefix}-${errorPrefix.replace(/\./g, "-")}${key}`,
    value: values[key] as unknown as string | number | null | undefined,
    onChange: (value: string) =>
      patch({ [key]: value } as unknown as Partial<T>),
    error: errors[errorPrefix + key],
  });
}
