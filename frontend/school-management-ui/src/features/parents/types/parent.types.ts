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
  addressLine: string | null;
  city: string | null;
  state: string | null;
  pincode: string | null;
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
  addressLine: string | null;
  city: string | null;
  state: string | null;
  pincode: string | null;
}

export interface UpdateParentRequest extends CreateParentRequest {
  status: "Active" | "Inactive";
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
