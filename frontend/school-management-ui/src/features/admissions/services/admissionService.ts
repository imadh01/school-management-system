import { apiClient } from "@/services/apiClient";
import type {
  AdmissionResponse,
  DeletedAdmissionResponse,
  CreateAdmissionRequest,
  UpdateAdmissionRequest,
  ConfirmAdmissionRequest,
  EnrollAdmissionRequest,
  RejectAdmissionRequest,
  GuardianMatchesResponse,
  ParentCandidate,
} from "../types/admission.types";

export const admissionService = {
  getAll: () =>
    apiClient.get<AdmissionResponse[]>("/admissions").then((res) => res.data),

  // The standard fee of the class this application is for (amount null = none set).
  getSuggestedFee: (id: number) =>
    apiClient
      .get<{ className: string; amount: number | null }>(
        `/admissions/${id}/suggested-fee`,
      )
      .then((res) => res.data),

  // Deleted applications with who deleted them and when (needs Admissions.Delete).
  getDeleted: () =>
    apiClient
      .get<DeletedAdmissionResponse[]>("/admissions/deleted")
      .then((res) => res.data),

  // Brings a deleted application back. Admin only (needs Admissions.Delete as well).
  restore: (id: number) =>
    apiClient
      .post<AdmissionResponse>(`/admissions/${id}/restore`)
      .then((res) => res.data),

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

  // Parents already in the system that match each guardian's mobile number.
  getGuardianMatches: (id: number) =>
    apiClient
      .get<GuardianMatchesResponse>(`/admissions/${id}/guardian-matches`)
      .then((res) => res.data),

  // G2: "Find an existing parent" on the registration form. Needs Parents.View (the API refuses otherwise).
  searchParents: (q: string) =>
    apiClient
      .get<ParentCandidate[]>("/admissions/parent-search", { params: { q } })
      .then((res) => res.data),

  enroll: (id: number, request: EnrollAdmissionRequest) =>
    apiClient
      .post<AdmissionResponse>(`/admissions/${id}/enroll`, request)
      .then((res) => res.data),

  reject: (id: number, request: RejectAdmissionRequest) =>
    apiClient
      .post<AdmissionResponse>(`/admissions/${id}/reject`, request)
      .then((res) => res.data),

  delete: (id: number) => apiClient.delete(`/admissions/${id}`),
};
