import type { AddressRequest, AddressResponse } from "@/types/address";
import type { BadgeTone } from "@/components/Badge";

export type AdmissionStatus =
  "Registered" | "Admitted" | "Enrolled" | "Rejected";

export type GuardianRelation = "Father" | "Mother" | "Guardian";

/** What the server suggests / what staff decide for each guardian at enrolment. */
export type GuardianAction =
  "UseExisting" | "CreateNew" | "MustChoose" | "MissingMobile";

export interface AdmissionGuardianRequest {
  relationType: GuardianRelation;
  name: string;
  mobile: string | null;
  email: string | null;
  isPrimaryContact: boolean;
}

export interface AdmissionGuardianResponse {
  id: number;
  relationType: GuardianRelation;
  name: string;
  mobile: string | null;
  email: string | null;
  isPrimaryContact: boolean;
}

export interface AdmissionResponse {
  id: number;
  regNo: string;
  firstName: string;
  middleName: string | null;
  lastName: string;
  gender: string;
  dateOfBirth: string;
  academicYearName: string;
  appliedForClassSectionId: number;
  appliedForClassSectionName: string;
  admissionType: string;
  previousSchool: string | null;
  phone: string;
  email: string | null;
  registrationDate: string;
  status: AdmissionStatus;
  rejectionReason: string | null;
  /** G1a: Home / Permanent / Mailing, primary first. */
  addresses: AddressResponse[];
  admissionFee: number | null;
  admissionFeeReference: string | null;
  bloodGroup: string | null;
  religion: string | null;
  category: string | null;
  medicalNotes: string | null;
  remarks: string | null;
  rollNumber: string | null;
  admissionNumber: string | null;
  admissionDate: string | null;
  entryPoint: string | null;
  transportRequired: boolean;
  allottedClassSectionId: number | null;
  allottedClassSectionName: string | null;
  studentId: number | null;
  guardians: AdmissionGuardianResponse[];
  /** Version stamp. Send it back unchanged when saving, so a concurrent edit is detected (409). */
  rowVersion: string;
}

export interface CreateAdmissionRequest {
  firstName: string;
  middleName: string | null;
  lastName: string;
  gender: string;
  dateOfBirth: string;
  appliedForClassSectionId: number;
  admissionType: string;
  previousSchool: string | null;
  phone: string;
  email: string | null;
  /** G1a: replaces the stored addresses; null keeps them. */
  addresses: AddressRequest[] | null;
  remarks: string | null;
  guardians: AdmissionGuardianRequest[];
}

export interface UpdateAdmissionRequest extends CreateAdmissionRequest {
  rowVersion: string;
}

export interface ConfirmAdmissionRequest {
  admissionFee: number | null;
  admissionFeeReference: string | null;
  bloodGroup: string | null;
  religion: string | null;
  category: string | null;
  medicalNotes: string | null;
  remarks: string | null;
}

export interface GuardianDecision {
  admissionGuardianId: number;
  action: "UseExisting" | "CreateNew";
  parentId: number | null;
}

export interface EnrollAdmissionRequest {
  rollNumber: string;
  admissionNumber: string;
  admissionDate: string;
  entryPoint: string | null;
  transportRequired: boolean;
  allottedClassSectionId: number;
  nationality: string | null;
  curriculumTrack: string | null;
  englishProficiency: string | null;
  ealCode: string | null;
  house: string | null;
  allergies: string | null;
  guardians: GuardianDecision[];
}

export interface RejectAdmissionRequest {
  rejectionReason: string;
}

export interface ParentCandidate {
  parentId: number;
  name: string;
  mobile: string;
  email: string | null;
  linkedChildren: string[];
}

export interface GuardianMatch {
  admissionGuardianId: number;
  relationType: GuardianRelation;
  name: string;
  mobile: string | null;
  suggestedAction: GuardianAction;
  candidates: ParentCandidate[];
}

export interface GuardianMatchesResponse {
  admissionId: number;
  guardians: GuardianMatch[];
}

/**
 * U2: an application can be edited only while it is in progress. Enrolled: the data now lives on
 * the student record. Rejected: closed. The API refuses the edit too (409); this only hides it.
 */
export function isAdmissionEditable(status: AdmissionStatus): boolean {
  return status === "Registered" || status === "Admitted";
}

/** An enrolled application is the student's admission record and is kept (the API refuses to delete it). */
export function isAdmissionDeletable(status: AdmissionStatus): boolean {
  return status !== "Enrolled";
}

/** One colour per status, used by the list and the detail page. */
export const ADMISSION_STATUS_TONE: Record<AdmissionStatus, BadgeTone> = {
  Registered: "info",
  Admitted: "warn",
  Enrolled: "ok",
  Rejected: "bad",
};
