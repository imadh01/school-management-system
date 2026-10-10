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
  allergies: string | null;
  admissionRegNo: string | null;
  admissionId: number | null;
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
  addressLine: string | null;
  city: string | null;
  state: string | null;
  pincode: string | null;
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
  addressLine: string | null;
  city: string | null;
  state: string | null;
  pincode: string | null;
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
