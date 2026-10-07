import { apiClient } from "@/services/apiClient";
import type {
  AttendanceRosterResponse,
  ClassAttendanceSummaryResponse,
  SaveAttendanceRequest,
  StudentAttendanceResponse,
} from "../types/attendance.types";

export const attendanceService = {
  getRoster: (params: {
    classSectionId: number;
    date: string;
    subjectId?: number | null;
  }) =>
    apiClient
      .get<AttendanceRosterResponse>("/attendance/roster", {
        params: {
          classSectionId: params.classSectionId,
          date: params.date,
          subjectId: params.subjectId ?? undefined,
        },
      })
      .then((res) => res.data),

  save: (request: SaveAttendanceRequest) =>
    apiClient
      .put<AttendanceRosterResponse>("/attendance", request)
      .then((res) => res.data),

  getStudentHistory: (studentId: number, from?: string, to?: string) =>
    apiClient
      .get<StudentAttendanceResponse>(`/attendance/students/${studentId}`, {
        params: { from, to },
      })
      .then((res) => res.data),

  getSummary: (classSectionId: number, from: string, to: string) =>
    apiClient
      .get<ClassAttendanceSummaryResponse>("/attendance/summary", {
        params: { classSectionId, from, to },
      })
      .then((res) => res.data),
};
