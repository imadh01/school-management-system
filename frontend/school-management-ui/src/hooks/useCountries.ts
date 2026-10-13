import { countryService } from "@/services/countryService";
import { useAsyncData } from "./useAsyncData";

/** The countries for address forms (loaded once and shared, see countryService). */
export function useCountries() {
  return useAsyncData(
    () => countryService.getAll(),
    [],
    "Could not load the list of countries.",
  );
}
