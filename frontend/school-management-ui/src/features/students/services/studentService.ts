import { apiClient } from "@/services/apiClient";
import type {
  StudentResponse,
  StudentSummaryResponse,
  CreateStudentRequest,
  UpdateStudentRequest,
  UpdateStudentIdentityRequest,
  StudentIdentityResponse,
  StudentEnrollmentResponse,
} from "../types/student.types";

export const studentService = {
  // List rows only — call getById for the full record.
  getAll: () =>
    apiClient
      .get<StudentSummaryResponse[]>("/students")
      .then((res) => res.data),

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

  // Needs both Students.Manage and Students.ViewSensitive.
  updateIdentity: (id: number, request: UpdateStudentIdentityRequest) =>
    apiClient
      .put<StudentIdentityResponse>(`/students/${id}/identity`, request)
      .then((res) => res.data),

  getEnrollments: (id: number) =>
    apiClient
      .get<StudentEnrollmentResponse[]>(`/students/${id}/enrollments`)
      .then((res) => res.data),

  delete: (id: number) => apiClient.delete(`/students/${id}`),
};
