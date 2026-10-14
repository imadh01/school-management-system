import { apiClient } from "@/services/apiClient";
import type {
  CreateRoleRequest,
  PermissionModule,
  Role,
  RoleSummary,
  UpdateRoleRequest,
} from "../types/role.types";

export const roleService = {
  getAll: () => apiClient.get<RoleSummary[]>("/roles").then((r) => r.data),

  getById: (id: number) =>
    apiClient.get<Role>(`/roles/${id}`).then((r) => r.data),

  /** Every permission, grouped by module (read-only: permissions are defined in code). */
  getCatalog: () =>
    apiClient.get<PermissionModule[]>("/permissions").then((r) => r.data),

  create: (request: CreateRoleRequest) =>
    apiClient.post<Role>("/roles", request).then((r) => r.data),

  update: (id: number, request: UpdateRoleRequest) =>
    apiClient.put<Role>(`/roles/${id}`, request).then((r) => r.data),

  /** Replaces the role's whole permission set. */
  setPermissions: (id: number, permissions: string[], rowVersion: string) =>
    apiClient
      .put<Role>(`/roles/${id}/permissions`, { permissions, rowVersion })
      .then((r) => r.data),

  changeStatus: (id: number, isActive: boolean, rowVersion: string) =>
    apiClient
      .patch<Role>(`/roles/${id}/status`, { isActive, rowVersion })
      .then((r) => r.data),
};
