export type AdmissionStatus =
  | "Registered"
  | "Admitted"
  | "Enrolled"
  | "Rejected";

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
  grade: string | null;
  admissionType: string;
  previousSchool: string | null;
  phone: string;
  email: string | null;
  registrationDate: string;
  status: AdmissionStatus;
  rejectionReason: string | null;
  fatherName: string | null;
  fatherMobile: string | null;
  motherName: string | null;
  motherMobile: string | null;
  guardianName: string | null;
  guardianRelation: string | null;
  guardianMobile: string | null;
  addressLine: string | null;
  city: string | null;
  state: string | null;
  pincode: string | null;
  registrationFee: number | null;
  notes: string | null;
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
}

export interface CreateAdmissionRequest {
  firstName: string;
  middleName: string | null;
  lastName: string;
  gender: string;
  dateOfBirth: string;
  appliedForClassSectionId: number;
  grade: string | null;
  admissionType: string;
  previousSchool: string | null;
  phone: string;
  email: string | null;
  fatherName: string | null;
  fatherMobile: string | null;
  motherName: string | null;
  motherMobile: string | null;
  guardianName: string | null;
  guardianRelation: string | null;
  guardianMobile: string | null;
  addressLine: string | null;
  city: string | null;
  state: string | null;
  pincode: string | null;
  registrationFee: number | null;
  notes: string | null;
}

export type UpdateAdmissionRequest = CreateAdmissionRequest;

export interface ConfirmAdmissionRequest {
  admissionFee: number | null;
  admissionFeeReference: string | null;
  bloodGroup: string | null;
  religion: string | null;
  category: string | null;
  medicalNotes: string | null;
  remarks: string | null;
}

export interface EnrollAdmissionRequest {
  rollNumber: string;
  admissionNumber: string;
  admissionDate: string;
  entryPoint: string | null;
  transportRequired: boolean;
  allottedClassSectionId: number;
}

export interface RejectAdmissionRequest {
  rejectionReason: string;
}
