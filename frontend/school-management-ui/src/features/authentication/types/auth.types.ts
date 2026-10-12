export interface LoginRequest {
  usernameOrEmail: string;
  password: string;
}

/** Body of /auth/login, /auth/refresh and /auth/change-password. The refresh token is never in it (httpOnly cookie). */
export interface AuthResult {
  userId: number;
  username: string;
  email: string;
  roles: string[];
  token: string;
  expiresAtUtc: string;
  mustChangePassword: boolean;
}

/** The signed-in user as the UI sees it: everything except the access token itself. */
export type CurrentUser = Omit<AuthResult, "token" | "expiresAtUtc">;

/**
 * GET /auth/me. The permission list is resolved on the server (the access token
 * carries none) and is kept in memory only, like the token. It drives what the
 * UI shows; the API still enforces every permission on its own.
 */
export interface CurrentUserResponse {
  userId: number;
  username: string;
  email: string;
  roles: string[];
  permissions: string[];
  mustChangePassword: boolean;
}
