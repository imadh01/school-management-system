export interface ClassSectionResponse {
  id: number;
  name: string;
  section: string;
  code: string;
  grade: number | null;
  stage: string;
  medium: string;
  stream: string;
  capacity: number | null;
  enrolled: number;
  building: string | null;
  floor: number | null;
  room: string | null;
  status: "Active" | "Inactive" | string;
  displayName: string;
  academicYearId: number;
  academicYearName: string;
  classTeacherId: number | null;
  classTeacherName: string | null;
  /** Version stamp. Send it back unchanged when saving, so a concurrent edit is detected (409). */
  rowVersion: string;
}

// Fields shared by create and update (what the form edits).
export interface ClassSectionFields {
  name: string;
  section: string;
  grade: number | null;
  stage: string;
  medium: string;
  stream: string;
  capacity: number;
  building: string | null;
  floor: number | null;
  room: string | null;
  isActive: boolean;
}

export interface UpdateClassSectionRequest extends ClassSectionFields {
  rowVersion: string;
}

export interface CreateClassSectionRequest extends ClassSectionFields {
  // null/undefined = backend uses the current academic year.
  academicYearId?: number | null;
}

// Must match ClassSectionOptions.cs on the backend.
export const CLASS_STAGES = ["Pre-Primary", "Primary", "Secondary"] as const;
export const CLASS_MEDIUMS = ["English", "Hindi", "Arabic"] as const;
export const CLASS_STREAMS = ["General", "Science", "Commerce"] as const;
