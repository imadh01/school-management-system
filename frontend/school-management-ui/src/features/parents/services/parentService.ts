import { apiClient } from "@/services/apiClient";
import type {
  ParentResponse,
  ParentDetail,
  CreateParentRequest,
  UpdateParentRequest,
  StudentGuardianResponse,
  LinkGuardianRequest,
  LinkedStudentResponse,
  LinkStudentRequest,
} from "../types/parent.types";

export const parentService = {
  // The full record for Parents.View, name and mobile only for Parents.ViewScoped (F1).
  getAll: () =>
    apiClient.get<ParentDetail[]>("/parents").then((res) => res.data),

  getById: (id: number) =>
    apiClient.get<ParentDetail>(`/parents/${id}`).then((res) => res.data),

  create: (request: CreateParentRequest) =>
    apiClient.post<ParentResponse>("/parents", request).then((res) => res.data),

  update: (id: number, request: UpdateParentRequest) =>
    apiClient
      .put<ParentResponse>(`/parents/${id}`, request)
      .then((res) => res.data),

  delete: (id: number) => apiClient.delete(`/parents/${id}`),

  // The guardians of one student (F1: a teacher gets the contact-only shape).
  getGuardiansForStudent: (studentId: number) =>
    apiClient
      .get<StudentGuardianResponse[]>(`/students/${studentId}/guardians`)
      .then((res) => res.data),

  linkGuardian: (studentId: number, request: LinkGuardianRequest) =>
    apiClient
      .post<StudentGuardianResponse>(
        `/students/${studentId}/guardians`,
        request,
      )
      .then((res) => res.data),

  unlinkGuardian: (studentId: number, parentId: number) =>
    apiClient.delete(`/students/${studentId}/guardians/${parentId}`),

  // Linked Children — managed from the Parents page.
  getLinkedStudents: (parentId: number) =>
    apiClient
      .get<LinkedStudentResponse[]>(`/parents/${parentId}/students`)
      .then((res) => res.data),

  linkStudent: (parentId: number, request: LinkStudentRequest) =>
    apiClient
      .post<LinkedStudentResponse>(`/parents/${parentId}/students`, request)
      .then((res) => res.data),

  unlinkStudent: (parentId: number, studentId: number) =>
    apiClient.delete(`/parents/${parentId}/students/${studentId}`),

  setPrimaryStudent: (parentId: number, studentId: number) =>
    apiClient.post(`/parents/${parentId}/students/${studentId}/primary`),
};
