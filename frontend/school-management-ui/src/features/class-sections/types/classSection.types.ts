export interface ClassSectionResponse {
  id: number;
  name: string;
  section: string;
  grade: number | null;
  capacity: number | null;
  room: string | null;
  status: string;
  displayName: string;
  academicYearId: number;
  academicYearName: string;
}
