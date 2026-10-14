import { apiClient } from "./apiClient";

/** What every signed-in user may know about the school (GET /api/settings/basics). */
export interface SchoolBasics {
  schoolName: string;
  shortName: string | null;
  countryId: number | null;
  /**
   * F2: the date in the school's time zone (yyyy-MM-dd) at the moment of the request. The cached copy of
   * the basics goes stale after midnight: call `schoolService.getToday()` when you need today.
   */
  today: string;
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

  /**
   * F2: today's date in the SCHOOL's time zone, fresh from the server (never cached). Attendance uses it so
   * the page and the API agree on "today" even when the browser is in another time zone.
   */
  getToday(): Promise<string> {
    return apiClient
      .get<SchoolBasics>("/settings/basics")
      .then((res) => res.data.today);
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
