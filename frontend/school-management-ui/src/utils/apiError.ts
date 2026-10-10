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
