import { apiClient } from "@/services/apiClient";
import type { ClassSectionResponse } from "../types/classSection.types";

export const classSectionService = {
  getAll: () =>
    apiClient
      .get<ClassSectionResponse[]>("/class-sections")
      .then((res) => res.data),
};
