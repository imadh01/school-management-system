import { apiClient } from "@/services/apiClient";
import type {
  ParentResponse,
  CreateParentRequest,
  StudentGuardianResponse,
  LinkGuardianRequest,
} from "../types/parent.types";

export const parentService = {
  getAll: () =>
    apiClient.get<ParentResponse[]>("/parents").then((res) => res.data),

  getById: (id: number) =>
    apiClient.get<ParentResponse>(`/parents/${id}`).then((res) => res.data),

  create: (request: CreateParentRequest) =>
    apiClient.post<ParentResponse>("/parents", request).then((res) => res.data),

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
};
