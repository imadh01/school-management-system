import { useCallback, useEffect, useRef, useState } from "react";
import { getReadErrors } from "@/utils/apiError";

interface AsyncData<T> {
  data: T | undefined;
  /** A friendly message when loading failed (404 and 403 included, see getReadErrors). */
  error: string | null;
  isLoading: boolean;
  /** Loads again, e.g. after a save. Keeps the old data on screen while loading. */
  reload: () => Promise<void>;
  /** Replaces the data without a request (e.g. with what a save returned). */
  setData: (data: T) => void;
}

/**
 * Loads something when the page opens (and again when `deps` change).
 *
 *   const { data: parent, error, isLoading, reload } =
 *     useAsyncData(() => parentService.getById(id), [id], "Could not load this parent.");
 *
 * A late answer for an old `deps` value is ignored, so fast navigation never shows the wrong record.
 */
export function useAsyncData<T>(
  load: () => Promise<T>,
  deps: readonly unknown[],
  fallbackMessage: string,
): AsyncData<T> {
  const [data, setData] = useState<T | undefined>(undefined);
  const [error, setError] = useState<string | null>(null);
  const [isLoading, setIsLoading] = useState(true);
  const loadRef = useRef(load);
  loadRef.current = load;
  const requestId = useRef(0);

  const reload = useCallback(async () => {
    const id = ++requestId.current;
    setIsLoading(true);
    try {
      const result = await loadRef.current();
      if (id === requestId.current) {
        setData(result);
        setError(null);
      }
    } catch (err) {
      if (id === requestId.current)
        setError(getReadErrors(err, fallbackMessage)[0]);
    } finally {
      if (id === requestId.current) setIsLoading(false);
    }
    // eslint-disable-next-line react-hooks/exhaustive-deps -- `deps` is the caller's list
  }, deps);

  useEffect(() => {
    void reload();
  }, [reload]);

  return { data, error, isLoading, reload, setData };
}
