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

export interface CreateTeacherRequest {
  name: string;
  username: string;
  email: string;
  password: string;
  phone: string | null;
  specialization: string | null;
  status: TeacherStatus;
}

export interface UpdateTeacherRequest {
  name: string;
  username: string;
  email: string;
  phone: string | null;
  specialization: string | null;
  status: TeacherStatus;
  newPassword: string | null;
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
