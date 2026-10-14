export const TEACHER_STATUSES = ["Active", "Inactive", "Suspended"] as const;
export type TeacherStatus = (typeof TEACHER_STATUSES)[number];

export interface TeacherResponse {
  id: number;
  userId: number;
  name: string;
  username: string;
  email: string;
  phone: string | null;
  specialization: string | null;
  status: TeacherStatus | string;
  classCount: number;
  subjectCount: number;
  classTeacherOfCount: number;
}

/** E3a: only the teaching profile is edited here; the login lives on the Users page. */
export interface UpdateTeacherRequest {
  name: string;
  phone: string | null;
  specialization: string | null;
}

export interface TeacherSubject {
  subjectId: number;
  name: string;
  code: string;
}

export interface TeacherAssignment {
  classSectionId: number;
  classSectionName: string;
  academicYearName: string;
  isClassTeacher: boolean;
  subjects: TeacherSubject[];
}

export interface AssignSubjectsRequest {
  classSectionId: number;
  subjectIds: number[];
  makeClassTeacher: boolean;
}
