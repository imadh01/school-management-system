import { apiClient } from "@/services/apiClient";

export interface AdmissionFeeResponse {
  className: string;
  /** null when no fee is set for the class yet. */
  amount: number | null;
}

const url = (className: string) =>
  `/admission-fees/${encodeURIComponent(className)}`;

export const admissionFeeService = {
  getAll: () =>
    apiClient
      .get<AdmissionFeeResponse[]>("/admission-fees")
      .then((res) => res.data),

  set: (className: string, amount: number) =>
    apiClient
      .put<AdmissionFeeResponse>(url(className), { amount })
      .then((res) => res.data),

  clear: (className: string) =>
    apiClient.delete(url(className)).then(() => undefined),
};
