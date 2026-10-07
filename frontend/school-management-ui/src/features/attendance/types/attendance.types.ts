export const ATTENDANCE_STATUSES = [
  "Present",
  "Absent",
  "Late",
  "Half Day",
  "Leave",
] as const;

export type AttendanceStatus = (typeof ATTENDANCE_STATUSES)[number];

export const STATUS_COLOR: Record<
  AttendanceStatus,
  "green" | "red" | "amber" | "blue" | "purple"
> = {
  Present: "green",
  Absent: "red",
  Late: "amber",
  "Half Day": "blue",
  Leave: "purple",
};

// Dates travel as "yyyy-MM-dd" strings; timestamps are ISO strings.

export interface AttendanceRosterStudent {
  studentId: number;
  rollNumber: string;
  name: string;
  admNo: string;
  isActiveInClass: boolean;
  status: AttendanceStatus | null;
  remarks: string | null;
  /** Previous (up to) 7 sessions, oldest first. null = not recorded that day. */
  last7: (string | null)[];
}

export interface AttendanceRosterResponse {
  classSectionId: number;
  className: string;
  date: string;
  subjectId: number | null;
  subjectName: string | null;
  isSaved: boolean;
  savedAtUtc: string | null;
  savedBy: string | null;
  canEdit: boolean;
  cannotEditReason: string | null;
  previousSessionDate: string | null;
  students: AttendanceRosterStudent[];
}

export interface AttendanceEntry {
  studentId: number;
  status: AttendanceStatus;
  remarks: string | null;
}

export interface SaveAttendanceRequest {
  classSectionId: number;
  date: string;
  subjectId: number | null;
  records: AttendanceEntry[];
}

export interface AttendanceCounts {
  present: number;
  absent: number;
  late: number;
  halfDay: number;
  leave: number;
  total: number;
  percentage: number | null;
}

export interface StudentAttendanceRecord {
  date: string;
  mode: "Daily" | "Subject Wise";
  subjectName: string | null;
  status: AttendanceStatus;
  remarks: string | null;
}

export interface StudentAttendanceResponse {
  studentId: number;
  name: string;
  admNo: string;
  from: string;
  to: string;
  summary: AttendanceCounts;
  records: StudentAttendanceRecord[];
}

export interface AttendanceSummaryStudent {
  studentId: number;
  rollNumber: string;
  name: string;
  admNo: string;
  counts: AttendanceCounts;
}

export interface ClassAttendanceSummaryResponse {
  classSectionId: number;
  className: string;
  from: string;
  to: string;
  daysMarked: number;
  classPercentage: number | null;
  students: AttendanceSummaryStudent[];
}

/**
 * Same formula as the backend (AttendanceRules.Percentage):
 * Present = 1, Late = 1, Half Day = 0.5, Absent = 0, Leave excluded.
 * Returns null when nothing counts yet.
 */
export function attendancePercentage(
  counts: Partial<Record<AttendanceStatus, number>>,
): number | null {
  const present = counts.Present ?? 0;
  const late = counts.Late ?? 0;
  const half = counts["Half Day"] ?? 0;
  const absent = counts.Absent ?? 0;
  const denominator = present + late + half + absent;
  if (denominator === 0) return null;
  return Math.round(((present + late + 0.5 * half) / denominator) * 1000) / 10;
}