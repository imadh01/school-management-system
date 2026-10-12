/** "jane.doe" → "JD", "Imadh" → "I". Used for avatars. */
export function initialsOf(name: string | undefined): string {
  if (!name) return "?";
  const parts = name.split(/[\s._-]+/).filter(Boolean);
  return (
    ((parts[0]?.[0] ?? "") + (parts[1]?.[0] ?? "")).toUpperCase() ||
    name[0].toUpperCase()
  );
}
