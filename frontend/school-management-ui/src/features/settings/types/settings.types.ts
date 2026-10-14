/** E3b: the school's own record (GET/PUT /api/settings/school). */
export const WEEK_DAYS = [
  "Monday",
  "Tuesday",
  "Wednesday",
  "Thursday",
  "Friday",
  "Saturday",
  "Sunday",
] as const;

export interface SchoolSettings {
  schoolName: string;
  shortName: string | null;
  email: string | null;
  phone: string | null;
  website: string | null;
  addressText: string | null;
  countryId: number | null;
  countryName: string | null;
  timeZoneId: string;
  currencyCode: string;
  workingDays: string[];
  aboutTagline: string | null;
  rowVersion: string;
}

export type UpdateSchoolSettingsRequest = Omit<
  SchoolSettings,
  "countryName"
>;

export interface TimeZoneOption {
  id: string;
  displayName: string;
  utcOffsetMinutes: number;
}
