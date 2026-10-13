import type { AddressRequest, AddressResponse } from "@/types/address";
export interface ParentResponse {
  id: number;
  name: string;
  email: string | null;
  mobile: string;
  status: "Active" | "Inactive";
  occupation: string | null;
  nationality: string | null;
  countryOfResidence: string | null;
  timezone: string | null;
  preferredLanguage: string | null;
  preferredContactMethod: string | null;
  whatsapp: string | null;
  emergencyOnly: boolean;
  notifyAttendance: boolean;
  notifyExams: boolean;
  notifyFees: boolean;
  notifyNotices: boolean;
  notifyDiscipline: boolean;
  employer: string | null;
  jobTitle: string | null;
  workEmail: string | null;
  workPhone: string | null;
  billingContact: boolean;
  /** G1a: Home / Permanent / Mailing, primary first. */
  addresses: AddressResponse[];
  /** The old one-line address, only when it could not be copied into addresses (until G1c). */
  legacyAddress: string | null;
  /** Version stamp. Send it back unchanged when saving, so a concurrent edit is detected (409). */
  rowVersion: string;
}

/** A parent as a Parents.ViewScoped caller (a teacher) sees them: who to call (F1). */
export interface ParentContactResponse {
  id: number;
  name: string;
  mobile: string;
}

/** GET /parents and GET /parents/{id} return the full record or, for a teacher, the contact only. */
export type ParentDetail = ParentResponse | ParentContactResponse;

export function isFullParent(p: ParentDetail): p is ParentResponse {
  return "status" in p;
}

export interface CreateParentRequest {
  name: string;
  email: string | null;
  mobile: string;
  occupation: string | null;
  nationality: string | null;
  countryOfResidence: string | null;
  timezone: string | null;
  preferredLanguage: string | null;
  preferredContactMethod: string | null;
  whatsapp: string | null;
  emergencyOnly: boolean;
  notifyAttendance: boolean;
  notifyExams: boolean;
  notifyFees: boolean;
  notifyNotices: boolean;
  notifyDiscipline: boolean;
  employer: string | null;
  jobTitle: string | null;
  workEmail: string | null;
  workPhone: string | null;
  billingContact: boolean;
  /** G1a: replaces the stored addresses; null keeps them. */
  addresses: AddressRequest[] | null;
}

export interface UpdateParentRequest extends CreateParentRequest {
  status: "Active" | "Inactive";
  rowVersion: string;
}

export type RelationType = "Father" | "Mother" | "Guardian";

export interface StudentGuardianResponse {
  parentId: number;
  parentName: string;
  parentMobile: string;
  parentEmail: string | null;
  relationType: RelationType;
  isPrimaryContact: boolean;
}

export interface LinkGuardianRequest {
  parentId: number;
  relationType: RelationType;
  isPrimaryContact: boolean;
}

export interface LinkedStudentResponse {
  studentId: number;
  studentName: string;
  admNo: string;
  classSectionName: string;
  rollNumber: string;
  relationType: RelationType;
  isPrimaryContact: boolean;
}

export interface LinkStudentRequest {
  studentId: number;
  relationType: RelationType;
  isPrimaryContact: boolean;
}
