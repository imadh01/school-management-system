import axios from "axios";

interface ApiErrorBody {
  message?: string;
  errorCode?: string;
  errors?: Record<string, string[]>;
}

/** True when the API refused a save because someone else changed the record first (HTTP 409). */
export function isConcurrencyConflict(error: unknown): boolean {
  return (
    axios.isAxiosError(error) &&
    error.response?.status === 409 &&
    (error.response.data as ApiErrorBody | undefined)?.errorCode ===
      "CONCURRENCY_CONFLICT"
  );
}

/** The HTTP status of a failed API call, or undefined (network error, not an API call). */
export function getApiStatus(error: unknown): number | undefined {
  return axios.isAxiosError(error) ? error.response?.status : undefined;
}

/** F1: what a 404 on a read means since scoped access: missing, or not yours. The API cannot say which. */
export const NOT_AVAILABLE_MESSAGE =
  "This record was not found, or it is not available to you.";

/**
 * Messages for a failed READ of students, parents or attendance (F1). The API
 * answers 404 both for "does not exist" and "not in your classes/children", and
 * 403 when the role lacks the permission; both get a plain message instead of
 * the raw API text. Other errors fall back to getApiErrors.
 */
export function getReadErrors(error: unknown, fallback: string): string[] {
  const status = getApiStatus(error);
  if (status === 404) return [NOT_AVAILABLE_MESSAGE];
  if (status === 403) return ["You don't have access to this."];
  return getApiErrors(error, fallback);
}

export function getApiErrors(error: unknown, fallback: string): string[] {
  if (axios.isAxiosError(error)) {
    if (!error.response) {
      return ["Cannot reach the server. Please try again."];
    }
    const data = error.response.data as ApiErrorBody | undefined;
    if (error.response.status === 403) {
      // A policy failure has an empty body (generic message); a business
      // "Forbidden" from the API carries its own explanation (e.g. attendance scope).
      return [data?.message ?? "You do not have permission to do this."];
    }
    if (data?.errors) {
      const messages = Object.values(data.errors).flat();
      if (messages.length > 0) return messages;
    }
    if (data?.message) return [data.message];
  }
  return [fallback];
}

/**
 * U2: a failed SAVE split into errors that belong to one field (shown under that field) and
 * errors that belong to the whole form (shown in the banner at the top).
 *
 * The API's validation keys are C# property paths: "FirstName", "Health.Allergies",
 * "PickupPersons[0].Phone". They become the form's field keys: "firstName",
 * "health.allergies", "pickupPersons.0.phone". `prefix` is for requests sent from a part of
 * the form (e.g. "identity" for PUT /students/{id}/identity).
 */
export interface SaveErrors {
  fields: Record<string, string>;
  form: string[];
}

export function toFieldKey(apiKey: string, prefix?: string): string {
  const key = apiKey
    .replace(/^\$\./, "")
    .replace(/\[(\d+)\]/g, ".$1")
    .split(".")
    .filter(Boolean)
    .map((part) => part.charAt(0).toLowerCase() + part.slice(1))
    .join(".");
  return prefix ? `${prefix}.${key}` : key;
}

export function getSaveErrors(
  error: unknown,
  fallback: string,
  prefix?: string,
): SaveErrors {
  const result: SaveErrors = { fields: {}, form: [] };
  if (axios.isAxiosError(error) && error.response) {
    const data = error.response.data as ApiErrorBody | undefined;
    if (data?.errors) {
      for (const [apiKey, messages] of Object.entries(data.errors)) {
        if (messages.length === 0) continue;
        if (!apiKey) result.form.push(...messages);
        else result.fields[toFieldKey(apiKey, prefix)] = messages[0];
      }
      if (Object.keys(result.fields).length > 0 || result.form.length > 0)
        return result;
    }
  }
  result.form = getApiErrors(error, fallback);
  return result;
}
