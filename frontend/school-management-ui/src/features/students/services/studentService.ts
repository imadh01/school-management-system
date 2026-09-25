import { apiClient } from "@/services/apiClient";
import type {
  StudentResponse,
  CreateStudentRequest,
  UpdateStudentRequest,
} from "../types/student.types";

export const studentService = {
  getAll: () =>
    apiClient.get<StudentResponse[]>("/students").then((res) => res.data),

  getById: (id: number) =>
    apiClient.get<StudentResponse>(`/students/${id}`).then((res) => res.data),

  create: (request: CreateStudentRequest) =>
    apiClient
      .post<StudentResponse>("/students", request)
      .then((res) => res.data),

  update: (id: number, request: UpdateStudentRequest) =>
    apiClient
      .put<StudentResponse>(`/students/${id}`, request)
      .then((res) => res.data),

  delete: (id: number) => apiClient.delete(`/students/${id}`),
};
