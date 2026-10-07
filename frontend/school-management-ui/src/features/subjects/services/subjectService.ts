import { apiClient } from "@/services/apiClient";
import type { SubjectRequest, SubjectResponse } from "../types/subject.types";

export const subjectService = {
  getAll: () =>
    apiClient.get<SubjectResponse[]>("/subjects").then((res) => res.data),

  getById: (id: number) =>
    apiClient.get<SubjectResponse>(`/subjects/${id}`).then((res) => res.data),

  create: (request: SubjectRequest) =>
    apiClient
      .post<SubjectResponse>("/subjects", request)
      .then((res) => res.data),

  update: (id: number, request: SubjectRequest) =>
    apiClient
      .put<SubjectResponse>(`/subjects/${id}`, request)
      .then((res) => res.data),

  delete: (id: number) => apiClient.delete(`/subjects/${id}`),
};
