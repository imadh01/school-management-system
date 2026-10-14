import { useEffect, useState } from "react";
import { schoolService, type SchoolBasics } from "@/services/schoolService";

/**
 * The school's name and country, or null until loaded (or if loading failed: nothing depends on
 * it, the top bar just shows no name and address forms have no suggested country).
 */
export function useSchoolBasics(): SchoolBasics | null {
  const [basics, setBasics] = useState<SchoolBasics | null>(null);

  useEffect(() => {
    let alive = true;
    const load = () =>
      schoolService
        .getBasics()
        .then((b) => {
          if (alive) setBasics(b);
        })
        .catch(() => undefined);
    void load();
    const stop = schoolService.onChange(() => void load());
    return () => {
      alive = false;
      stop();
    };
  }, []);

  return basics;
}
