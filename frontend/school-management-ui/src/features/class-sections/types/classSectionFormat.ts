import type { ClassSectionResponse } from "./classSection.types";

/** "Main block · Floor 2 · Room 204", or null when no location is recorded. */
export function classLocation(
  c: Pick<ClassSectionResponse, "building" | "floor" | "room">,
): string | null {
  const parts = [
    c.building,
    c.floor !== null ? `Floor ${c.floor}` : null,
    c.room ? `Room ${c.room}` : null,
  ].filter(Boolean);
  return parts.length ? parts.join(" · ") : null;
}

/**
 * The code the API will give a class ("Class 5" + "A" → "C5A"). A preview only: it mirrors
 * ClassSection.Code on the backend, which stays the source of truth.
 */
export function previewClassCode(name: string, section: string): string {
  if (!name.trim() || !section.trim()) return "";
  return (
    name.trim().replace("Class ", "C").replace(/ /g, "") + section.trim()
  ).toUpperCase();
}
