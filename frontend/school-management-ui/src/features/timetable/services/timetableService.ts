import { apiClient } from "@/services/apiClient";
import type {
  ClassTimetableResponse,
  CopyTimetableResult,
  CreateTimetablePeriodRequest,
  TeacherTimetableResponse,
  TimetableCellInput,
  TimetablePeriodResponse,
  UpdateTimetablePeriodRequest,
} from "../types/timetable.types";

export const timetableService = {
  // ---- periods
  getPeriods: (academicYearId?: number | null) =>
    apiClient
      .get<TimetablePeriodResponse[]>("/timetable/periods", {
        params: { academicYearId: academicYearId ?? undefined },
      })
      .then((res) => res.data),

  createPeriod: (request: CreateTimetablePeriodRequest) =>
    apiClient
      .post<TimetablePeriodResponse>("/timetable/periods", request)
      .then((res) => res.data),

  updatePeriod: (id: number, request: UpdateTimetablePeriodRequest) =>
    apiClient
      .put<TimetablePeriodResponse>(`/timetable/periods/${id}`, request)
      .then((res) => res.data),

  deletePeriod: (id: number) => apiClient.delete(`/timetable/periods/${id}`),

  // ---- a class's week
  getClass: (classSectionId: number) =>
    apiClient
      .get<ClassTimetableResponse>(`/timetable/classes/${classSectionId}`)
      .then((res) => res.data),

  saveCells: (classSectionId: number, cells: TimetableCellInput[]) =>
    apiClient
      .put<ClassTimetableResponse>(
        `/timetable/classes/${classSectionId}/cells`,
        { cells },
      )
      .then((res) => res.data),

  clearCell: (classSectionId: number, day: number, periodId: number) =>
    apiClient.delete(`/timetable/classes/${classSectionId}/cells`, {
      params: { day, periodId },
    }),

  copyFrom: (classSectionId: number, sourceClassSectionId: number) =>
    apiClient
      .post<CopyTimetableResult>(
        `/timetable/classes/${classSectionId}/copy-from/${sourceClassSectionId}`,
      )
      .then((res) => res.data),

  // ---- a teacher's week
  getTeacher: (teacherId: number, academicYearId?: number | null) =>
    apiClient
      .get<TeacherTimetableResponse>(`/timetable/teachers/${teacherId}`, {
        params: { academicYearId: academicYearId ?? undefined },
      })
      .then((res) => res.data),
};
