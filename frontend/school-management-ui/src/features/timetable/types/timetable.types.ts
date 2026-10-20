// Matches backend DTOs/Timetable. Times travel as "HH:mm"; days as DayOfWeek numbers
// (Sunday = 0 ... Saturday = 6).

export const PERIOD_KINDS = ["Teaching", "Break"] as const;
export type PeriodKind = (typeof PERIOD_KINDS)[number];

export const DAY_NAMES = [
  "Sunday",
  "Monday",
  "Tuesday",
  "Wednesday",
  "Thursday",
  "Friday",
  "Saturday",
] as const;
export const dayName = (day: number) => DAY_NAMES[day] ?? `Day ${day}`;
export const dayShort = (day: number) => dayName(day).slice(0, 3);

export interface TimetablePeriodResponse {
  id: number;
  academicYearId: number;
  label: string;
  startTime: string;
  endTime: string;
  kind: PeriodKind | string;
  sortOrder: number;
  /** Live lessons using this period. A used period cannot be deleted. */
  cellCount: number;
  rowVersion: string;
}

export interface CreateTimetablePeriodRequest {
  academicYearId: number;
  label: string;
  startTime: string;
  endTime: string;
  kind: PeriodKind;
  sortOrder: number;
}

export interface UpdateTimetablePeriodRequest {
  label: string;
  startTime: string;
  endTime: string;
  kind: PeriodKind;
  sortOrder: number;
  rowVersion: string;
}

export interface TimetableCellResponse {
  entryId: number;
  dayOfWeek: number;
  periodId: number;
  subjectId: number;
  subjectName: string;
  subjectCode: string;
  teacherId: number | null;
  teacherName: string | null;
  /** The room typed for this lesson only. */
  room: string | null;
  /** What to show: this lesson's room, else the class's own room. */
  effectiveRoom: string | null;
}

export interface ClassTimetableResponse {
  classSectionId: number;
  className: string;
  academicYearId: number;
  classRoom: string | null;
  days: number[];
  periods: TimetablePeriodResponse[];
  cells: TimetableCellResponse[];
}

export interface TimetableCellInput {
  dayOfWeek: number;
  periodId: number;
  subjectId: number;
  room: string | null;
}

export interface SkippedTimetableCell {
  dayOfWeek: number;
  periodLabel: string;
  subjectName: string;
  reason: string;
}

export interface CopyTimetableResult {
  replaced: number;
  copied: number;
  skipped: SkippedTimetableCell[];
}

export interface TeacherTimetableCell {
  dayOfWeek: number;
  periodId: number;
  classSectionId: number;
  className: string;
  subjectId: number;
  subjectName: string;
  effectiveRoom: string | null;
}

export interface TeacherTimetableResponse {
  teacherId: number;
  teacherName: string;
  academicYearId: number;
  days: number[];
  periods: TimetablePeriodResponse[];
  cells: TeacherTimetableCell[];
}
