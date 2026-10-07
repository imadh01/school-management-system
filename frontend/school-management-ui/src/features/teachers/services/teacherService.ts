import { apiClient } from "@/services/apiClient";
import type {
  AssignSubjectsRequest,
  CreateTeacherRequest,
  TeacherAssignment,
  TeacherResponse,
  TeacherStatus,
  UpdateTeacherRequest,
} from "../types/teacher.types";

export const teacherService = {
  getAll: () =>
    apiClient.get<TeacherResponse[]>("/teachers").then((res) => res.data),

  getById: (id: number) =>
    apiClient.get<TeacherResponse>(`/teachers/${id}`).then((res) => res.data),

  create: (request: CreateTeacherRequest) =>
    apiClient
      .post<TeacherResponse>("/teachers", request)
      .then((res) => res.data),

  update: (id: number, request: UpdateTeacherRequest) =>
    apiClient
      .put<TeacherResponse>(`/teachers/${id}`, request)
      .then((res) => res.data),

  changeStatus: (id: number, status: TeacherStatus) =>
    apiClient
      .patch<TeacherResponse>(`/teachers/${id}/status`, { status })
      .then((res) => res.data),

  delete: (id: number) => apiClient.delete(`/teachers/${id}`),

  // Assignment endpoints all return the teacher's refreshed assignment list.
  getAssignments: (id: number) =>
    apiClient
      .get<TeacherAssignment[]>(`/teachers/${id}/assignments`)
      .then((res) => res.data),

  assignSubjects: (id: number, request: AssignSubjectsRequest) =>
    apiClient
      .post<TeacherAssignment[]>(`/teachers/${id}/subjects`, request)
      .then((res) => res.data),

  unassignSubject: (id: number, subjectId: number) =>
    apiClient
      .delete<TeacherAssignment[]>(`/teachers/${id}/subjects/${subjectId}`)
      .then((res) => res.data),

  setClassTeacher: (id: number, classSectionId: number) =>
    apiClient
      .put<TeacherAssignment[]>(`/teachers/${id}/class-teacher`, {
        classSectionId,
      })
      .then((res) => res.data),

  clearClassTeacher: (id: number, classSectionId: number) =>
    apiClient
      .delete<
        TeacherAssignment[]
      >(`/teachers/${id}/class-teacher/${classSectionId}`)
      .then((res) => res.data),
};
