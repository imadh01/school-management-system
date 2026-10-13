import { apiClient } from "./apiClient";
import type { CountryResponse } from "../types/address";

let cached: Promise<CountryResponse[]> | null = null;

export const countryService = {
  /**
   * Countries change rarely (a setting), so the list is loaded once per page load and shared by
   * every address form. A failed load is not cached: the next form tries again.
   */
  getAll(): Promise<CountryResponse[]> {
    if (!cached) {
      cached = apiClient
        .get<CountryResponse[]>("/countries")
        .then((res) => res.data)
        .catch((err) => {
          cached = null;
          throw err;
        });
    }
    return cached;
  },
};
