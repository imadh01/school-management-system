import { apiClient } from "@/services/apiClient";

/** One academic year. Dates are "yyyy-MM-dd". Matches AcademicYearResponse on the backend. */
export interface AcademicYearResponse {
  id: number;
  name: string;
  startDate: string;
  endDate: string;
  isCurrent: boolean;
  /** Upcoming | Current | Ended | Closed */
  status: string;
  rowVersion: string;
  /** ISO 8601 UTC, null until the year is closed. */
  closedAt: string | null;
  classCount: number;
  activeEnrollmentCount: number;
  periodCount: number;
}

export interface CreateAcademicYearRequest {
  name: string;
  startDate: string;
  endDate: string;
}

export interface UpdateAcademicYearRequest extends CreateAcademicYearRequest {
  rowVersion: string;
}

export interface CopyPeriodsResult {
  copied: number;
}

export const academicYearService = {
  /** Every year, newest first. Any signed-in user may read the list (it feeds the dropdowns). */
  getAll: (status?: string) =>
    apiClient
      .get<AcademicYearResponse[]>("/academic-years", {
        params: { status: status ?? undefined },
      })
      .then((res) => res.data),

  create: (request: CreateAcademicYearRequest) =>
    apiClient
      .post<AcademicYearResponse>("/academic-years", request)
      .then((res) => res.data),

  update: (id: number, request: UpdateAcademicYearRequest) =>
    apiClient
      .put<AcademicYearResponse>(`/academic-years/${id}`, request)
      .then((res) => res.data),

  /** expectedCurrentYearId: the year the confirm dialog named as current (null = none). */
  setCurrent: (
    id: number,
    request: { rowVersion: string; expectedCurrentYearId: number | null },
  ) =>
    apiClient
      .post<AcademicYearResponse>(`/academic-years/${id}/set-current`, request)
      .then((res) => res.data),

  /** confirmName must equal the year's name: closing cannot be undone. */
  close: (id: number, request: { rowVersion: string; confirmName: string }) =>
    apiClient
      .post<AcademicYearResponse>(`/academic-years/${id}/close`, request)
      .then((res) => res.data),

  copyPeriods: (id: number, fromYearId: number) =>
    apiClient
      .post<CopyPeriodsResult>(`/academic-years/${id}/copy-periods`, null, {
        params: { fromId: fromYearId },
      })
      .then((res) => res.data),

  remove: (id: number) => apiClient.delete(`/academic-years/${id}`),
};
