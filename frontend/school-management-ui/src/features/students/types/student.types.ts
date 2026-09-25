export type StudentStatus =
  | "Active"
  | "Inactive"
  | "Transferred"
  | "Passed Out"
  | "Suspended";

export interface StudentResponse {
  id: number;
  admNo: string;
  rollNumber: string;
  classSectionName: string;
  admissionDate: string;
  status: StudentStatus;
  photoUrl: string | null;
  firstName: string;
  middleName: string | null;
  lastName: string;
  gender: string;
  dateOfBirth: string;
  bloodGroup: string | null;
  aadhaarNumber: string | null;
  mobile: string | null;
  email: string | null;
  addressLine: string | null;
  city: string | null;
  state: string | null;
  pincode: string | null;
  fatherName: string | null;
  fatherOccupation: string | null;
  fatherMobile: string | null;
  motherName: string | null;
  motherOccupation: string | null;
  motherMobile: string | null;
  guardianName: string | null;
  guardianRelation: string | null;
  guardianMobile: string | null;
  category: string;
  religion: string | null;
  previousSchool: string | null;
  transportRequired: boolean;
  transportRoute: string | null;
  medicalNotes: string | null;
  nationality: string | null;
  secondNationality: string | null;
  countryOfBirth: string | null;
  preferredName: string | null;
  passportNumber: string | null;
  passportExpiry: string | null;
  visaType: string | null;
  visaExpiry: string | null;
  motherTongue: string | null;
  homeLanguage: string | null;
  englishProficiency: string | null;
  curriculumTrack: string | null;
  admissionType: string;
  custodyArrangement: string | null;
  primaryContactParent: string | null;
  authorizedPickupPersons: string | null;
  mediaConsent: boolean;
  dietaryRequirements: string | null;
  allergies: string | null;
  insuranceProvider: string | null;
  insurancePolicyExpiry: string | null;
  house: string | null;
  ealCode: string | null;
  feeConcessionPercent: number | null;
  specialEducationalNeeds: string | null;
  admissionRegNo: string | null;
  admissionId: number | null;
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
  bloodGroup: string | null;
  aadhaarNumber: string | null;
  mobile: string | null;
  email: string | null;
  addressLine: string | null;
  city: string | null;
  state: string | null;
  pincode: string | null;
  fatherName: string | null;
  fatherOccupation: string | null;
  fatherMobile: string | null;
  motherName: string | null;
  motherOccupation: string | null;
  motherMobile: string | null;
  guardianName: string | null;
  guardianRelation: string | null;
  guardianMobile: string | null;
  category: string;
  religion: string | null;
  previousSchool: string | null;
  transportRequired: boolean;
  transportRoute: string | null;
  medicalNotes: string | null;
  nationality: string | null;
  secondNationality: string | null;
  countryOfBirth: string | null;
  preferredName: string | null;
  passportNumber: string | null;
  passportExpiry: string | null;
  visaType: string | null;
  visaExpiry: string | null;
  motherTongue: string | null;
  homeLanguage: string | null;
  englishProficiency: string | null;
  curriculumTrack: string | null;
  admissionType: string;
  custodyArrangement: string | null;
  primaryContactParent: string | null;
  authorizedPickupPersons: string | null;
  mediaConsent: boolean;
  dietaryRequirements: string | null;
  allergies: string | null;
  insuranceProvider: string | null;
  insurancePolicyExpiry: string | null;
  house: string | null;
  ealCode: string | null;
  feeConcessionPercent: number | null;
  specialEducationalNeeds: string | null;
}

export interface UpdateStudentRequest extends Omit<
  CreateStudentRequest,
  "admNo" | "admissionDate"
> {
  status: StudentStatus;
}

export interface CreateStudentFromAdmissionRequest {
  nationality: string | null;
  curriculumTrack: string | null;
  englishProficiency: string | null;
  ealCode: string | null;
  house: string | null;
  allergies: string | null;
}
