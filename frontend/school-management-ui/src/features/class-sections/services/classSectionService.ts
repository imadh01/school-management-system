import { apiClient } from "@/services/apiClient";
import type {
  ClassSectionResponse,
  CreateClassSectionRequest,
  UpdateClassSectionRequest,
} from "../types/classSection.types";

export const classSectionService = {
  getAll: () =>
    apiClient
      .get<ClassSectionResponse[]>("/class-sections")
      .then((res) => res.data),

  getById: (id: number) =>
    apiClient
      .get<ClassSectionResponse>(`/class-sections/${id}`)
      .then((res) => res.data),

  create: (request: CreateClassSectionRequest) =>
    apiClient
      .post<ClassSectionResponse>("/class-sections", request)
      .then((res) => res.data),

  update: (id: number, request: UpdateClassSectionRequest) =>
    apiClient
      .put<ClassSectionResponse>(`/class-sections/${id}`, request)
      .then((res) => res.data),

  changeStatus: (id: number, isActive: boolean) =>
    apiClient
      .patch<ClassSectionResponse>(`/class-sections/${id}/status`, {
        isActive,
      })
      .then((res) => res.data),

  delete: (id: number) => apiClient.delete(`/class-sections/${id}`),
};
