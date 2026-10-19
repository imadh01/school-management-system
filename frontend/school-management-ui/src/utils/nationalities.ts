/**
 * Nationalities offered in the student form. India and UAE come first because they are the
 * school's main intake; the rest are alphabetical. The value saved is the label itself.
 * A student whose saved nationality is not in this list keeps it: see withCurrent().
 */
export const NATIONALITIES: readonly string[] = [
  "Indian",
  "Emirati",
  "Afghan",
  "American",
  "Bangladeshi",
  "British",
  "Canadian",
  "Chinese",
  "Egyptian",
  "Filipino",
  "French",
  "German",
  "Indonesian",
  "Iranian",
  "Iraqi",
  "Jordanian",
  "Kenyan",
  "Lebanese",
  "Malaysian",
  "Nepalese",
  "Nigerian",
  "Omani",
  "Pakistani",
  "Palestinian",
  "Saudi",
  "South African",
  "Sri Lankan",
  "Sudanese",
  "Syrian",
  "Yemeni",
  "Other",
];

/** The list, plus the stored value when it is not one of them (so editing never silently drops it). */
export function withCurrent(
  options: readonly string[],
  current: string | null | undefined,
): readonly string[] {
  return current && !options.includes(current) ? [current, ...options] : options;
}
