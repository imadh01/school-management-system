import { apiClient } from "@/services/apiClient";
import type {
  AdmissionResponse,
  CreateAdmissionRequest,
  UpdateAdmissionRequest,
  ConfirmAdmissionRequest,
  EnrollAdmissionRequest,
  RejectAdmissionRequest,
} from "../types/admission.types";
import type {
  StudentResponse,
  CreateStudentFromAdmissionRequest,
} from "@/features/students/types/student.types";

export const admissionService = {
  getAll: () =>
    apiClient.get<AdmissionResponse[]>("/admissions").then((res) => res.data),

  getById: (id: number) =>
    apiClient
      .get<AdmissionResponse>(`/admissions/${id}`)
      .then((res) => res.data),

  create: (request: CreateAdmissionRequest) =>
    apiClient
      .post<AdmissionResponse>("/admissions", request)
      .then((res) => res.data),

  update: (id: number, request: UpdateAdmissionRequest) =>
    apiClient
      .put<AdmissionResponse>(`/admissions/${id}`, request)
      .then((res) => res.data),

  confirmAdmission: (id: number, request: ConfirmAdmissionRequest) =>
    apiClient
      .post<AdmissionResponse>(`/admissions/${id}/confirm-admission`, request)
      .then((res) => res.data),

  enroll: (id: number, request: EnrollAdmissionRequest) =>
    apiClient
      .post<AdmissionResponse>(`/admissions/${id}/enroll`, request)
      .then((res) => res.data),

  reject: (id: number, request: RejectAdmissionRequest) =>
    apiClient
      .post<AdmissionResponse>(`/admissions/${id}/reject`, request)
      .then((res) => res.data),

  createStudent: (id: number, request: CreateStudentFromAdmissionRequest) =>
    apiClient
      .post<StudentResponse>(`/admissions/${id}/create-student`, request)
      .then((res) => res.data),

  delete: (id: number) => apiClient.delete(`/admissions/${id}`),
};
