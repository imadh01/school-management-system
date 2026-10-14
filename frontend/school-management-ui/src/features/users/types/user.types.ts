import type { AddressRequest, AddressResponse } from "@/types/address";

/**
 * E3a: staff accounts and their profiles. Same shapes as the API's UserDtos / UserResponse.
 */
export const USER_STATUSES = ["Active", "Inactive", "Suspended"] as const;
export type UserStatus = (typeof USER_STATUSES)[number];

/** Staff profiles: Male or Female (the API's Genders; student records allow more). */
export const STAFF_GENDERS = ["Male", "Female"] as const;

/**
 * Roles that cannot be given yet (decision 1: no student or parent logins until the portal). The
 * API refuses them; the page does not offer them at all.
 */
export const PORTAL_ROLES: readonly string[] = ["Student", "Parent"];

/** The teacher role: choosing it creates the teacher record (and shows Specialization). */
export const TEACHER_ROLE = "Teacher";

export interface UserListItem {
  id: number;
  username: string;
  fullName: string;
  /** True while the name is still the username copied in by the E2a migration. */
  needsName: boolean;
  email: string;
  mobile: string | null;
  role: string | null;
  gender: string | null;
  status: UserStatus | string;
  joiningDate: string | null;
  lastLoginAt: string | null;
  isLocked: boolean;
  mustChangePassword: boolean;
  teacherId: number | null;
  employeeNumber: string | null;
}

export interface StaffProfile {
  fullName: string;
  needsName: boolean;
  gender: string | null;
  dateOfBirth: string | null;
  mobile: string | null;
  joiningDate: string | null;
  qualification: string | null;
  employeeNumber: string | null;
  bloodGroup: string | null;
  emergencyContactName: string | null;
  emergencyContactPhone: string | null;
}

export interface UserResponse {
  id: number;
  username: string;
  email: string;
  status: UserStatus | string;
  roles: string[];
  mustChangePassword: boolean;
  isLocked: boolean;
  lastLoginAt: string | null;
  createdAt: string;
  teacherId: number | null;
  /** Null only for accounts without a staff profile (none yet: student/parent logins come later). */
  profile: StaffProfile | null;
  addresses: AddressResponse[];
  rowVersion: string;
}

export type StaffProfileRequest = Omit<StaffProfile, "needsName">;

export interface CreateUserRequest {
  username: string;
  email: string;
  /** Temporary: the user must change it at the first sign-in. Never shown again after saving. */
  password: string;
  roleName: string;
  profile: StaffProfileRequest;
  addresses: AddressRequest[];
  specialization: string | null;
}

export interface UpdateUserRequest {
  username: string;
  email: string;
  roleName: string;
  profile: StaffProfileRequest;
  addresses: AddressRequest[];
  rowVersion: string;
}

/** A role the form can offer (from GET /api/roles). */
export interface RoleOption {
  id: number;
  name: string;
  isActive: boolean;
}
