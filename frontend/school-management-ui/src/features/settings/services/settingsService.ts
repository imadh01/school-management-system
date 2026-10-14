import { apiClient } from "@/services/apiClient";
import { schoolService } from "@/services/schoolService";
import type {
  SchoolSettings,
  TimeZoneOption,
  UpdateSchoolSettingsRequest,
} from "../types/settings.types";

export const settingsService = {
  getSchool: () =>
    apiClient.get<SchoolSettings>("/settings/school").then((res) => res.data),

  /** Saves, then makes the top bar and address forms pick up the new name/country. */
  updateSchool: (request: UpdateSchoolSettingsRequest) =>
    apiClient
      .put<SchoolSettings>("/settings/school", request)
      .then((res) => {
        schoolService.invalidate();
        return res.data;
      }),

  getTimeZones: () =>
    apiClient
      .get<TimeZoneOption[]>("/settings/time-zones")
      .then((res) => res.data),
};
