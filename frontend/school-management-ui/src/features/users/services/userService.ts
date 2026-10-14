import { apiClient } from "@/services/apiClient";
import type {
  CreateUserRequest,
  RoleOption,
  UpdateUserRequest,
  UserListItem,
  UserResponse,
  UserStatus,
} from "../types/user.types";

/**
 * E3a: the Users endpoints. Status, password, unlock and delete are account security: the API
 * runs them through one service that refuses your own account and the last active Admin, and
 * signs the user out everywhere.
 */
export const userService = {
  getAll: () =>
    apiClient.get<UserListItem[]>("/users").then((res) => res.data),

  getById: (id: number) =>
    apiClient.get<UserResponse>(`/users/${id}`).then((res) => res.data),

  create: (request: CreateUserRequest) =>
    apiClient.post<UserResponse>("/users", request).then((res) => res.data),

  update: (id: number, request: UpdateUserRequest) =>
    apiClient
      .put<UserResponse>(`/users/${id}`, request)
      .then((res) => res.data),

  changeStatus: (id: number, status: UserStatus, rowVersion: string) =>
    apiClient
      .patch<UserResponse>(`/users/${id}/status`, { status, rowVersion })
      .then((res) => res.data),

  resetPassword: (id: number, newPassword: string) =>
    apiClient.post(`/users/${id}/reset-password`, { newPassword }),

  unlock: (id: number) => apiClient.post(`/users/${id}/unlock`),

  delete: (id: number) => apiClient.delete(`/users/${id}`),

  /** Roles for the Role field (needs Roles.View; the form falls back to the built-in names). */
  getRoles: () =>
    apiClient.get<RoleOption[]>("/roles").then((res) => res.data),
};
