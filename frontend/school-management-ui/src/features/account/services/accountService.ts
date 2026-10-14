import { apiClient } from "@/services/apiClient";
import type { AddressRequest } from "@/types/address";
import type { UserResponse } from "@/features/users/types/user.types";

/** What you may change about yourself (PUT /api/me/profile); an administrator sets the rest. */
export interface UpdateMyProfileRequest {
  email: string;
  fullName: string;
  gender: string | null;
  dateOfBirth: string | null;
  mobile: string | null;
  qualification: string | null;
  bloodGroup: string | null;
  emergencyContactName: string | null;
  emergencyContactPhone: string | null;
  addresses: AddressRequest[];
  rowVersion: string;
}

export const accountService = {
  getMine: () =>
    apiClient.get<UserResponse>("/me/profile").then((res) => res.data),

  updateMine: (request: UpdateMyProfileRequest) =>
    apiClient.put<UserResponse>("/me/profile", request).then((res) => res.data),
};
