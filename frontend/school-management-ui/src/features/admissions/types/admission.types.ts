export type AdmissionStatus =
  | "Registered"
  | "Admitted"
  | "Enrolled"
  | "Rejected";

export type GuardianRelation = "Father" | "Mother" | "Guardian";

/** What the server suggests / what staff decide for each guardian at enrolment. */
export type GuardianAction =
  | "UseExisting"
  | "CreateNew"
  | "MustChoose"
  | "MissingMobile";

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
  addressLine: string | null;
  city: string | null;
  state: string | null;
  pincode: string | null;
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
  addressLine: string | null;
  city: string | null;
  state: string | null;
  pincode: string | null;
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
