import { apiClient } from "@/services/apiClient";
import type {
  StudentResponse,
  StudentSummaryResponse,
  CreateStudentRequest,
  UpdateStudentRequest,
  UpdateStudentIdentityRequest,
  StudentEnrollmentResponse,
  StudentDetail,
} from "../types/student.types";

export const studentService = {
  // List rows only — call getById for the full record.
  getAll: () =>
    apiClient
      .get<StudentSummaryResponse[]>("/students")
      .then((res) => res.data),

  // The full record for Students.View, the reduced one for Students.ViewScoped (F1).
  getById: (id: number) =>
    apiClient.get<StudentDetail>(`/students/${id}`).then((res) => res.data),

  create: (request: CreateStudentRequest) =>
    apiClient
      .post<StudentResponse>("/students", request)
      .then((res) => res.data),

  update: (id: number, request: UpdateStudentRequest) =>
    apiClient
      .put<StudentResponse>(`/students/${id}`, request)
      .then((res) => res.data),

  // Needs both Students.Edit and Students.ViewSensitive.
  // Sends the student's rowVersion (409 if it is stale) and returns the whole student with the NEW rowVersion.
  updateIdentity: (
    id: number,
    request: UpdateStudentIdentityRequest,
    rowVersion: string,
  ) =>
    apiClient
      .put<StudentResponse>(`/students/${id}/identity`, {
        ...request,
        rowVersion,
      })
      .then((res) => res.data),

  getEnrollments: (id: number) =>
    apiClient
      .get<StudentEnrollmentResponse[]>(`/students/${id}/enrollments`)
      .then((res) => res.data),

  delete: (id: number) => apiClient.delete(`/students/${id}`),

  // Closes the student's class period and sets the status to Left (needs Students.Edit).
  markLeft: (id: number) => apiClient.post(`/students/${id}/leave`),

  // The photo is a file behind the API (an <img> tag cannot send the login token), so it is fetched
  // as a blob and shown through an object URL (see useStudentPhoto).
  getPhoto: (id: number) =>
    apiClient
      .get<Blob>(`/students/${id}/photo`, { responseType: "blob" })
      .then((res) => res.data),

  // JPG or PNG up to 2 MB, sent as multipart/form-data. Replaces the current photo (needs Students.Edit).
  setPhoto: (id: number, file: File) => {
    const form = new FormData();
    form.append("file", file, file.name);
    return apiClient.put(`/students/${id}/photo`, form, {
      headers: { "Content-Type": "multipart/form-data" },
    });
  },

  removePhoto: (id: number) => apiClient.delete(`/students/${id}/photo`),
};
