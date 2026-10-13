import type { AddressRequest, AddressResponse } from "@/types/address";
import type { StudentGuardianResponse } from "@/features/parents/types/parent.types";

// BUSINESS DECISION REQUIRED: the API currently accepts only these three
// statuses. The earlier prototype also listed Transferred / Passed Out /
// Suspended — add them to the backend validator first if they are wanted.
export type StudentStatus = "Active" | "Inactive" | "Left";

export interface StudentHealthDto {
  bloodGroup: string | null;
  allergies: string | null;
  dietaryRequirements: string | null;
  medicalNotes: string | null;
  specialEducationalNeeds: string | null;
  insuranceProvider: string | null;
  insurancePolicyExpiry: string | null;
}

export interface PickupPersonRequest {
  name: string;
  relation: string;
  phone: string;
  idNote: string | null;
}

export interface PickupPersonResponse extends PickupPersonRequest {
  id: number;
}

export interface StudentIdentityResponse {
  aadhaarNumber: string | null;
  passportNumber: string | null;
  passportExpiry: string | null;
  visaType: string | null;
  visaExpiry: string | null;
  /** true when the caller lacks Students.ViewSensitive and numbers are hidden. */
  isMasked: boolean;
}

export interface UpdateStudentIdentityRequest {
  aadhaarNumber: string | null;
  passportNumber: string | null;
  passportExpiry: string | null;
  visaType: string | null;
  visaExpiry: string | null;
}

/** One row of the student list (GET /students). */
export interface StudentSummaryResponse {
  id: number;
  admNo: string;
  rollNumber: string;
  classSectionId: number;
  classSectionName: string;
  admissionDate: string;
  status: StudentStatus;
  photoUrl: string | null;
  firstName: string;
  middleName: string | null;
  lastName: string;
  gender: string;
  dateOfBirth: string;
  mobile: string | null;
  category: string;
  transportRequired: boolean;
  nationality: string | null;
  curriculumTrack: string | null;
  house: string | null;
  ealCode: string | null;
  /** The allergy text: only for Students.ViewSensitive holders, null for everyone else (U1). */
  allergies: string | null;
  admissionRegNo: string | null;
  admissionId: number | null;
  /** Everyone who can read the student sees whether there are allergies. */
  hasAllergies: boolean;
}

/** Full detail of one student (GET /students/{id}). */
export interface StudentResponse {
  id: number;
  admNo: string;
  rollNumber: string;
  classSectionId: number;
  classSectionName: string;
  admissionDate: string;
  status: StudentStatus;
  photoUrl: string | null;
  firstName: string;
  middleName: string | null;
  lastName: string;
  gender: string;
  dateOfBirth: string;
  mobile: string | null;
  email: string | null;
  /** G1a: Home / Permanent / Mailing, primary first. */
  addresses: AddressResponse[];
  /** The old one-line address, only when it could not be copied into addresses (until G1c). */
  legacyAddress: string | null;
  category: string;
  religion: string | null;
  previousSchool: string | null;
  transportRequired: boolean;
  transportRoute: string | null;
  nationality: string | null;
  secondNationality: string | null;
  countryOfBirth: string | null;
  preferredName: string | null;
  motherTongue: string | null;
  homeLanguage: string | null;
  englishProficiency: string | null;
  curriculumTrack: string | null;
  admissionType: string;
  custodyArrangement: string | null;
  mediaConsent: boolean;
  house: string | null;
  ealCode: string | null;
  feeConcessionPercent: number | null;
  admissionRegNo: string | null;
  admissionId: number | null;
  health: StudentHealthDto;
  identity: StudentIdentityResponse;
  pickupPersons: PickupPersonResponse[];
  guardians: StudentGuardianResponse[];
  /** Version stamp. Send it back unchanged when saving, so a concurrent edit is detected (409). */
  rowVersion: string;
  /** health.allergies is null without Students.ViewSensitive; this flag is for everyone (U1). */
  hasAllergies: boolean;
}

export interface CreateStudentRequest {
  admNo: string;
  rollNumber: string;
  classSectionId: number;
  admissionDate: string;
  photoUrl: string | null;
  firstName: string;
  middleName: string | null;
  lastName: string;
  gender: string;
  dateOfBirth: string;
  mobile: string | null;
  email: string | null;
  /** G1a: replaces the stored addresses; null keeps them. */
  addresses: AddressRequest[] | null;
  category: string;
  religion: string | null;
  previousSchool: string | null;
  transportRequired: boolean;
  transportRoute: string | null;
  nationality: string | null;
  secondNationality: string | null;
  countryOfBirth: string | null;
  preferredName: string | null;
  motherTongue: string | null;
  homeLanguage: string | null;
  englishProficiency: string | null;
  curriculumTrack: string | null;
  admissionType: string;
  custodyArrangement: string | null;
  mediaConsent: boolean;
  house: string | null;
  ealCode: string | null;
  feeConcessionPercent: number | null;
  health: StudentHealthDto | null;
  pickupPersons: PickupPersonRequest[] | null;
}

export interface UpdateStudentRequest extends Omit<
  CreateStudentRequest,
  "admNo" | "admissionDate"
> {
  status: StudentStatus;
  rowVersion: string;
}
export interface StudentEnrollmentResponse {
  id: number;
  academicYearId: number;
  academicYearName: string;
  classSectionId: number;
  classSectionName: string;
  rollNumber: string;
  startDate: string;
  endDate: string | null;
  status: "Active" | "Promoted" | "Repeated" | "Transferred" | "Left";
  remarks: string | null;
}

/** A guardian as a Students.ViewScoped caller (a teacher) sees them: who to call (F1). */
export interface GuardianContactResponse {
  parentId: number;
  parentName: string;
  parentMobile: string;
  relationType: string;
  isPrimaryContact: boolean;
}

/**
 * GET /students/{id} for a caller with only Students.ViewScoped (F1): a strict subset of
 * StudentResponse with the same property names. No contact details, identity, health text,
 * pickup persons or fee data; allergies only as a flag.
 */
export interface StudentScopedResponse {
  id: number;
  admNo: string;
  rollNumber: string;
  classSectionId: number;
  classSectionName: string;
  status: StudentStatus;
  photoUrl: string | null;
  firstName: string;
  middleName: string | null;
  lastName: string;
  preferredName: string | null;
  gender: string;
  dateOfBirth: string;
  house: string | null;
  health: { hasAllergies: boolean };
  guardians: GuardianContactResponse[];
}

export type StudentDetail = StudentResponse | StudentScopedResponse;

/** True when the API sent the full record (Students.View), false for the reduced one. */
export function isFullStudent(s: StudentDetail): s is StudentResponse {
  return "identity" in s;
}
