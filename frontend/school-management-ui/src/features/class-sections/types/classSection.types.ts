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
}

// Fields shared by create and update.
export interface UpdateClassSectionRequest {
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

export interface CreateClassSectionRequest extends UpdateClassSectionRequest {
  // null/undefined = backend uses the current academic year.
  academicYearId?: number | null;
}

// Must match ClassSectionOptions.cs on the backend.
export const CLASS_STAGES = ["Pre-Primary", "Primary", "Secondary"] as const;
export const CLASS_MEDIUMS = ["English", "Hindi", "Arabic"] as const;
export const CLASS_STREAMS = ["General", "Science", "Commerce"] as const;
