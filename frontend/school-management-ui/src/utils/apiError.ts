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
