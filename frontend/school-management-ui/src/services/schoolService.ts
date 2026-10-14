import { apiClient } from "./apiClient";

/** What every signed-in user may know about the school (GET /api/settings/basics). */
export interface SchoolBasics {
  schoolName: string;
  shortName: string | null;
  countryId: number | null;
}

let cached: Promise<SchoolBasics> | null = null;
const listeners = new Set<() => void>();

/**
 * E3b: the school's name and country, loaded once per page load and shared (top bar, address
 * forms). Saving the school settings calls `invalidate()`, so the top bar shows the new name
 * without a reload. A failed load is not cached.
 */
export const schoolService = {
  getBasics(): Promise<SchoolBasics> {
    if (!cached) {
      cached = apiClient
        .get<SchoolBasics>("/settings/basics")
        .then((res) => res.data)
        .catch((err) => {
          cached = null;
          throw err;
        });
    }
    return cached;
  },

  /** Forget the cached values and tell every user of them to load again. */
  invalidate() {
    cached = null;
    listeners.forEach((l) => l());
  },

  /** Called after `invalidate()`; returns the way to stop listening. */
  onChange(listener: () => void): () => void {
    listeners.add(listener);
    return () => {
      listeners.delete(listener);
    };
  },
};
