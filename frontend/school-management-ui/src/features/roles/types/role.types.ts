/** E3c: roles and the permission catalog. Same shapes as the API's RoleDtos. */
export interface PermissionInfo {
  name: string;
  description: string;
  /** What must be granted together with it; each entry is satisfied by ANY ONE of its names. */
  requires: string[][];
}

export interface PermissionModule {
  module: string;
  permissions: PermissionInfo[];
}

export interface RoleSummary {
  id: number;
  name: string;
  description: string | null;
  isSystem: boolean;
  isActive: boolean;
  /** True for Admin, which holds the whole catalog in code. */
  hasAllPermissions: boolean;
  userCount: number;
  permissionCount: number;
}

export interface Role {
  id: number;
  name: string;
  description: string | null;
  isSystem: boolean;
  isActive: boolean;
  hasAllPermissions: boolean;
  permissions: string[];
  rowVersion: string;
}

export interface CreateRoleRequest {
  name: string;
  description: string | null;
  permissions: string[];
}

export interface UpdateRoleRequest {
  name: string;
  description: string | null;
  rowVersion: string;
}
