import { apiClient } from "@/services/apiClient";

/** One academic year. Dates are "yyyy-MM-dd". Matches AcademicYearResponse on the backend. */
export interface AcademicYearResponse {
  id: number;
  name: string;
  startDate: string;
  endDate: string;
  isCurrent: boolean;
  status: string;
}

export const academicYearService = {
  getAll: () =>
    apiClient
      .get<AcademicYearResponse[]>("/academic-years")
      .then((res) => res.data),
};
