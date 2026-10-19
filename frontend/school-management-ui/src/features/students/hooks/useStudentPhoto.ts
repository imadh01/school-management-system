import { useEffect, useState } from "react";
import { studentService } from "../services/studentService";

/**
 * The student's photo as a URL an <img> can show, or null (no photo, still loading, or not allowed).
 * The picture is fetched through the logged-in API connection and released when it changes or the
 * page closes. `version` changes after an upload or a removal to load the new picture.
 */
export function useStudentPhoto(
  studentId: number,
  hasPhoto: boolean,
  version = 0,
): string | null {
  const [src, setSrc] = useState<string | null>(null);

  useEffect(() => {
    if (!hasPhoto) {
      setSrc(null);
      return;
    }
    let cancelled = false;
    let objectUrl: string | null = null;
    studentService
      .getPhoto(studentId)
      .then((blob) => {
        if (cancelled) return;
        objectUrl = URL.createObjectURL(blob);
        setSrc(objectUrl);
      })
      .catch(() => {
        if (!cancelled) setSrc(null); // fall back to the initials
      });
    return () => {
      cancelled = true;
      if (objectUrl) URL.revokeObjectURL(objectUrl);
    };
  }, [studentId, hasPhoto, version]);

  return src;
}
